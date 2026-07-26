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

    /// <summary>
    /// Upper bound on a single inbound WebSocket message. Frames are accumulated until
    /// EndOfMessage, so without a cap a client that never terminates a fragmented message grows
    /// the buffer without limit. Real protocol messages are action envelopes of a few hundred
    /// bytes; 1 MiB is orders of magnitude of headroom.
    /// </summary>
    private const int MaxInboundMessageBytes = 1024 * 1024;

    public async Task Handle(HttpListenerContext context)
    {
        var wsContext = await context.AcceptWebSocketAsync(null);
        var socket = wsContext.WebSocket;
        // The connection owns its own lifetime (linked to server shutdown), so the heartbeat, the
        // receive loop and any in-flight send all stop the moment any one of them decides the
        // client is finished — instead of leaving the others to notice a disposed socket up to one
        // ping interval later.
        var client = new ClientConnection(socket, _cts.Token);
        _registry.Add(client);

        Task heartbeat = Task.CompletedTask;

        try
        {
            await _broadcaster.SendEnvelope(client, new ProtocolEnvelope
            {
                V = 2,
                Type = "hello",
                Seq = client.NextServerSeq(),
                Payload = new HelloPayload { ProtocolVersion = 2, SessionId = client.SessionId }
            });
            heartbeat = Task.Run(() => RunHeartbeatLoop(client));

            var buffer = new byte[4096];
            while (socket.State == WebSocketState.Open && !client.Token.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                var oversized = false;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), client.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    if (ms.Length + result.Count > MaxInboundMessageBytes)
                    {
                        oversized = true;
                        break;
                    }
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (oversized)
                {
                    await client.CloseQuietly(WebSocketCloseStatus.MessageTooBig, "Message exceeds size limit");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close) break;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(ms.ToArray());
                    await _protocol.HandleMessage(client, message);
                }
            }
        }
        catch (WebSocketException) { }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        finally
        {
            // Stop and drain the heartbeat before disposing, so it can never touch a disposed
            // socket; then deregister before disposing, so a broadcast that snapshots the
            // registry never hands out a connection whose socket is already gone.
            client.Abort();
            await DrainHeartbeat(heartbeat);
            _registry.Remove(client);
            client.Dispose();
        }
    }

    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How many consecutive pings a client may leave unanswered before it is considered gone.
    /// <para>
    /// A single missed pong does not mean a client is dead. The browser answers on its main thread,
    /// and this is a WebGL dungeon crawler: building a dungeon's geometry, compiling a shader, or a
    /// GC pause blocks that thread for longer than one interval. Killing on the first miss dropped
    /// healthy sessions at exactly the busiest moments — the player saw "Connection lost", and
    /// input entered while down is discarded by design, so the action that triggered the stall
    /// appeared to do nothing.
    /// </para>
    /// <para>
    /// Three misses still deregisters a genuinely silent client inside ~15s, which is what keeps a
    /// black-holed connection from leaking its session for the life of the process.
    /// </para>
    /// </summary>
    private const int MaxMissedPongs = 3;

    /// <summary>
    /// Pings the client until it stops answering, then cancels the connection.
    /// <para>
    /// Cancelling is the point: a client that fails its heartbeat has usually gone silent at the
    /// TCP level rather than closed, so the receive loop's pending ReceiveAsync would never
    /// return on its own. Deciding the client is gone and not unblocking the reader left the
    /// connection registered — and the session leaked — for the life of the process.
    /// </para>
    /// </summary>
    private async Task RunHeartbeatLoop(ClientConnection client)
    {
        var token = client.Token;
        try
        {
            while (client.Socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                await Task.Delay(PingInterval, token);
                if (client.Socket.State != WebSocketState.Open) break;
                if (!client.IsReady) continue;

                var pingSeq = client.NextPingSeq();
                await SendPing(client, pingSeq);

                // Judged against the pong the client owes us from MaxMissedPongs pings ago, so a
                // client that is merely busy gets several intervals to catch up.
                if (client.LastPongSeq <= pingSeq - MaxMissedPongs)
                {
                    await client.CloseQuietly(WebSocketCloseStatus.PolicyViolation, "Heartbeat timeout");
                    break;
                }
            }
        }
        catch (WebSocketException) { }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        finally
        {
            // Safe to call after Handle's finally has already aborted: Abort is idempotent.
            client.Abort();
        }
    }

    /// <summary>
    /// Waits for the heartbeat to finish, reporting rather than rethrowing whatever it failed
    /// with. The caller drains it from the same finally block that deregisters and disposes the
    /// connection, so an exception escaping here would skip both and strand the connection in the
    /// registry for the life of the process — the leak <see cref="GameServer.ClientCount"/> exists
    /// to detect. The heartbeat's failure is worth reporting; it is never worth the cleanup that
    /// follows it.
    /// </summary>
    private static async Task DrainHeartbeat(Task heartbeat)
    {
        try
        {
            await heartbeat;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Transport] Heartbeat loop faulted: {ex.Message}");
        }
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
