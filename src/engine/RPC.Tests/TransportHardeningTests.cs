using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RPC.Host.Web;

namespace RPC.Tests;

/// <summary>
/// Pins the host transport's behaviour against hostile or broken clients. Every case here
/// corresponds to a way a single misbehaving client could previously degrade the server:
/// a malformed heartbeat pong threw out of the receive loop and dropped an otherwise healthy
/// session, an unterminated fragmented message grew the receive buffer without bound, and the
/// static-file route prefix-matched paths it did not own.
/// </summary>
public class TransportHardeningTests : IDisposable
{
    private readonly GameServer _server;
    private readonly CancellationTokenSource _cts = new();

    public TransportHardeningTests()
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

    private async Task<ClientWebSocket> ConnectAsync()
    {
        var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://localhost:{_server.Port}/"), _cts.Token);
        await ReceiveAsync(ws); // hello
        return ws;
    }

    private static async Task SendAsync(ClientWebSocket ws, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<JsonElement> ReceiveAsync(ClientWebSocket ws)
    {
        var buffer = new byte[16384];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new WebSocketException($"Server closed the connection: {ws.CloseStatus}");
            }
            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(ms.ToArray()));
    }

    /// <summary>
    /// Reads until a frame of the wanted type arrives, skipping unrelated server traffic such as
    /// heartbeat pings so the assertions do not depend on heartbeat timing.
    /// </summary>
    private static async Task<JsonElement> ReceiveOfTypeAsync(ClientWebSocket ws, string type)
    {
        for (int i = 0; i < 20; i++)
        {
            var frame = await ReceiveAsync(ws);
            if (frame.GetProperty("type").GetString() == type) return frame;
        }
        throw new InvalidOperationException($"No '{type}' frame arrived within 20 messages");
    }

    /// <summary>
    /// A command that reaches its handler and is rejected for what it carried — here a party slot
    /// that does not exist — is the client's action being wrong, not the server failing. Reporting
    /// it as "internal_error" told the player nothing and pointed anyone debugging it at a server
    /// fault that was not there.
    /// </summary>
    [Fact]
    public async Task Action_Rejected_For_A_Bad_Argument_Reports_Invalid_Action()
    {
        using var ws = await ConnectAsync();
        await SendAsync(ws, """{"v":2,"type":"ready","seq":1}""");
        await ReceiveOfTypeAsync(ws, "state");

        await SendAsync(ws, """{"v":2,"type":"action","seq":2,"payload":{"type":"swap_row","slot":99}}""");
        var error = await ReceiveOfTypeAsync(ws, "error");

        Assert.Equal("invalid_action", error.GetProperty("payload").GetProperty("code").GetString());
        Assert.Equal(WebSocketState.Open, ws.State);
    }

    [Fact]
    public async Task Malformed_Heartbeat_Pong_Does_Not_Drop_The_Connection()
    {
        using var ws = await ConnectAsync();

        // pingSeq as a string rather than a number: previously GetInt32() threw, unwound the
        // receive loop, and killed the session.
        await SendAsync(ws, """{"v":2,"type":"heartbeat.pong","seq":1,"payload":{"pingSeq":"not-a-number"}}""");
        await SendAsync(ws, """{"v":2,"type":"heartbeat.pong","seq":2,"payload":null}""");
        await SendAsync(ws, """{"v":2,"type":"heartbeat.pong","seq":3,"payload":{}}""");

        // The session must still serve traffic.
        await SendAsync(ws, """{"v":2,"type":"ready","seq":4}""");
        var state = await ReceiveOfTypeAsync(ws, "state");

        Assert.Equal("state", state.GetProperty("type").GetString());
        Assert.Equal(WebSocketState.Open, ws.State);
    }

    /// <summary>
    /// A client that goes silent — a dropped link, a sleeping machine — never answers a heartbeat
    /// and never sends a close frame either. The server used to decide such a client was gone and
    /// then wait on it anyway: the close handshake waited for an answering frame with no
    /// cancellation, and the receive loop's ReceiveAsync was tied only to server shutdown, so
    /// neither ever completed. The connection stayed registered for the life of the process, and
    /// every wedged client added another.
    ///
    /// Silence is produced by simply never reading: the client acknowledges nothing the server
    /// sends, which is exactly what a black-holed connection looks like from the server's side.
    /// </summary>
    [Fact]
    public async Task Unresponsive_Client_Is_Deregistered_Instead_Of_Leaking_Its_Session()
    {
        using var ws = await ConnectAsync();
        await SendAsync(ws, """{"v":2,"type":"ready","seq":1}""");
        Assert.Equal(1, _server.ClientCount);

        // Ping interval + pong timeout + close timeout, with headroom for a loaded machine.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (_server.ClientCount > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100, _cts.Token);
        }

        Assert.Equal(0, _server.ClientCount);
    }

    [Fact]
    public async Task Malformed_Envelope_Is_Answered_With_A_Recoverable_Error()
    {
        using var ws = await ConnectAsync();

        await SendAsync(ws, "{ this is not json");
        var error = await ReceiveOfTypeAsync(ws, "error");

        Assert.Equal("malformed_payload", error.GetProperty("payload").GetProperty("code").GetString());
        Assert.True(error.GetProperty("payload").GetProperty("recoverable").GetBoolean());
        Assert.Equal(WebSocketState.Open, ws.State);
    }

    [Fact]
    public async Task Oversized_Message_Closes_The_Connection_Instead_Of_Growing_The_Buffer()
    {
        using var ws = await ConnectAsync();

        // Just over the 1 MiB inbound cap.
        var filler = new string('x', (1024 * 1024) + 1024);
        await SendAsync(ws, "{\"v\":2,\"type\":\"action\",\"seq\":1,\"payload\":{\"junk\":\"" + filler + "\"}}");

        var closed = await WaitForCloseAsync(ws, TimeSpan.FromSeconds(10));
        Assert.True(closed, $"Server did not close the oversized session; state={ws.State}");
    }

    private static async Task<bool> WaitForCloseAsync(ClientWebSocket ws, TimeSpan timeout)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        var buffer = new byte[4096];
        try
        {
            while (!timeoutCts.Token.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), timeoutCts.Token);
                if (result.MessageType == WebSocketMessageType.Close) return true;
            }
        }
        catch (OperationCanceledException) { return false; }
        catch (WebSocketException) { return true; }
        return false;
    }

    [Theory]
    [InlineData("/api/dungeon")]  // removed: served hardcoded placeholder data no client consumed
    [InlineData("/appliance")]    // must not prefix-match the /app static route
    [InlineData("/assetsfoo")]
    [InlineData("/nope")]
    public async Task Unowned_Paths_Return_404(string path)
    {
        using var http = new HttpClient();
        var response = await http.GetAsync($"http://localhost:{_server.Port}{path}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Status_Endpoint_Still_Answers()
    {
        using var http = new HttpClient();
        var response = await http.GetAsync($"http://localhost:{_server.Port}/api/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", body.GetProperty("status").GetString());
    }

    [Fact]
    public void Stop_Is_Idempotent()
    {
        var server = new GameServer(port: GetFreePort(), loadSave: false);
        server.Start();

        server.Stop();
        server.Stop(); // must not throw on the already-disposed listener
    }
}
