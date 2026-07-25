using System.Net.WebSockets;

namespace RPC.Host.Web;

public class ClientConnection : IDisposable
{
    public WebSocket Socket { get; }
    public string SessionId { get; }

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

    public ClientConnection(WebSocket socket)
    {
        Socket = socket;
        SessionId = Guid.NewGuid().ToString("N");
    }

    public int NextServerSeq() => Interlocked.Increment(ref _serverSeq);
    public int NextPingSeq() => Interlocked.Increment(ref _pingSeq);

    public void Dispose()
    {
        Socket.Dispose();
    }
}
