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

    private static async Task<JsonElement> WaitForStateAsync(ClientWebSocket ws, CancellationToken ct)
    {
        int clientSeq = 100;
        while (!ct.IsCancellationRequested)
        {
            var msg = await ReceiveAsync(ws);
            var msgType = msg.GetProperty("type").GetString();
            if (msgType == "state")
                return msg;
            if (msgType == "heartbeat.ping")
            {
                var pingSeq = msg.GetProperty("payload").GetProperty("pingSeq").GetInt32();
                await SendAsync(ws, new { v = 2, type = "heartbeat.pong", seq = clientSeq++, payload = new { pingSeq } });
            }
        }
        throw new TimeoutException("Timed out waiting for state message");
    }

    private static async Task<JsonElement> WaitForErrorAsync(ClientWebSocket ws, CancellationToken ct)
    {
        int clientSeq = 100;
        while (!ct.IsCancellationRequested)
        {
            var msg = await ReceiveAsync(ws);
            var msgType = msg.GetProperty("type").GetString();
            if (msgType == "error")
                return msg;
            if (msgType == "heartbeat.ping")
            {
                var pingSeq = msg.GetProperty("payload").GetProperty("pingSeq").GetInt32();
                await SendAsync(ws, new { v = 2, type = "heartbeat.pong", seq = clientSeq++, payload = new { pingSeq } });
            }
        }
        throw new TimeoutException("Timed out waiting for error message");
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
    public async Task AckSeq_Only_Sent_To_Originating_Client()
    {
        using var ws1 = await ConnectAsync();
        using var ws2 = await ConnectAsync();
        await ReceiveAsync(ws1); // hello
        await ReceiveAsync(ws2); // hello

        await SendAsync(ws1, new { v = 2, type = "ready", seq = 1, payload = new { } });
        await SendAsync(ws2, new { v = 2, type = "ready", seq = 1, payload = new { } });
        await WaitForStateAsync(ws1, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        await WaitForStateAsync(ws2, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);

        // Client 1 sends an action with seq = 99
        await SendAsync(ws1, new { v = 2, type = "action", seq = 99, payload = new { type = "turn_left" } });

        // Drain messages from both
        JsonElement state1 = default, state2 = default;
        var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        int pongSeq = 300;

        while (!drainCts.Token.IsCancellationRequested && (state1.ValueKind == JsonValueKind.Undefined || state2.ValueKind == JsonValueKind.Undefined))
        {
            if (ws1.State == WebSocketState.Open && state1.ValueKind == JsonValueKind.Undefined)
            {
                var msg1 = await ReceiveAsync(ws1);
                var type1 = msg1.GetProperty("type").GetString();
                if (type1 == "state") state1 = msg1;
                else if (type1 == "heartbeat.ping")
                {
                    var pingSeq = msg1.GetProperty("payload").GetProperty("pingSeq").GetInt32();
                    await SendAsync(ws1, new { v = 2, type = "heartbeat.pong", seq = pongSeq++, payload = new { pingSeq } });
                }
            }
            if (ws2.State == WebSocketState.Open && state2.ValueKind == JsonValueKind.Undefined)
            {
                var msg2 = await ReceiveAsync(ws2);
                var type2 = msg2.GetProperty("type").GetString();
                if (type2 == "state") state2 = msg2;
                else if (type2 == "heartbeat.ping")
                {
                    var pingSeq = msg2.GetProperty("payload").GetProperty("pingSeq").GetInt32();
                    await SendAsync(ws2, new { v = 2, type = "heartbeat.pong", seq = pongSeq++, payload = new { pingSeq } });
                }
            }
        }

        Assert.Equal("state", state1.GetProperty("type").GetString());
        Assert.Equal(99, state1.GetProperty("ackSeq").GetInt32());

        Assert.Equal("state", state2.GetProperty("type").GetString());
        if (state2.TryGetProperty("ackSeq", out var ackSeqProp))
        {
            Assert.True(ackSeqProp.ValueKind == JsonValueKind.Null, "Non-originating client should not receive a non-null ackSeq");
        }
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

    [Fact]
    public async Task Stale_Protocol_Version_Returns_Error()
    {
        using var ws = await ConnectAsync();
        await ReceiveAsync(ws); // hello

        await SendAsync(ws, new { v = 1, type = "ready", seq = 1, payload = new { } });
        var error = await WaitForErrorAsync(ws, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);

        Assert.Equal("malformed_payload", error.GetProperty("payload").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Invalid_Action_Type_Returns_Error()
    {
        using var ws = await ConnectAsync();
        await ReceiveAsync(ws); // hello

        await SendAsync(ws, new { v = 2, type = "ready", seq = 1, payload = new { } });
        await WaitForStateAsync(ws, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);

        await SendAsync(ws, new { v = 2, type = "action", seq = 2, payload = new { type = "unknown_action_xyz" } });
        var error = await WaitForErrorAsync(ws, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);

        Assert.Equal("invalid_action", error.GetProperty("payload").GetProperty("code").GetString());
        Assert.True(error.GetProperty("payload").GetProperty("recoverable").GetBoolean());
        Assert.Contains("unknown_action_xyz", error.GetProperty("payload").GetProperty("message").GetString());
    }

    [Fact]
    public async Task Turn_Left_Changes_Player_Facing()
    {
        using var ws = await ConnectAsync();
        await ReceiveAsync(ws); // hello

        await SendAsync(ws, new { v = 2, type = "ready", seq = 1, payload = new { } });
        var initialState = await WaitForStateAsync(ws, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.Equal("North", initialState.GetProperty("payload").GetProperty("player").GetProperty("facing").GetString());

        await SendAsync(ws, new { v = 2, type = "action", seq = 2, payload = new { type = "turn_left" } });
        var state = await WaitForStateAsync(ws, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);

        Assert.Equal("West", state.GetProperty("payload").GetProperty("player").GetProperty("facing").GetString());
    }

    [Fact]
    public async Task Turn_Right_Changes_Player_Facing()
    {
        using var ws = await ConnectAsync();
        await ReceiveAsync(ws); // hello

        await SendAsync(ws, new { v = 2, type = "ready", seq = 1, payload = new { } });
        var initialState = await WaitForStateAsync(ws, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.Equal("North", initialState.GetProperty("payload").GetProperty("player").GetProperty("facing").GetString());

        await SendAsync(ws, new { v = 2, type = "action", seq = 2, payload = new { type = "turn_right" } });
        var state = await WaitForStateAsync(ws, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);

        Assert.Equal("East", state.GetProperty("payload").GetProperty("player").GetProperty("facing").GetString());
    }

    [Fact]
    public async Task Enter_Dungeon_Then_Move_Forward_Changes_Position()
    {
        using var ws = await ConnectAsync();
        await ReceiveAsync(ws); // hello

        await SendAsync(ws, new { v = 2, type = "ready", seq = 1, payload = new { } });
        var initialState = await WaitForStateAsync(ws, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        Assert.Equal("Menu", initialState.GetProperty("payload").GetProperty("mode").GetString());

        // Enter dungeon
        await SendAsync(ws, new { v = 2, type = "action", seq = 2, payload = new { type = "enter_dungeon", dungeonType = "broken_engine" } });

        // Collect next message (could be state, error, or heartbeat)
        JsonElement dungeonState = default;
        var found = false;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        int pongSeq = 200;
        while (!cts.Token.IsCancellationRequested)
        {
            var msg = await ReceiveAsync(ws);
            var msgType = msg.GetProperty("type").GetString();
            if (msgType == "state")
            {
                dungeonState = msg;
                found = true;
                break;
            }
            if (msgType == "error")
            {
                var code = msg.GetProperty("payload").GetProperty("code").GetString();
                var errmsg = msg.GetProperty("payload").GetProperty("message").GetString();
                Assert.Fail($"Received error instead of state: {code} - {errmsg}");
            }
            if (msgType == "heartbeat.ping")
            {
                var pingSeq = msg.GetProperty("payload").GetProperty("pingSeq").GetInt32();
                await SendAsync(ws, new { v = 2, type = "heartbeat.pong", seq = pongSeq++, payload = new { pingSeq } });
            }
        }
        Assert.True(found, "Timed out waiting for state after enter_dungeon");

        Assert.Equal("Exploration", dungeonState.GetProperty("payload").GetProperty("mode").GetString());
        Assert.True(dungeonState.GetProperty("payload").GetProperty("hasDungeon").GetBoolean());

        var startX = dungeonState.GetProperty("payload").GetProperty("player").GetProperty("x").GetInt32();
        var startY = dungeonState.GetProperty("payload").GetProperty("player").GetProperty("y").GetInt32();

        // Turn right to face East (entrance room has open space to the east)
        await SendAsync(ws, new { v = 2, type = "action", seq = 3, payload = new { type = "turn_right" } });
        await WaitForStateAsync(ws, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);

        // Move forward
        await SendAsync(ws, new { v = 2, type = "action", seq = 4, payload = new { type = "move_forward" } });
        var moveState = await WaitForStateAsync(ws, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);

        var endX = moveState.GetProperty("payload").GetProperty("player").GetProperty("x").GetInt32();
        var endY = moveState.GetProperty("payload").GetProperty("player").GetProperty("y").GetInt32();

        // Position should have changed (moved east from entrance)
        Assert.True(endX != startX || endY != startY, $"Expected position to change from ({startX},{startY}) but got ({endX},{endY})");
    }

    [Fact]
    public async Task Concurrent_Actions_From_Two_Clients_Both_Receive_Valid_States()
    {
        using var ws1 = await ConnectAsync();
        using var ws2 = await ConnectAsync();
        await ReceiveAsync(ws1); // hello
        await ReceiveAsync(ws2); // hello

        await SendAsync(ws1, new { v = 2, type = "ready", seq = 1, payload = new { } });
        await SendAsync(ws2, new { v = 2, type = "ready", seq = 1, payload = new { } });
        await WaitForStateAsync(ws1, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        await WaitForStateAsync(ws2, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);

        // Both clients send actions concurrently to stress the mutation + send paths
        var t1 = SendAsync(ws1, new { v = 2, type = "action", seq = 2, payload = new { type = "turn_left" } });
        var t2 = SendAsync(ws2, new { v = 2, type = "action", seq = 2, payload = new { type = "turn_right" } });
        await Task.WhenAll(t1, t2);

        // Drain messages from both clients; every received envelope must be valid JSON
        var states1 = new List<JsonElement>();
        var states2 = new List<JsonElement>();
        var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        int pongSeq = 300;

        while (!drainCts.Token.IsCancellationRequested && (states1.Count < 2 || states2.Count < 2))
        {
            if (ws1.State == WebSocketState.Open)
            {
                var msg1 = await ReceiveAsync(ws1);
                var type1 = msg1.GetProperty("type").GetString();
                if (type1 == "state") states1.Add(msg1);
                else if (type1 == "heartbeat.ping")
                {
                    var pingSeq = msg1.GetProperty("payload").GetProperty("pingSeq").GetInt32();
                    await SendAsync(ws1, new { v = 2, type = "heartbeat.pong", seq = pongSeq++, payload = new { pingSeq } });
                }
            }
            if (ws2.State == WebSocketState.Open)
            {
                var msg2 = await ReceiveAsync(ws2);
                var type2 = msg2.GetProperty("type").GetString();
                if (type2 == "state") states2.Add(msg2);
                else if (type2 == "heartbeat.ping")
                {
                    var pingSeq = msg2.GetProperty("payload").GetProperty("pingSeq").GetInt32();
                    await SendAsync(ws2, new { v = 2, type = "heartbeat.pong", seq = pongSeq++, payload = new { pingSeq } });
                }
            }
        }

        Assert.True(states1.Count >= 1, "Client 1 should have received at least one state update");
        Assert.True(states2.Count >= 1, "Client 2 should have received at least one state update");

        // All received payloads should be parseable objects (not malformed interleaved bytes)
        foreach (var s in states1)
        {
            Assert.Equal("state", s.GetProperty("type").GetString());
            Assert.True(s.GetProperty("payload").TryGetProperty("mode", out _), "State payload should contain mode");
        }
        foreach (var s in states2)
        {
            Assert.Equal("state", s.GetProperty("type").GetString());
            Assert.True(s.GetProperty("payload").TryGetProperty("mode", out _), "State payload should contain mode");
        }
    }
}
