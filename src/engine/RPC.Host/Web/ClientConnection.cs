using System.Net.WebSockets;

namespace RPC.Host.Web;

public class ClientConnection : IDisposable
{
    public WebSocket Socket { get; }
    public string SessionId { get; }
    public bool IsReady { get; set; }
    public SemaphoreSlim SendLock { get; } = new(1, 1);
    private int _serverSeq = -1;
    private int _pingSeq = -1;
    public int LastPingSeq { get; set; } = -1;
    public DateTime LastPingTime { get; set; } = DateTime.MinValue;
    public int LastPongSeq { get; set; } = -1;

    public ClientConnection(WebSocket socket)
    {
        Socket = socket;
        SessionId = Guid.NewGuid().ToString("N");
    }

    public int NextServerSeq() => Interlocked.Increment(ref _serverSeq);
    public int NextPingSeq() => Interlocked.Increment(ref _pingSeq);

    public void Dispose()
    {
        SendLock.Dispose();
        Socket.Dispose();
    }
}
