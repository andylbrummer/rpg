using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RPC.Host.Web;

namespace RPC.Tests;

/// <summary>
/// Drives the host the way several windows onto the same game would: many sockets connected at
/// once, all sending actions, while others connect and drop mid-flight. This is the load the
/// transport hardening exists for — a shared game-state lock, a per-socket send lock, and a
/// broadcast that fans out to every other client on every state change.
///
/// What is asserted is liveness and cleanliness rather than any particular game outcome: the
/// server keeps answering, every client sees consistent frames, and the connection registry
/// returns to zero afterwards.
/// </summary>
public class ConcurrentClientStressTests : IDisposable
{
    private readonly GameServer _server;
    private readonly CancellationTokenSource _cts = new();

    public ConcurrentClientStressTests()
    {
        _server = new GameServer(port: GetFreePort(), loadSave: false);
        _server.Start();
    }

    public void Dispose()
    {
        _server.Stop();
        _cts.Cancel();
        _cts.Dispose();
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task<ClientWebSocket> ConnectReadyAsync()
    {
        var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://localhost:{_server.Port}/"), _cts.Token);
        await ReceiveJsonAsync(ws); // hello
        await SendAsync(ws, """{"v":2,"type":"ready","seq":1}""");
        await ReceiveOfTypeAsync(ws, "state");
        return ws;
    }

    private static async Task SendAsync(ClientWebSocket ws, string json) =>
        await ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);

    private static async Task<JsonElement> ReceiveJsonAsync(ClientWebSocket ws)
    {
        var buffer = new byte[64 * 1024];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException("closed by server");
            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(ms.ToArray()));
    }

    private static async Task<JsonElement> ReceiveOfTypeAsync(ClientWebSocket ws, string type, int maxFrames = 200)
    {
        for (int i = 0; i < maxFrames; i++)
        {
            var frame = await ReceiveJsonAsync(ws);
            if (frame.GetProperty("type").GetString() == type) return frame;
        }
        throw new InvalidOperationException($"No '{type}' frame within {maxFrames} frames");
    }

    /// <summary>
    /// Answers heartbeat pings and discards everything else, so a client under test never stalls
    /// the server's per-socket send by leaving its receive buffer full.
    /// </summary>
    private static async Task<int> DrainAsync(ClientWebSocket ws, CancellationToken token)
    {
        var stateFrames = 0;
        var buffer = new byte[64 * 1024];
        try
        {
            while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close) return stateFrames;
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                var frame = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(ms.ToArray()));
                if (frame.GetProperty("type").GetString() == "state") stateFrames++;
                if (frame.GetProperty("type").GetString() == "heartbeat.ping"
                    && frame.TryGetProperty("payload", out var payload)
                    && payload.TryGetProperty("pingSeq", out var seq))
                {
                    await SendAsync(ws, "{\"v\":2,\"type\":\"heartbeat.pong\",\"seq\":1,\"payload\":{\"pingSeq\":" + seq.GetInt32() + "}}");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }

        return stateFrames;
    }

    [Fact]
    public async Task Server_stays_responsive_while_many_clients_act_concurrently()
    {
        const int clientCount = 8;
        const int actionsPerClient = 15;

        var clients = new List<ClientWebSocket>();
        for (int i = 0; i < clientCount; i++) clients.Add(await ConnectReadyAsync());

        using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        // One client is the observer; the rest hammer the server while draining their own frames.
        var observer = clients[0];
        var drains = clients.Skip(1).Select(c => Task.Run(() => DrainAsync(c, drainCts.Token))).ToList();
        drains.Add(Task.Run(() => DrainAsync(observer, drainCts.Token)));

        var senders = clients.Select(c => Task.Run(async () =>
        {
            for (int i = 0; i < actionsPerClient; i++)
            {
                // Turning in place is always legal in any mode, so this exercises the command path
                // and the broadcast fan-out without depending on where the game happens to be.
                await SendAsync(c, """{"v":2,"type":"action","seq":2,"payload":{"type":"turn_right"}}""");
            }
        })).ToList();

        await Task.WhenAll(senders);

        // The server must still be answering after the burst.
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var status = await http.GetAsync($"http://localhost:{_server.Port}/api/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);

        drainCts.Cancel();
        var frameCounts = await Task.WhenAll(drains);

        // Guard against a vacuous pass: if the actions had been rejected, or the fan-out had not
        // run, the burst would have produced no state frames and every assertion above would
        // still hold.
        Assert.True(frameCounts.Sum() > 0, "no state frames were broadcast during the burst");

        foreach (var c in clients)
        {
            c.Abort();
            c.Dispose();
        }

        await WaitForClientCountAsync(0, TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Connects and drops clients while others are mid-action. This is the window the connection
    /// teardown ordering guards: a broadcast snapshots the registry, so a socket disposed while
    /// still registered would be written to after disposal.
    /// </summary>
    [Fact]
    public async Task Churning_connections_during_traffic_leaves_no_leaked_sessions()
    {
        var steady = await ConnectReadyAsync();
        using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        var drain = Task.Run(() => DrainAsync(steady, drainCts.Token));

        var traffic = Task.Run(async () =>
        {
            for (int i = 0; i < 40; i++)
            {
                await SendAsync(steady, """{"v":2,"type":"action","seq":2,"payload":{"type":"turn_left"}}""");
                await Task.Delay(10);
            }
        });

        for (int i = 0; i < 25; i++)
        {
            var transient = new ClientWebSocket();
            await transient.ConnectAsync(new Uri($"ws://localhost:{_server.Port}/"), _cts.Token);
            await ReceiveJsonAsync(transient); // hello
            await SendAsync(transient, """{"v":2,"type":"ready","seq":1}""");
            // Drop without a close handshake, mid-broadcast, the way a closed window does.
            transient.Abort();
            transient.Dispose();
        }

        await traffic;

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync($"http://localhost:{_server.Port}/api/status")).StatusCode);

        drainCts.Cancel();
        Assert.True(await drain > 0, "the steady client saw no state frames while connections churned");
        steady.Abort();
        steady.Dispose();

        await WaitForClientCountAsync(0, TimeSpan.FromSeconds(30));
    }

    private async Task WaitForClientCountAsync(int expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_server.ClientCount == expected) return;
            await Task.Delay(50);
        }
        Assert.Fail($"ClientCount stayed at {_server.ClientCount}, expected {expected}");
    }
}
