using System.Net.WebSockets;

namespace RPC.Host.Web;

public class ClientRegistry
{
    private readonly List<ClientConnection> _clients = new();

    public void Add(ClientConnection client)
    {
        lock (_clients)
        {
            _clients.Add(client);
        }
    }

    public void Remove(ClientConnection client)
    {
        lock (_clients)
        {
            _clients.Remove(client);
        }
    }

    public List<ClientConnection> Snapshot()
    {
        lock (_clients)
        {
            return _clients.ToList();
        }
    }

    public int Count
    {
        get
        {
            lock (_clients)
            {
                return _clients.Count;
            }
        }
    }
}
