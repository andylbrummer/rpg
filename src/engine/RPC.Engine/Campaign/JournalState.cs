namespace RPC.Engine;

public class JournalState
{
    public List<string> DiscoveryOrder { get; } = new();
    public HashSet<string> Discovered { get; } = new();

    public void Discover(string id)
    {
        if (Discovered.Add(id))
            DiscoveryOrder.Add(id);
    }

    public bool IsDiscovered(string id) => Discovered.Contains(id);
}
