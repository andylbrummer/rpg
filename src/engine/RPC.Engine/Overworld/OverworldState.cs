using RPC.Engine.Campaign;
using RPC.Engine.Combat;

namespace RPC.Engine.Overworld;

public enum NodeType
{
    Town,
    Dungeon,
    Pass
}

public enum RouteStatus
{
    Open,
    Contested,
    Blocked,
    BloomAffected
}

public record OverworldNode(string Id, string Name, NodeType Type)
{
    public List<string> FactionPresence { get; set; } = new();
    public string? DungeonTemplateId { get; set; }
}

public record OverworldRoute(string From, string To, int Distance, int DangerRating, string Terrain)
{
    public RouteStatus Status { get; set; } = RouteStatus.Open;
}

public class OverworldState
{
    public string CurrentNodeId { get; set; } = "the_reach";
    public Dictionary<string, OverworldNode> Nodes { get; set; } = new();
    public List<OverworldRoute> Routes { get; set; } = new();
    public int Turns { get; set; } = 0;

    public OverworldState()
    {
        Nodes["the_reach"] = new OverworldNode("the_reach", "The Reach", NodeType.Town);
        Nodes["broken_engine"] = new OverworldNode("broken_engine", "Broken Engine", NodeType.Dungeon)
        {
            DungeonTemplateId = "broken_engine"
        };
        Routes.Add(new OverworldRoute("the_reach", "broken_engine", 2, 3, "caves"));
    }

    public bool Travel(string targetId)
    {
        if (targetId == CurrentNodeId) return false;
        var route = GetRoute(CurrentNodeId, targetId);
        if (route == null) return false;
        CurrentNodeId = targetId;
        return true;
    }

    public List<OverworldRoute> GetAvailableRoutes(string nodeId)
    {
        return Routes
            .Where(r => (r.From == nodeId || r.To == nodeId) && r.Status != RouteStatus.Blocked)
            .ToList();
    }

    public OverworldRoute? GetRoute(string from, string to)
    {
        return Routes.FirstOrDefault(r =>
            (r.From == from && r.To == to) ||
            (r.To == from && r.From == to));
    }

    public void GenerateFromConfig(CampaignConfig config, GameRandom rng)
    {
        Nodes.Clear();
        Routes.Clear();

        var nodeIds = BuildNodes(config, rng);
        BuildRoutes(nodeIds, rng, config);
        ApplyComplication(config, rng);
    }

    private List<string> BuildNodes(CampaignConfig config, GameRandom rng)
    {
        AddCoreNodes(rng);
        AssignFactions(config, rng);
        return Nodes.Keys.ToList();
    }

    private void AddCoreNodes(GameRandom rng)
    {
        Nodes["the_reach"] = new OverworldNode("the_reach", "The Reach", NodeType.Town);

        var townPool = new[] { ("ashford", "Ashford"), ("velm", "Velm"), ("crucible", "Crucible") };
        int extraTowns = rng.Roll(1, 3);
        foreach (var (id, name) in Shuffle(townPool, rng).Take(extraTowns))
        {
            Nodes[id] = new OverworldNode(id, name, NodeType.Town);
        }

        Nodes["broken_engine"] = new OverworldNode("broken_engine", "Broken Engine", NodeType.Dungeon)
        {
            DungeonTemplateId = "broken_engine"
        };

        var dungeonPool = new[] { ("crypt", "Crypt of Whispers", "crypt"), ("sewers", "Sewer Warrens", "sewers") };
        int extraDungeons = rng.Roll(1, 2);
        foreach (var (id, name, template) in Shuffle(dungeonPool, rng).Take(extraDungeons))
        {
            Nodes[id] = new OverworldNode(id, name, NodeType.Dungeon) { DungeonTemplateId = template };
        }

        var passPool = new[] { ("dead_pass", "Dead Pass"), ("ruin_of_velm", "Ruin of Velm"), ("cinder_waste", "Cinder Waste") };
        int passes = rng.Roll(1, 2);
        foreach (var (id, name) in Shuffle(passPool, rng).Take(passes))
        {
            Nodes[id] = new OverworldNode(id, name, NodeType.Pass);
        }
    }

    private void AssignFactions(CampaignConfig config, GameRandom rng)
    {
        var factions = new[] { config.Patron, config.Threat, config.Mastermind, config.WildCard }
            .Where(f => !string.IsNullOrEmpty(f))
            .Distinct()
            .ToArray();

        foreach (var node in Nodes.Values)
        {
            if (node.Type == NodeType.Town && factions.Length > 0)
            {
                int count = Math.Min(rng.Roll(1, 2), factions.Length);
                node.FactionPresence = Shuffle(factions, rng).Take(count).ToList();
            }
            else if (node.Type == NodeType.Pass && factions.Length > 0 && rng.Roll(0, 1) == 1)
            {
                node.FactionPresence = new List<string> { factions[rng.Next(factions.Length)] };
            }
        }
    }

    private void BuildRoutes(List<string> nodeIds, GameRandom rng, CampaignConfig config)
    {
        var terrainPool = new[] { "plains", "forest", "mountain", "caves", "marsh" };

        for (int i = 0; i < nodeIds.Count - 1; i++)
        {
            AddRoute(nodeIds[i], nodeIds[i + 1], rng, terrainPool, config);
        }

        int targetRoutes = Math.Min(8, Math.Max(4, rng.Roll(4, 8)));
        int attempts = 0;
        while (Routes.Count < targetRoutes && attempts < 50)
        {
            attempts++;
            var a = nodeIds[rng.Next(nodeIds.Count)];
            var b = nodeIds[rng.Next(nodeIds.Count)];
            if (a != b && GetRoute(a, b) == null)
            {
                AddRoute(a, b, rng, terrainPool, config);
            }
        }
    }

    private void AddRoute(string from, string to, GameRandom rng, string[] terrainPool, CampaignConfig config)
    {
        var terrain = terrainPool[rng.Next(terrainPool.Length)];
        var distance = rng.Roll(1, 4);
        var danger = rng.Roll(1, 5);
        Routes.Add(new OverworldRoute(from, to, distance, danger, terrain));
    }

    private void ApplyComplication(CampaignConfig config, GameRandom rng)
    {
        if (Routes.Count == 0) return;

        switch (config.Complication)
        {
            case ComplicationType.ClosingPasses:
                BlockRandomRoute(rng);
                break;
            case ComplicationType.OpenWar:
                ContestRandomRoutes(rng);
                break;
            case ComplicationType.BloomSiege:
                BloomAffectRandomRoute(rng);
                break;
        }
    }

    private void BlockRandomRoute(GameRandom rng)
    {
        var idx = rng.Next(Routes.Count);
        Routes[idx].Status = RouteStatus.Blocked;
    }

    private void ContestRandomRoutes(GameRandom rng)
    {
        int count = Math.Min(rng.Roll(1, 2), Routes.Count);
        for (int i = 0; i < count; i++)
        {
            var idx = rng.Next(Routes.Count);
            Routes[idx].Status = RouteStatus.Contested;
        }
    }

    private void BloomAffectRandomRoute(GameRandom rng)
    {
        var idx = rng.Next(Routes.Count);
        Routes[idx].Status = RouteStatus.BloomAffected;
    }

    private static T[] Shuffle<T>(T[] items, GameRandom rng)
    {
        var result = items.ToArray();
        for (int i = result.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }
        return result;
    }
}
