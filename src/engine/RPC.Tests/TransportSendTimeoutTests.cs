using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using RPC.Engine.Protocol;
using RPC.Host.Web;

namespace RPC.Tests;

/// <summary>
/// Pins the send path against a peer that stops draining its socket.
///
/// A client that goes silent but keeps its TCP window open is already handled — the heartbeat
/// times it out. The harder case is a peer whose receive window is full: the socket is still
/// Open, so nothing looks wrong, but SendAsync blocks until the peer reads. Bound only to the
/// server-wide shutdown token, that block lasts for the life of the process, which:
///
///  - wedges the heartbeat mid-ping, so the receive loop's finally never finishes awaiting it
///    and the connection is never deregistered (a session leak),
///  - holds the connection's send lock, so every later send to it queues behind the wedged one,
///  - and stalls the *other* players, because a broadcast is awaited by the acting client's
///    receive loop before it handles that client's next action.
///
/// So a send must be bounded and must tear its own connection down when it expires.
/// </summary>
public class TransportSendTimeoutTests
{
    /// <summary>
    /// The send deadline these tests run their wedged connections at. They exist to prove a send
    /// expires at all, not to measure the production five seconds — waiting those out three times
    /// made them among the slowest tests in the suite for no added confidence.
    /// </summary>
    private static readonly TimeSpan FastSendTimeout = TimeSpan.FromMilliseconds(200);

    private static StateBroadcaster CreateBroadcaster()
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
        return new StateBroadcaster(
            new ClientRegistry(),
            jsonOptions,
            new CancellationTokenSource());
    }

    private static ProtocolEnvelope Envelope(ClientConnection client) => new()
    {
        V = 2,
        Type = "state",
        Seq = client.NextServerSeq(),
        Payload = new { }
    };

    [Fact]
    public async Task Send_To_A_Wedged_Peer_Gives_Up_Instead_Of_Blocking_Forever()
    {
        var broadcaster = CreateBroadcaster();
        using var socket = new WedgedWebSocket();
        var client = new ClientConnection(socket, CancellationToken.None, FastSendTimeout);

        var send = broadcaster.SendEnvelope(client, Envelope(client));

        var finished = await Task.WhenAny(send, Task.Delay(FastSendTimeout * 8));
        Assert.True(ReferenceEquals(finished, send), "SendEnvelope never returned; the send is unbounded.");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);
    }

    [Fact]
    public async Task Send_Timeout_Aborts_The_Connection_So_Its_Receive_Loop_Unwinds()
    {
        var broadcaster = CreateBroadcaster();
        using var socket = new WedgedWebSocket();
        var client = new ClientConnection(socket, CancellationToken.None, FastSendTimeout);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => broadcaster.SendEnvelope(client, Envelope(client)));

        Assert.True(
            client.Token.IsCancellationRequested,
            "A send that timed out left the connection live; the receive loop would stay parked on ReceiveAsync.");
    }

    /// <summary>
    /// A wedged send must not leave the send lock held: the connection is being torn down, and a
    /// permanently held lock would park every teardown-path send behind it.
    /// </summary>
    [Fact]
    public async Task Send_Timeout_Releases_The_Send_Lock()
    {
        var broadcaster = CreateBroadcaster();
        using var socket = new WedgedWebSocket();
        var client = new ClientConnection(socket, CancellationToken.None, FastSendTimeout);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => broadcaster.SendEnvelope(client, Envelope(client)));

        Assert.Equal(1, client.SendLock.CurrentCount);
    }

    /// <summary>
    /// Closing the connection has to unblock an in-flight send immediately rather than leaving it
    /// to expire on its own timeout — that is what lets the receive loop's teardown finish
    /// promptly when the peer drops.
    /// </summary>
    [Fact]
    public async Task Aborting_The_Connection_Cancels_An_In_Flight_Send()
    {
        var broadcaster = CreateBroadcaster();
        using var socket = new WedgedWebSocket();
        // The production deadline on purpose: this test is about Abort unblocking the send, and a
        // short deadline would expire it on its own and let the test pass without Abort doing
        // anything. Abort happens immediately, so the long deadline costs nothing.
        var client = new ClientConnection(socket, CancellationToken.None);

        var send = broadcaster.SendEnvelope(client, Envelope(client));
        await socket.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        client.Abort();

        var finished = await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.True(ReferenceEquals(finished, send), "Abort did not unblock the in-flight send.");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);
    }

    /// <summary>
    /// A socket that never drains: Open forever, and every send parks until cancelled. Models a
    /// peer with a full receive window rather than one that has closed.
    /// </summary>
    private sealed class WedgedWebSocket : WebSocket
    {
        public TaskCompletionSource SendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override WebSocketState State => WebSocketState.Open;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            SendStarted.TrySetResult();
            return Task.Delay(Timeout.Infinite, cancellationToken);
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            => Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith(_ => new WebSocketReceiveResult(0, WebSocketMessageType.Close, true), TaskScheduler.Default);

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            => Task.Delay(Timeout.Infinite, cancellationToken);

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            => Task.Delay(Timeout.Infinite, cancellationToken);

        public override void Abort() { }
        public override void Dispose() { }
    }
}
