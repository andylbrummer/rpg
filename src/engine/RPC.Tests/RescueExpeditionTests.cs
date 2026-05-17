using RPC.Engine;
using RPC.Engine.Character;

namespace RPC.Tests;

public class RescueExpeditionTests
{
    private static CharacterState MakeChar(string name, string classId)
        => new(Guid.NewGuid(), name, classId, 1, 0,
            new BaseStats(4, 4, 4, 4, 4),
            20, Equipment.Empty,
            Array.Empty<string>(), 0);

    [Fact]
    public void StartRescueExpedition_RequiresIronman()
    {
        var state = new GameState();
        state.IsIronman = false;
        state.Party.Bench.Add(MakeChar("A", "bonewarden"));
        state.Party.Bench.Add(MakeChar("B", "hollow"));
        state.Party.Bench.Add(MakeChar("C", "stillblade"));

        var result = state.StartRescueExpedition();
        Assert.False(result);
    }

    [Fact]
    public void StartRescueExpedition_RequiresThreeBench()
    {
        var state = new GameState();
        state.IsIronman = true;
        state.Party.Bench.Add(MakeChar("A", "bonewarden"));
        state.Party.Bench.Add(MakeChar("B", "hollow"));

        var result = state.StartRescueExpedition();
        Assert.False(result);
    }

    [Fact]
    public void StartRescueExpedition_SetsRescueState()
    {
        var state = new GameState();
        state.IsIronman = true;
        state.CurrentDungeonType = "broken_engine";
        state.Player = new RPC.Engine.Models.Dungeons.Player(new(3, 3), RPC.Engine.Models.Dungeons.Direction.North);

        var a = MakeChar("A", "bonewarden");
        var b = MakeChar("B", "hollow");
        var c = MakeChar("C", "stillblade");
        state.Party.Bench.Add(a);
        state.Party.Bench.Add(b);
        state.Party.Bench.Add(c);

        var result = state.StartRescueExpedition();
        Assert.True(result);
        Assert.NotNull(state.RescueExpedition);
        Assert.True(state.RescueExpedition.IsActive);
        Assert.Equal("broken_engine", state.RescueExpedition.DungeonType);
        Assert.Equal(3, state.RescueExpedition.RescuePartyIds.Length);
        Assert.Empty(state.Party.Bench);
        Assert.Equal(a.Id, state.Party.Members[0].Id);
        Assert.Equal(b.Id, state.Party.Members[1].Id);
        Assert.Equal(c.Id, state.Party.Members[2].Id);
    }

    [Fact]
    public void ResolveRescueExpedition_Success_RecoversEquipment()
    {
        var state = new GameState();
        state.IsIronman = true;
        state.CurrentDungeonType = "broken_engine";
        state.Player = new RPC.Engine.Models.Dungeons.Player(new(3, 3), RPC.Engine.Models.Dungeons.Direction.North);
        for (int i = 0; i < 3; i++)
            state.Party.Bench.Add(MakeChar($"R{i}", "bonewarden"));
        state.StartRescueExpedition();

        state.ResolveRescueExpedition(success: true);
        Assert.NotNull(state.RescueExpedition);
        Assert.True(state.RescueExpedition.Resolved);
        Assert.True(state.RescueExpedition.Success);
        Assert.False(state.RescueExpedition.IsActive);
    }

    [Fact]
    public void ResolveRescueExpedition_Failure_DeletesSave()
    {
        var tempSave = Path.Combine(Path.GetTempPath(), $"rpc_rescue_test_{Guid.NewGuid()}.json");
        File.WriteAllText(tempSave, "{}");

        var state = new GameState();
        state.IsIronman = true;
        state.SavePath = tempSave;
        state.CurrentDungeonType = "broken_engine";
        state.Player = new RPC.Engine.Models.Dungeons.Player(new(3, 3), RPC.Engine.Models.Dungeons.Direction.North);
        for (int i = 0; i < 3; i++)
            state.Party.Bench.Add(MakeChar($"R{i}", "bonewarden"));
        state.StartRescueExpedition();

        state.ResolveRescueExpedition(success: false);
        Assert.False(File.Exists(tempSave), "Ironman save should be deleted on rescue failure");
    }

    [Fact]
    public void IsFragileState_OnlyWhenIronmanAndLowBenchAndLateTurns()
    {
        var state = new GameState();
        Assert.False(state.IsFragileState); // Not ironman

        state.IsIronman = true;
        Assert.False(state.IsFragileState); // Turns <= 25

        state.Overworld.Turns = 30;
        Assert.True(state.IsFragileState); // Empty bench (0 < 3) + late turns = fragile

        state.Party.Bench.Add(MakeChar("A", "bonewarden"));
        state.Party.Bench.Add(MakeChar("B", "hollow"));
        state.Party.Bench.Add(MakeChar("C", "stillblade"));
        Assert.False(state.IsFragileState); // Now bench == 3
    }
}
