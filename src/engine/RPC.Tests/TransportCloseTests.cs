using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Content;
using RPC.Engine.Protocol;
using RPC.Host.Web;

namespace RPC.Tests;

/// <summary>
/// Pins how a connection closes its socket.
///
/// A close frame is a write, and a WebSocket permits only one write at a time. The transport has
/// two writers — state broadcasts and the heartbeat's pings, both serialized on the connection's
/// send lock — and teardown used to issue its close outside that lock. The close then raced
/// whatever was in flight, and the loser threw InvalidOperationException. Losing it in the
/// heartbeat was the damaging case: that exception is not one the heartbeat loop expects, so it
/// faulted the heartbeat task, and the receive loop's teardown awaited that task before
/// deregistering the client — so a transient send race turned into a connection that stayed
/// registered for the life of the process.
///
/// The other half of the contract is that closing must not be able to wedge teardown: waiting for
/// the send lock is bounded, because the send holding it may itself be waiting on a peer that
/// stopped reading.
/// </summary>
public class TransportCloseTests
{
    private static StateBroadcaster CreateBroadcaster()
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
        return new StateBroadcaster(
            new ClientRegistry(),
            new StatePresenter(new ClassRegistry(), new ItemRegistry()),
            new GameState(),
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
    public async Task Close_Waits_For_An_In_Flight_Send_Rather_Than_Writing_Over_It()
    {
        var broadcaster = CreateBroadcaster();
        using var socket = new RecordingWebSocket();
        var client = new ClientConnection(socket, CancellationToken.None);

        var send = broadcaster.SendEnvelope(client, Envelope(client));
        await socket.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var close = client.CloseQuietly(WebSocketCloseStatus.PolicyViolation, "Heartbeat timeout");

        // The send still holds the lock, so the close cannot have written anything yet no matter
        // how the two tasks are scheduled.
        await Task.Delay(100);
        Assert.False(socket.CloseCalled, "The close frame was written while a send was in flight.");

        socket.ReleaseSend();
        await send;
        await close;

        Assert.True(socket.CloseCalled, "The close frame was never written once the send finished.");
        Assert.False(socket.SawConcurrentWrite, "Two writes overlapped on one socket.");
    }

    /// <summary>
    /// A send parked on a peer that stopped draining holds the send lock until its own timeout.
    /// Teardown cannot wait that out — the close is a courtesy, and the caller aborts the
    /// connection either way — so an unavailable lock must make the close give up, not block.
    /// </summary>
    [Fact]
    public async Task Close_Gives_Up_Rather_Than_Waiting_On_A_Send_That_Never_Finishes()
    {
        var broadcaster = CreateBroadcaster();
        using var socket = new RecordingWebSocket();
        var client = new ClientConnection(socket, CancellationToken.None);

        // Never released: models a peer whose receive window is full.
        var send = broadcaster.SendEnvelope(client, Envelope(client));
        await socket.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var close = client.CloseQuietly(WebSocketCloseStatus.PolicyViolation, "Heartbeat timeout");

        var finished = await Task.WhenAny(close, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.True(ReferenceEquals(finished, close), "Closing blocked on the send lock; teardown would never finish.");
        await close;
        Assert.False(socket.SawConcurrentWrite, "Two writes overlapped on one socket.");

        client.Abort();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);
    }

    /// <summary>
    /// A socket that reports whether two writes were ever in flight at once. Sends park until
    /// released, so a test can hold the send lock open across a close attempt.
    /// </summary>
    private sealed class RecordingWebSocket : WebSocket
    {
        private readonly TaskCompletionSource _sendGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _writersInFlight;
        private volatile bool _sawConcurrentWrite;

        public TaskCompletionSource SendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool SawConcurrentWrite => _sawConcurrentWrite;
        public volatile bool CloseCalled;

        public void ReleaseSend() => _sendGate.TrySetResult();

        public override WebSocketState State => WebSocketState.Open;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;

        public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            EnterWrite();
            try
            {
                SendStarted.TrySetResult();
                await _sendGate.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                ExitWrite();
            }
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            EnterWrite();
            try
            {
                CloseCalled = true;
                return Task.CompletedTask;
            }
            finally
            {
                ExitWrite();
            }
        }

        private void EnterWrite()
        {
            if (Interlocked.Increment(ref _writersInFlight) > 1) _sawConcurrentWrite = true;
        }

        private void ExitWrite() => Interlocked.Decrement(ref _writersInFlight);

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            => Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith(_ => new WebSocketReceiveResult(0, WebSocketMessageType.Close, true), TaskScheduler.Default);

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            => Task.Delay(Timeout.Infinite, cancellationToken);

        public override void Abort() { }
        public override void Dispose() { }
    }
}
