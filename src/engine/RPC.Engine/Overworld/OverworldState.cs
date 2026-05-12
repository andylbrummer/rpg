namespace RPC.Engine.Overworld;

public class OverworldState
{
    public string CurrentNodeId { get; set; } = "the_reach";
    public List<OverworldNode> Nodes { get; set; } = new();
    public List<OverworldRoute> Routes { get; set; } = new();
    public int Turns { get; set; } = 0;

    public OverworldState()
    {
        Nodes.Add(new OverworldNode("the_reach", "The Reach", "town"));
        Nodes.Add(new OverworldNode("broken_engine", "Broken Engine", "dungeon_entrance"));
        Routes.Add(new OverworldRoute("the_reach", "broken_engine", 2, 3, "caves"));
    }

    public bool Travel(string targetId)
    {
        if (targetId == CurrentNodeId) return false;
        var route = Routes.FirstOrDefault(r =>
            (r.From == CurrentNodeId && r.To == targetId) ||
            (r.To == CurrentNodeId && r.From == targetId));
        if (route == null) return false;
        CurrentNodeId = targetId;
        return true;
    }
}

public class OverworldNode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }

    public OverworldNode(string id, string name, string type)
    {
        Id = id;
        Name = name;
        Type = type;
    }
}

public class OverworldRoute
{
    public string From { get; set; }
    public string To { get; set; }
    public int Distance { get; set; }
    public int DangerRating { get; set; }
    public string Terrain { get; set; }

    public OverworldRoute(string from, string to, int distance, int dangerRating, string terrain)
    {
        From = from;
        To = to;
        Distance = distance;
        DangerRating = dangerRating;
        Terrain = terrain;
    }
}
