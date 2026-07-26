using System.Net.WebSockets;

namespace RPC.Host.Web;

/// <summary>
/// One client's socket, protocol sequence counters, and lifetime. The connection owns its own
/// cancellation source — linked to server shutdown — so any collaborator holding the connection
/// (receive loop, heartbeat, broadcast) can both observe that it is finished and declare it
/// finished, without those collaborators having to pass a token around between themselves.
/// </summary>
public class ClientConnection : IDisposable
{
    /// <summary>
    /// Upper bound on a single send. A peer whose receive window is full leaves SendAsync parked
    /// with the socket still Open, so nothing else in the transport notices; only a deadline
    /// does. Sized well above any healthy send (state snapshots go out over loopback in
    /// microseconds) and above the heartbeat's own ping/pong window, so a send only expires when
    /// the peer has genuinely stopped draining.
    /// </summary>
    public static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(5);

    public WebSocket Socket { get; }
    public string SessionId { get; }

    /// <summary>
    /// Cancelled when this connection is finished — by server shutdown, by the receive loop
    /// unwinding, or by a send that gave up on an undrained peer.
    /// <para>
    /// Deliberately never disposed, for the same reason the send lock is not: the heartbeat and
    /// in-flight broadcasts still read Token as they unwind, and disposing it here would turn an
    /// orderly teardown into ObjectDisposedException on those threads. It registers no timer and
    /// no unmanaged resource, so collection with the connection is sufficient.
    /// </para>
    /// </summary>
    private readonly CancellationTokenSource _connectionCts;

    /// <summary>
    /// Mutable per-connection flags are read by the heartbeat and broadcast threads while the
    /// receive loop writes them, so every cross-thread field is volatile-backed rather than a
    /// plain auto-property.
    /// </summary>
    private volatile bool _isReady;
    private int _lastPongSeq = -1;
    private int _serverSeq = -1;
    private int _pingSeq = -1;

    public bool IsReady
    {
        get => _isReady;
        set => _isReady = value;
    }

    public int LastPongSeq
    {
        get => Volatile.Read(ref _lastPongSeq);
        set => Volatile.Write(ref _lastPongSeq, value);
    }

    /// <summary>
    /// Serializes concurrent sends on this socket. Deliberately never disposed: the heartbeat
    /// loop and state broadcasts can still be awaiting it when the receive loop tears the
    /// connection down, and disposing it there would surface as ObjectDisposedException on
    /// those threads. A SemaphoreSlim whose AvailableWaitHandle is never touched holds no
    /// unmanaged resource, so letting it be collected with the connection is sufficient.
    /// </summary>
    public SemaphoreSlim SendLock { get; } = new(1, 1);

    public ClientConnection(WebSocket socket, CancellationToken serverShutdown)
    {
        Socket = socket;
        SessionId = Guid.NewGuid().ToString("N");
        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(serverShutdown);
    }

    /// <summary>Cancelled once this connection is finished; see <see cref="Abort"/>.</summary>
    public CancellationToken Token => _connectionCts.Token;

    /// <summary>
    /// Declares the connection finished, unblocking everything parked on it — the receive loop's
    /// ReceiveAsync, the heartbeat's delay, and any in-flight send. Idempotent, and safe to call
    /// from any of those threads.
    /// </summary>
    public void Abort()
    {
        try { _connectionCts.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    public int NextServerSeq() => Interlocked.Increment(ref _serverSeq);
    public int NextPingSeq() => Interlocked.Increment(ref _pingSeq);

    /// <summary>
    /// How long teardown will spend closing this socket before abandoning the attempt. Both paths
    /// that close a connection are provoked by a client that is already misbehaving, so the close
    /// cannot be allowed to depend on that client cooperating.
    /// </summary>
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Closes this socket best-effort, serialized against the connection's other senders.
    /// <para>
    /// The close frame is a write like every other, so it has to hold <see cref="SendLock"/>: a
    /// WebSocket permits only one send at a time, and a close issued while a broadcast or a ping
    /// is mid-flight throws <see cref="InvalidOperationException"/> out of whichever of the two
    /// loses the race. Losing it in the heartbeat was the damaging case — that exception is not
    /// one the heartbeat loop expects, so it faulted the heartbeat task, and the receive loop's
    /// teardown awaited that task before deregistering the client. The connection then stayed in
    /// the registry for the life of the process, which is exactly the leak
    /// <c>GameServer.ClientCount</c> exists to detect.
    /// </para>
    /// <para>
    /// Acquiring the lock is bounded by the same deadline as the close itself, and giving up on it
    /// simply skips the close frame. Teardown must never be gated on a lock held by a send that is
    /// itself waiting on an unresponsive peer; the caller aborts the connection regardless, which
    /// drops the socket without the courtesy frame.
    /// </para>
    /// <para>
    /// Uses CloseOutputAsync rather than CloseAsync because the latter waits for the peer's
    /// answering close frame, and the peer here is by definition unresponsive — a heartbeat
    /// timeout means it has already stopped answering, and a black-holed connection (dropped link,
    /// sleeping machine) never answers at all.
    /// </para>
    /// </summary>
    public async Task CloseQuietly(WebSocketCloseStatus status, string description)
    {
        using var timeout = new CancellationTokenSource(CloseTimeout);

        try
        {
            await SendLock.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            await Socket.CloseOutputAsync(status, description, timeout.Token);
        }
        catch (WebSocketException) { }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
        catch (OperationCanceledException) { }
        finally
        {
            SendLock.Release();
        }
    }

    public void Dispose()
    {
        Socket.Dispose();
    }
}
