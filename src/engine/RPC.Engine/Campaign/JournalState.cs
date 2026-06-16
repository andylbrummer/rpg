namespace RPC.Engine;

public class JournalState
{
    public List<string> DiscoveryOrder { get; } = new();
    public HashSet<string> Discovered { get; } = new();

    // Detected-but-unrevealed secrets: the party knows something is there (automap "?") but not its
    // nature. Set by the Inkblood Cartographer passive; promoted to Discovered by explicit search.
    // Transient marker — re-derived every movement step by the passive — so it is not persisted.
    public List<string> DetectionOrder { get; } = new();
    public HashSet<string> Detected { get; } = new();

    public void Discover(string id)
    {
        if (Discovered.Add(id))
            DiscoveryOrder.Add(id);
    }

    public bool IsDiscovered(string id) => Discovered.Contains(id);

    public void Detect(string id)
    {
        if (Detected.Add(id))
            DetectionOrder.Add(id);
    }

    public bool IsDetected(string id) => Detected.Contains(id);
}
