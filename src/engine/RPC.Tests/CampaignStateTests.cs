using RPC.Engine;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Overworld;

namespace RPC.Tests;

public class CampaignStateTests : IDisposable
{
    private readonly string _testSavePath;

    public CampaignStateTests()
    {
        _testSavePath = Path.Combine(Path.GetTempPath(), $"test_campaign_{Guid.NewGuid()}.json");
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

    [Fact]
    public void Travel_IncrementsTurns_ByRouteDistance()
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Routes.Clear();
        gs.Overworld.Routes.Add(new OverworldRoute("the_reach", "broken_engine", 3, 1, "caves"));

        gs.Travel("broken_engine");

        Assert.Equal(3, gs.Overworld.Turns);
    }

    [Fact]
    public void EnterDungeon_IncrementsTurns_ByOne()
    {
        var gs = new GameState(seed: 42);
        var dungeon = CreateTestDungeon();

        gs.EnterDungeon(dungeon, "test");

        Assert.Equal(1, gs.Overworld.Turns);
    }

    [Fact]
    public void ReturnToTown_IncrementsTurns_ByOne()
    {
        var gs = new GameState(seed: 42);
        var dungeon = CreateTestDungeon();
        gs.EnterDungeon(dungeon, "test");
        var turnsAfterEntry = gs.Overworld.Turns;

        gs.ReturnToTown();

        Assert.Equal(turnsAfterEntry + 1, gs.Overworld.Turns);
    }

    [Fact]
    public void DungeonExpedition_TotalIncrement_IsTwo()
    {
        var gs = new GameState(seed: 42);
        var dungeon = CreateTestDungeon();

        gs.EnterDungeon(dungeon, "test");
        gs.ReturnToTown();

        Assert.Equal(2, gs.Overworld.Turns);
    }

    [Fact]
    public void TurnLimit_At15_CampaignEnds()
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Turns = 14;
        var dungeon = CreateTestDungeon();

        gs.EnterDungeon(dungeon, "test");

        Assert.Equal(15, gs.Overworld.Turns);
        Assert.True(gs.CampaignEnded);
    }

    [Fact]
    public void TurnLimit_At15_ForcesReturnToTown()
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Turns = 14;
        var dungeon = CreateTestDungeon();

        gs.EnterDungeon(dungeon, "test");

        Assert.Null(gs.CurrentDungeon);
        Assert.Equal(GameMode.Menu, gs.Mode);
    }

    [Fact]
    public void TurnLimit_Exceeds15_ClampsTo15()
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Turns = 13;
        gs.Overworld.Routes.Clear();
        gs.Overworld.Routes.Add(new OverworldRoute("the_reach", "broken_engine", 5, 1, "caves"));

        gs.Travel("broken_engine");

        Assert.Equal(15, gs.Overworld.Turns);
        Assert.True(gs.CampaignEnded);
    }

    [Fact]
    public void CampaignEnded_BlocksTravel()
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Turns = 15;
        gs.CampaignEnded = true;

        var result = gs.Travel("broken_engine");

        Assert.False(result);
    }

    [Fact]
    public void CampaignEnded_BlocksEnterDungeon()
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Turns = 15;
        gs.CampaignEnded = true;
        var dungeon = CreateTestDungeon();

        gs.EnterDungeon(dungeon, "test");

        Assert.Null(gs.CurrentDungeon);
        Assert.Equal(GameMode.Menu, gs.Mode);
    }

    [Fact]
    public void SaveLoad_RoundTrip_PreservesTurns()
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Turns = 7;
        gs.Overworld.CurrentNodeId = "broken_engine";

        gs.SaveGame(_testSavePath);

        var gs2 = new GameState(seed: 99);
        var loaded = gs2.LoadGame(_testSavePath);

        Assert.True(loaded);
        Assert.Equal(7, gs2.Overworld.Turns);
        Assert.Equal("broken_engine", gs2.Overworld.CurrentNodeId);
    }

    [Fact]
    public void SaveLoad_RoundTrip_PreservesCampaignEnded()
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Turns = 15;
        gs.CampaignEnded = true;

        gs.SaveGame(_testSavePath);

        var gs2 = new GameState(seed: 99);
        var loaded = gs2.LoadGame(_testSavePath);

        Assert.True(loaded);
        Assert.True(gs2.CampaignEnded);
    }

    [Fact]
    public void Reset_ClearsTurnsAndCampaignEnded()
    {
        var gs = new GameState(seed: 42);
        gs.Overworld.Turns = 10;
        gs.CampaignEnded = true;

        gs.Reset();

        Assert.Equal(0, gs.Overworld.Turns);
        Assert.False(gs.CampaignEnded);
    }
}
