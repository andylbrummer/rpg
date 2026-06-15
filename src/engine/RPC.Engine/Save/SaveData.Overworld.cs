namespace RPC.Engine.Save;

public class SaveOverworldNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string[] FactionPresence { get; set; } = Array.Empty<string>();
    public string? DungeonTemplateId { get; set; }
}

public class SaveOverworldRoute
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public int Distance { get; set; }
    public int DangerRating { get; set; }
    public string Terrain { get; set; } = "";
    public string Status { get; set; } = "";
}

public class SaveWorldState
{
    public Dictionary<string, string> Settlements { get; set; } = new();
    public string[] AccessibleDungeons { get; set; } = Array.Empty<string>();
    public Dictionary<string, string[]> FactionTerritory { get; set; } = new();
}
