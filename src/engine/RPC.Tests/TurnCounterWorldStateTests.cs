using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Overworld;

namespace RPC.Tests;

public class TurnCounterWorldStateTests : IDisposable
{
    private readonly string _testSavePath;

    public TurnCounterWorldStateTests()
    {
        _testSavePath = Path.Combine(Path.GetTempPath(), $"test_tws_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testSavePath))
            File.Delete(_testSavePath);
        if (File.Exists(_testSavePath + ".tmp"))
            File.Delete(_testSavePath + ".tmp");
    }

    private static Dungeon CreateTestDungeon()
    {
        var dungeon = new Dungeon(3, 3, "test");
        dungeon.Tiles[1, 1] = new Tile(TileType.Floor);
        return dungeon;
    }

    private static GameState CreateGameStateWithTimeline(int turn, string factionId, int preparing, int executing)
    {
        var gs = new GameState(seed: 42);
        gs.CampaignConfig = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            WildCard = "stillness",
            Scheme = SchemeType.BloomHarvest,
            Complication = ComplicationType.BloomSiege,
            EvidenceChain = Enumerable.Repeat("test", 10).ToList(),
            NpcCasting = new Dictionary<string, string> { { "npc1", "role1" } },
            FactionTimelines = new Dictionary<string, FactionTimeline>
            {
                { factionId, new FactionTimeline(preparing, executing) }
            }
        };
        gs.Overworld.Turns = turn;
        return gs;
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(15, 1)]
    [InlineData(16, 2)]
    [InlineData(25, 2)]
    [InlineData(26, 3)]
    [InlineData(35, 3)]
    public void CurrentAct_DerivedFromTurns(int turn, int expectedAct)
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Turns = turn;

        Assert.Equal(expectedAct, gs.CurrentAct);
    }

    [Fact]
    public void FactionState_Investigating_BeforePreparing()
    {
        var gs = CreateGameStateWithTimeline(5, "bureau", 10, 20);

        Assert.Equal(FactionState.Investigating, gs.GetFactionState("bureau"));
    }

    [Fact]
    public void FactionState_Preparing_AtPreparingTurn()
    {
        var gs = CreateGameStateWithTimeline(10, "bureau", 10, 20);

        Assert.Equal(FactionState.Preparing, gs.GetFactionState("bureau"));
    }

    [Fact]
    public void FactionState_Executing_AtExecutingTurn()
    {
        var gs = CreateGameStateWithTimeline(20, "bureau", 10, 20);

        Assert.Equal(FactionState.Executing, gs.GetFactionState("bureau"));
    }

    [Fact]
    public void FactionState_UnknownFaction_ReturnsInvestigating()
    {
        var gs = new GameState(seed: 42);
        gs.CampaignConfig = new CampaignConfig();

        Assert.Equal(FactionState.Investigating, gs.GetFactionState("unknown"));
    }

    [Fact]
    public void FactionState_NullConfig_ReturnsInvestigating()
    {
        var gs = new GameState(seed: 42);

        Assert.Equal(FactionState.Investigating, gs.GetFactionState("bureau"));
    }

    [Fact]
    public void IncrementTurns_EmitsFactionProgression_AtMilestone12()
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Turns = 10;
        gs.Overworld.Routes.Clear();
        gs.Overworld.Routes.Add(new OverworldRoute("the_reach", "broken_engine", 3, 1, "caves"));

        gs.Travel("broken_engine");

        Assert.Equal(13, gs.Overworld.Turns);
        var progression = gs.ActionLog.LastOrDefault(e => e.Type == "faction_progression" && e.Payload.GetValueOrDefault("milestone") == "12");
        Assert.NotNull(progression);
    }

    [Fact]
    public void IncrementTurns_EmitsFactionProgression_AtMilestone22()
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Turns = 20;
        gs.Overworld.Routes.Clear();
        gs.Overworld.Routes.Add(new OverworldRoute("the_reach", "broken_engine", 3, 1, "caves"));

        gs.Travel("broken_engine");

        Assert.Equal(23, gs.Overworld.Turns);
        var progression = gs.ActionLog.LastOrDefault(e => e.Type == "faction_progression" && e.Payload.GetValueOrDefault("milestone") == "22");
        Assert.NotNull(progression);
    }

    [Fact]
    public void IncrementTurns_NoDoubleEmit_WhenCrossingBothMilestones()
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Turns = 10;
        gs.Overworld.Routes.Clear();
        gs.Overworld.Routes.Add(new OverworldRoute("the_reach", "broken_engine", 15, 1, "caves"));

        gs.Travel("broken_engine");

        Assert.Equal(25, gs.Overworld.Turns);
        var m12 = gs.ActionLog.Where(e => e.Type == "faction_progression" && e.Payload.GetValueOrDefault("milestone") == "12").ToList();
        var m22 = gs.ActionLog.Where(e => e.Type == "faction_progression" && e.Payload.GetValueOrDefault("milestone") == "22").ToList();
        Assert.Single(m12);
        Assert.Single(m22);
    }

    [Fact]
    public void Campaign_DoesNotEnd_AtTurn15()
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Turns = 14;
        var dungeon = CreateTestDungeon();

        gs.EnterDungeon(dungeon, "test");

        Assert.Equal(15, gs.Overworld.Turns);
        Assert.False(gs.CampaignEnded);
    }

    [Fact]
    public void Campaign_DoesNotEnd_AtTurn25()
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Turns = 24;
        var dungeon = CreateTestDungeon();

        gs.EnterDungeon(dungeon, "test");

        Assert.Equal(25, gs.Overworld.Turns);
        Assert.False(gs.CampaignEnded);
    }

    [Fact]
    public void ChooseSettlementFate_UpdatesWorldState()
    {
        var gs = new GameState(seed: 42);

        gs.ChooseSettlementFate("the_reach", "saved");

        Assert.Equal("saved", gs.WorldState.Settlements["the_reach"]);
    }

    [Fact]
    public void GenerateOverworld_PopulatesWorldState()
    {
        var gs = new GameState(seed: 42);
        var config = CampaignConfig.Roll(new GameRandom(42));
        config.EvidenceChain = Enumerable.Repeat("test", 10).ToList();
        config.NpcCasting = new Dictionary<string, string> { { "npc1", "role1" } };

        gs.GenerateOverworld(config);

        Assert.NotEmpty(gs.WorldState.AccessibleDungeons);
        Assert.All(gs.WorldState.AccessibleDungeons, id =>
            Assert.Equal(NodeType.Dungeon, gs.Overworld.Nodes[id].Type));
        Assert.NotEmpty(gs.WorldState.Settlements);
        Assert.All(gs.WorldState.Settlements.Values, status => Assert.Equal("pending", status));
    }

    [Fact]
    public void GenerateOverworld_PopulatesFactionTerritory()
    {
        var gs = new GameState(seed: 42);
        var config = CampaignConfig.Roll(new GameRandom(42));
        config.EvidenceChain = Enumerable.Repeat("test", 10).ToList();
        config.NpcCasting = new Dictionary<string, string> { { "npc1", "role1" } };

        gs.GenerateOverworld(config);

        var totalTerritoryNodes = gs.WorldState.FactionTerritory.Values.Sum(v => v.Count);
        var nodesWithPresence = gs.Overworld.Nodes.Values.Sum(n => n.FactionPresence.Count);
        Assert.Equal(nodesWithPresence, totalTerritoryNodes);
    }

    [Fact]
    public void Reset_ClearsWorldState()
    {
        var gs = new GameState(seed: 42);
        gs.ChooseSettlementFate("the_reach", "saved");

        gs.Reset();

        Assert.Empty(gs.WorldState.Settlements);
        Assert.Empty(gs.WorldState.AccessibleDungeons);
        Assert.Empty(gs.WorldState.FactionTerritory);
    }

    [Fact]
    public void SaveLoad_RoundTrip_PreservesCurrentAct()
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Turns = 20;

        gs.SaveGame(_testSavePath);

        var gs2 = new GameState(seed: 99);
        var loaded = gs2.LoadGame(_testSavePath);

        Assert.True(loaded);
        Assert.Equal(2, gs2.CurrentAct);
    }

    [Fact]
    public void SaveLoad_RoundTrip_PreservesWorldState()
    {
        var gs = new GameState(seed: 42);
        gs.ChooseSettlementFate("the_reach", "saved");
        gs.WorldState.AccessibleDungeons = ["broken_engine", "crypt"];
        gs.WorldState.FactionTerritory["bureau"] = ["the_reach", "ashford"];

        gs.SaveGame(_testSavePath);

        var gs2 = new GameState(seed: 99);
        var loaded = gs2.LoadGame(_testSavePath);

        Assert.True(loaded);
        Assert.Equal("saved", gs2.WorldState.Settlements["the_reach"]);
        Assert.Contains("broken_engine", gs2.WorldState.AccessibleDungeons);
        Assert.Contains("crypt", gs2.WorldState.AccessibleDungeons);
        Assert.Contains("the_reach", gs2.WorldState.FactionTerritory["bureau"]);
        Assert.Contains("ashford", gs2.WorldState.FactionTerritory["bureau"]);
    }

    [Fact]
    public void SaveLoad_OldSaveWithoutWorldState_DefaultsToEmpty()
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Turns = 5;
        gs.SaveGame(_testSavePath);

        var json = File.ReadAllText(_testSavePath);
        json = json.Replace("\"worldState\"", "\"oldWorldState\"");
        File.WriteAllText(_testSavePath, json);

        var gs2 = new GameState(seed: 99);
        var loaded = gs2.LoadGame(_testSavePath);

        Assert.True(loaded);
        Assert.Empty(gs2.WorldState.Settlements);
        Assert.Empty(gs2.WorldState.AccessibleDungeons);
        Assert.Empty(gs2.WorldState.FactionTerritory);
    }
}
