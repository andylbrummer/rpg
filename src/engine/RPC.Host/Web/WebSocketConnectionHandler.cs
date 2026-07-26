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
        var client = new ClientConnection(socket);
        _registry.Add(client);

        // Ties the heartbeat to this connection's lifetime as well as to server shutdown, so the
        // receive loop exiting stops the heartbeat immediately instead of leaving it to notice a
        // disposed socket up to one ping interval later.
        using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
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
            heartbeat = Task.Run(() => RunHeartbeatLoop(client, connectionCts));

            var buffer = new byte[4096];
            while (socket.State == WebSocketState.Open && !connectionCts.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                var oversized = false;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), connectionCts.Token);
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
                    await CloseQuietly(socket, WebSocketCloseStatus.MessageTooBig, "Message exceeds size limit");
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
            connectionCts.Cancel();
            await heartbeat;
            _registry.Remove(client);
            client.Dispose();
        }
    }

    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PongTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PongPollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Pings the client until it stops answering, then cancels the connection.
    /// <para>
    /// Cancelling is the point: a client that fails its heartbeat has usually gone silent at the
    /// TCP level rather than closed, so the receive loop's pending ReceiveAsync would never
    /// return on its own. Deciding the client is gone and not unblocking the reader left the
    /// connection registered — and the session leaked — for the life of the process.
    /// </para>
    /// </summary>
    private async Task RunHeartbeatLoop(ClientConnection client, CancellationTokenSource connectionCts)
    {
        var token = connectionCts.Token;
        try
        {
            while (client.Socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                await Task.Delay(PingInterval, token);
                if (client.Socket.State != WebSocketState.Open) break;
                if (!client.IsReady) continue;

                var pingSeq = client.NextPingSeq();
                await SendPing(client, pingSeq);

                if (!await WaitForPong(client, pingSeq, token)) break;
            }
        }
        catch (WebSocketException) { }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        finally
        {
            // Safe to call after Handle's finally has already cancelled: Cancel is idempotent, and
            // Handle awaits this task before the source is disposed.
            try { connectionCts.Cancel(); } catch (ObjectDisposedException) { }
        }
    }

    /// <summary>
    /// Polls for the pong matching <paramref name="pingSeq"/>. Returns false when the client
    /// failed to answer in time and the socket was closed, i.e. the heartbeat loop should stop.
    /// </summary>
    private static async Task<bool> WaitForPong(ClientConnection client, int pingSeq, CancellationToken token)
    {
        using var pongCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        pongCts.CancelAfter(PongTimeout);
        try
        {
            while (client.Socket.State == WebSocketState.Open && !pongCts.Token.IsCancellationRequested)
            {
                if (client.LastPongSeq >= pingSeq) return true;
                await Task.Delay(PongPollInterval, pongCts.Token);
            }
        }
        catch (OperationCanceledException) { }

        if (token.IsCancellationRequested) return false;

        if (client.LastPongSeq < pingSeq && client.Socket.State == WebSocketState.Open)
        {
            await CloseQuietly(client.Socket, WebSocketCloseStatus.PolicyViolation, "Heartbeat timeout");
            return false;
        }

        return true;
    }

    /// <summary>
    /// How long a close is allowed to take before the connection is abandoned. Both paths that
    /// close a socket here are provoked by a client that is already misbehaving, so the close
    /// cannot be allowed to depend on that client cooperating.
    /// </summary>
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Closes a socket best-effort. A peer that has already vanished makes the close handshake
    /// throw, which is not a condition the caller can act on — it is already tearing down.
    /// <para>
    /// Uses CloseOutputAsync, under a timeout: CloseAsync waits for the peer's answering close
    /// frame, and the peer here is by definition unresponsive — a heartbeat timeout means it has
    /// already stopped answering, and a black-holed connection (dropped link, sleeping machine)
    /// never answers at all. That wait had no cancellation, so the heartbeat task never finished,
    /// the receive loop's finally block awaited it forever, and the client was never removed from
    /// the registry. One wedged peer leaked a session for the life of the process.
    /// </para>
    /// </summary>
    private static async Task CloseQuietly(WebSocket socket, WebSocketCloseStatus status, string description)
    {
        using var timeout = new CancellationTokenSource(CloseTimeout);
        try
        {
            await socket.CloseOutputAsync(status, description, timeout.Token);
        }
        catch (WebSocketException) { }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
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
