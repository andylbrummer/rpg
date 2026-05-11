using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RPC.Host.Web;

namespace RPC.Tests;

public class ProtocolEnvelopeTests : IDisposable
{
    private readonly GameServer _server;
    private readonly CancellationTokenSource _cts = new();

    public ProtocolEnvelopeTests()
    {
        var port = GetFreePort();
        _server = new GameServer(port: port);
        _server.Start();
    }

    public void Dispose()
    {
        _server.Stop();
        _cts.Cancel();
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
        var port = _server.Port;
        await ws.ConnectAsync(new Uri($"ws://localhost:{port}/"), _cts.Token);
        return ws;
    }

    private static async Task<JsonElement> ReceiveAsync(ClientWebSocket ws)
    {
        var buffer = new byte[16384];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        var json = Encoding.UTF8.GetString(ms.ToArray());
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static async Task SendAsync(ClientWebSocket ws, object message)
    {
        var json = JsonSerializer.Serialize(message);
        await ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    [Fact]
    public async Task Hello_Sent_On_Connect_With_ProtocolVersion_2()
    {
        using var ws = await ConnectAsync();
        var msg = await ReceiveAsync(ws);

        Assert.Equal("hello", msg.GetProperty("type").GetString());
        Assert.Equal(2, msg.GetProperty("v").GetInt32());
        Assert.Equal(0, msg.GetProperty("seq").GetInt32());
        Assert.Equal(2, msg.GetProperty("payload").GetProperty("protocolVersion").GetInt32());
        Assert.False(string.IsNullOrEmpty(msg.GetProperty("payload").GetProperty("sessionId").GetString()));
    }

    [Fact]
    public async Task Seq_Monotonic_On_Server_Messages()
    {
        using var ws = await ConnectAsync();
        var seqs = new List<int>();

        // hello
        var hello = await ReceiveAsync(ws);
        seqs.Add(hello.GetProperty("seq").GetInt32());

        // send ready
        await SendAsync(ws, new { v = 2, type = "ready", seq = 1, payload = new { } });

        // state
        var state = await ReceiveAsync(ws);
        seqs.Add(state.GetProperty("seq").GetInt32());

        // send action
        await SendAsync(ws, new { v = 2, type = "action", seq = 2, payload = new { type = "move_forward" } });

        // state ack
        var state2 = await ReceiveAsync(ws);
        seqs.Add(state2.GetProperty("seq").GetInt32());

        for (int i = 1; i < seqs.Count; i++)
        {
            Assert.True(seqs[i] > seqs[i - 1], $"Sequence not monotonic: {seqs[i]} <= {seqs[i - 1]}");
        }
    }

    [Fact]
    public async Task Client_Seq_Echoed_In_State_Ack()
    {
        using var ws = await ConnectAsync();
        await ReceiveAsync(ws); // hello

        await SendAsync(ws, new { v = 2, type = "ready", seq = 1, payload = new { } });
        await ReceiveAsync(ws); // initial state

        await SendAsync(ws, new { v = 2, type = "action", seq = 42, payload = new { type = "reset_game" } });

        JsonElement state = default;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        int clientSeq = 2;
        while (!cts.Token.IsCancellationRequested)
        {
            var msg = await ReceiveAsync(ws);
            var msgType = msg.GetProperty("type").GetString();
            if (msgType == "state")
            {
                state = msg;
                break;
            }
            if (msgType == "heartbeat.ping")
            {
                var pingSeq = msg.GetProperty("payload").GetProperty("pingSeq").GetInt32();
                await SendAsync(ws, new { v = 2, type = "heartbeat.pong", seq = clientSeq++, payload = new { pingSeq } });
            }
        }

        Assert.Equal("state", state.GetProperty("type").GetString());
        Assert.Equal(42, state.GetProperty("ackSeq").GetInt32());
    }

    [Fact]
    public async Task Malformed_Json_Returns_Error_Malformed_Payload_Recoverable()
    {
        using var ws = await ConnectAsync();
        await ReceiveAsync(ws); // hello

        await ws.SendAsync(Encoding.UTF8.GetBytes("not json"), WebSocketMessageType.Text, true, CancellationToken.None);

        var msg = await ReceiveAsync(ws);
        Assert.Equal("error", msg.GetProperty("type").GetString());
        Assert.Equal("malformed_payload", msg.GetProperty("payload").GetProperty("code").GetString());
        Assert.True(msg.GetProperty("payload").GetProperty("recoverable").GetBoolean());
    }

    [Fact]
    public async Task Action_Before_Ready_Returns_Not_Ready_Error()
    {
        using var ws = await ConnectAsync();
        await ReceiveAsync(ws); // hello

        await SendAsync(ws, new { v = 2, type = "action", seq = 1, payload = new { type = "move_forward" } });

        var msg = await ReceiveAsync(ws);
        Assert.Equal("error", msg.GetProperty("type").GetString());
        Assert.Equal("not_ready", msg.GetProperty("payload").GetProperty("code").GetString());
        Assert.True(msg.GetProperty("payload").GetProperty("recoverable").GetBoolean());
    }

    [Fact]
    public async Task Heartbeat_Ping_Sent_After_Ready()
    {
        using var ws = await ConnectAsync();
        await ReceiveAsync(ws); // hello

        await SendAsync(ws, new { v = 2, type = "ready", seq = 1, payload = new { } });
        await ReceiveAsync(ws); // state

        // Wait for heartbeat ping (server sends every 5s, but we use a shorter timeout for tests)
        // The server may have already sent one depending on timing.
        // We use a 6s timeout to reliably catch at least one ping.
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        while (!cts.Token.IsCancellationRequested)
        {
            var msg = await ReceiveAsync(ws);
            if (msg.GetProperty("type").GetString() == "heartbeat.ping")
            {
                Assert.True(msg.GetProperty("payload").TryGetProperty("pingSeq", out _));
                return;
            }
        }

        Assert.Fail("Expected heartbeat.ping within timeout");
    }

    [Fact]
    public async Task Heartbeat_Pong_Keeps_Connection_Alive()
    {
        using var ws = await ConnectAsync();
        await ReceiveAsync(ws); // hello

        await SendAsync(ws, new { v = 2, type = "ready", seq = 1, payload = new { } });
        await ReceiveAsync(ws); // state

        // Wait for ping
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        JsonElement ping = default;
        bool gotPing = false;
        while (!cts.Token.IsCancellationRequested)
        {
            var msg = await ReceiveAsync(ws);
            if (msg.GetProperty("type").GetString() == "heartbeat.ping")
            {
                ping = msg;
                gotPing = true;
                break;
            }
        }

        Assert.True(gotPing, "Expected heartbeat.ping");

        var pingSeq = ping.GetProperty("payload").GetProperty("pingSeq").GetInt32();
        await SendAsync(ws, new { v = 2, type = "heartbeat.pong", seq = 2, payload = new { pingSeq } });

        // Connection should still be open; send another action and expect state
        await SendAsync(ws, new { v = 2, type = "action", seq = 3, payload = new { type = "turn_left" } });
        var state = await ReceiveAsync(ws);
        Assert.Equal("state", state.GetProperty("type").GetString());
    }
}
