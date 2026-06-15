using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using RPC.Engine.Protocol;
using RPC.Host.Web.Protocol;

namespace RPC.Host.Web;

/// <summary>
/// Owns a single client's WebSocket lifecycle: accept, the hello handshake, the receive loop,
/// and the per-client heartbeat. Inbound text frames are forwarded to the
/// <see cref="ProtocolMessageHandler"/>. Extracted from <see cref="GameServer"/> as the
/// WebSocket transport seam.
/// </summary>
internal sealed class WebSocketConnectionHandler
{
    private readonly ClientRegistry _registry;
    private readonly StateBroadcaster _broadcaster;
    private readonly ProtocolMessageHandler _protocol;
    private readonly CancellationTokenSource _cts;

    public WebSocketConnectionHandler(
        ClientRegistry registry,
        StateBroadcaster broadcaster,
        ProtocolMessageHandler protocol,
        CancellationTokenSource cts)
    {
        _registry = registry;
        _broadcaster = broadcaster;
        _protocol = protocol;
        _cts = cts;
    }

    public async Task Handle(HttpListenerContext context)
    {
        var wsContext = await context.AcceptWebSocketAsync(null);
        var socket = wsContext.WebSocket;
        var client = new ClientConnection(socket);
        _registry.Add(client);

        try
        {
            await _broadcaster.SendEnvelope(client, new ProtocolEnvelope
        {
            V = 2,
            Type = "hello",
            Seq = client.NextServerSeq(),
            Payload = new HelloPayload { ProtocolVersion = 2, SessionId = client.SessionId }
        });
            _ = Task.Run(() => RunHeartbeatLoop(client));

            var buffer = new byte[4096];
            while (socket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close) break;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(ms.ToArray());
                    await _protocol.HandleMessage(client, message);
                }
            }
        }
        catch (WebSocketException) { }
        finally
        {
            client.Dispose();
            _registry.Remove(client);
        }
    }

    private async Task RunHeartbeatLoop(ClientConnection client)
    {
        try
        {
            while (client.Socket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token);
                if (client.Socket.State != WebSocketState.Open) break;
                if (!client.IsReady) continue;

                var pingSeq = client.NextPingSeq();
                client.LastPingSeq = pingSeq;
                client.LastPingTime = DateTime.UtcNow;
                await SendPing(client, pingSeq);

                // Wait up to 2s for pong
                var pongCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try
                {
                    while (client.Socket.State == WebSocketState.Open && !pongCts.Token.IsCancellationRequested)
                    {
                        if (client.LastPongSeq >= pingSeq)
                            break;
                        await Task.Delay(100, pongCts.Token);
                    }
                }
                catch (TaskCanceledException) { }

                if (client.LastPongSeq < pingSeq && client.Socket.State == WebSocketState.Open)
                {
                    await client.Socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Heartbeat timeout", CancellationToken.None);
                    break;
                }
            }
        }
        catch (WebSocketException) { }
        catch (OperationCanceledException) { }
    }

    private async Task SendPing(ClientConnection client, int pingSeq)
    {
        await _broadcaster.SendEnvelope(client, new ProtocolEnvelope
        {
            V = 2,
            Type = "heartbeat.ping",
            Seq = client.NextServerSeq(),
            Payload = new HeartbeatPingPayload { PingSeq = pingSeq }
        });
    }
}
