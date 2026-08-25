namespace RPC.Engine;

public class WorldState
{
    public Dictionary<string, string> Settlements { get; set; } = new();
    public List<string> AccessibleDungeons { get; set; } = new();
    public Dictionary<string, List<string>> FactionTerritory { get; set; } = new();

    public void Reset()
    {
        Settlements.Clear();
        AccessibleDungeons.Clear();
        FactionTerritory.Clear();
    }
}
