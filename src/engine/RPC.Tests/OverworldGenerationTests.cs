using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Combat;
using RPC.Engine.Overworld;
using RPC.Engine.Save;

namespace RPC.Tests;

public class OverworldGenerationTests : IDisposable
{
    private readonly string _testSavePath;

    public OverworldGenerationTests()
    {
        _testSavePath = Path.Combine(Path.GetTempPath(), $"test_overworld_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testSavePath))
            File.Delete(_testSavePath);
        if (File.Exists(_testSavePath + ".tmp"))
            File.Delete(_testSavePath + ".tmp");
    }

    private static CampaignConfig CreateTestConfig()
    {
        return new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            Scheme = SchemeType.BloomHarvest,
            Complication = ComplicationType.BloomSiege,
            EvidenceChain = Enumerable.Repeat("test", 10).ToList(),
            NpcCasting = new Dictionary<string, string> { { "role1", "npc1" } }
        };
    }

    [Fact]
    public void GenerateFromConfig_CreatesAtLeastTwoTowns()
    {
        var ow = new OverworldState();
        var config = CreateTestConfig();
        var rng = new GameRandom(42);

        ow.GenerateFromConfig(config, rng);

        var towns = ow.Nodes.Values.Count(n => n.Type == NodeType.Town);
        Assert.True(towns >= 2, $"Expected at least 2 towns, got {towns}");
    }

    [Fact]
    public void GenerateFromConfig_CreatesMultipleDungeonEntrances()
    {
        var ow = new OverworldState();
        var config = CreateTestConfig();
        var rng = new GameRandom(42);

        ow.GenerateFromConfig(config, rng);

        var dungeons = ow.Nodes.Values.Count(n => n.Type == NodeType.Dungeon);
        Assert.True(dungeons >= 2, $"Expected at least 2 dungeons, got {dungeons}");
    }

    [Fact]
    public void GenerateFromConfig_CreatesFourToEightRoutes()
    {
        var ow = new OverworldState();
        var config = CreateTestConfig();
        var rng = new GameRandom(42);

        ow.GenerateFromConfig(config, rng);

        Assert.True(ow.Routes.Count >= 4, $"Expected at least 4 routes, got {ow.Routes.Count}");
        Assert.True(ow.Routes.Count <= 8, $"Expected at most 8 routes, got {ow.Routes.Count}");
    }

    [Fact]
    public void GetAvailableRoutes_ExcludesBlocked()
    {
        var ow = new OverworldState();
        ow.Nodes["a"] = new OverworldNode("a", "A", NodeType.Town);
        ow.Nodes["b"] = new OverworldNode("b", "B", NodeType.Town);
        ow.Nodes["c"] = new OverworldNode("c", "C", NodeType.Town);
        ow.Routes.Add(new OverworldRoute("a", "b", 1, 1, "plains") { Status = RouteStatus.Open });
        ow.Routes.Add(new OverworldRoute("a", "c", 2, 2, "forest") { Status = RouteStatus.Blocked });

        var available = ow.GetAvailableRoutes("a");

        Assert.Single(available);
        Assert.Equal("b", available[0].To);
    }

    [Fact]
    public void GetRoute_ReturnsCorrectRoute()
    {
        var ow = new OverworldState();
        ow.Nodes["a"] = new OverworldNode("a", "A", NodeType.Town);
        ow.Nodes["b"] = new OverworldNode("b", "B", NodeType.Town);
        ow.Routes.Add(new OverworldRoute("a", "b", 3, 2, "mountain"));

        var route = ow.GetRoute("a", "b");
        var reverse = ow.GetRoute("b", "a");

        Assert.NotNull(route);
        Assert.Equal(3, route.Distance);
        Assert.NotNull(reverse);
        Assert.Same(route, reverse);
    }

    [Fact]
    public void Travel_UsesRouteDistance()
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Nodes.Clear();
        gs.Overworld.Nodes["the_reach"] = new OverworldNode("the_reach", "The Reach", NodeType.Town);
        gs.Overworld.Nodes["broken_engine"] = new OverworldNode("broken_engine", "Broken Engine", NodeType.Dungeon);
        gs.Overworld.Routes.Clear();
        gs.Overworld.Routes.Add(new OverworldRoute("the_reach", "broken_engine", 3, 1, "caves"));

        gs.Travel("broken_engine");

        Assert.Equal(3, gs.Overworld.Turns);
    }

    [Fact]
    public void SaveRoundTrip_PreservesNodesAndRoutes()
    {
        var gs = new GameState(seed: 42);
        var config = CreateTestConfig();
        gs.GenerateOverworld(config);

        gs.SaveGame(_testSavePath);

        var gs2 = new GameState(seed: 99);
        var loaded = gs2.LoadGame(_testSavePath);

        Assert.True(loaded);
        Assert.Equal(gs.Overworld.Nodes.Count, gs2.Overworld.Nodes.Count);
        Assert.Equal(gs.Overworld.Routes.Count, gs2.Overworld.Routes.Count);

        foreach (var id in gs.Overworld.Nodes.Keys)
        {
            Assert.True(gs2.Overworld.Nodes.ContainsKey(id));
            var original = gs.Overworld.Nodes[id];
            var restored = gs2.Overworld.Nodes[id];
            Assert.Equal(original.Name, restored.Name);
            Assert.Equal(original.Type, restored.Type);
            Assert.Equal(original.DungeonTemplateId, restored.DungeonTemplateId);
            Assert.Equal(original.FactionPresence, restored.FactionPresence);
        }

        foreach (var original in gs.Overworld.Routes)
        {
            var restored = gs2.Overworld.GetRoute(original.From, original.To);
            Assert.NotNull(restored);
            Assert.Equal(original.Distance, restored.Distance);
            Assert.Equal(original.DangerRating, restored.DangerRating);
            Assert.Equal(original.Terrain, restored.Terrain);
            Assert.Equal(original.Status, restored.Status);
        }
    }

    [Fact]
    public void Graph_IsConnected()
    {
        var ow = new OverworldState();
        var config = CreateTestConfig();
        var rng = new GameRandom(42);

        ow.GenerateFromConfig(config, rng);

        foreach (var start in ow.Nodes.Keys)
        {
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var route in ow.Routes.Where(r => r.Status != RouteStatus.Blocked && (r.From == current || r.To == current)))
                {
                    var neighbor = route.From == current ? route.To : route.From;
                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            var unreachable = ow.Nodes.Keys.Except(visited).ToList();
            Assert.Empty(unreachable);
        }
    }
}
