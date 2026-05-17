using RPC.Engine;
using RPC.Engine.Commands;
using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Save;

namespace RPC.Tests;

public class IronmanTests : IDisposable
{
    private readonly string _tempSavePath;

    public IronmanTests()
    {
        _tempSavePath = Path.Combine(Path.GetTempPath(), $"test_save_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        try { File.Delete(_tempSavePath); } catch { }
    }

    [Fact]
    public void Ironman_Flag_Saved_And_Loaded()
    {
        var state = new GameState(seed: 42);
        state.IsIronman = true;
        state.SavePath = _tempSavePath;
        SaveSystem.Save(state, _tempSavePath);

        var loadedState = new GameState(seed: 42);
        var result = SaveSystem.Load(loadedState, _tempSavePath);

        Assert.True(result);
        Assert.True(loadedState.IsIronman);
    }

    [Fact]
    public void Ironman_Flag_Defaults_To_False()
    {
        var state = new GameState(seed: 42);
        SaveSystem.Save(state, _tempSavePath);

        var loadedState = new GameState(seed: 42);
        SaveSystem.Load(loadedState, _tempSavePath);

        Assert.False(loadedState.IsIronman);
    }

    [Fact]
    public void AutoSave_On_State_Change_When_Ironman()
    {
        var state = new GameState(seed: 42);
        state.IsIronman = true;
        state.SavePath = _tempSavePath;
        var handler = new GameCommandHandler(state, new StubDungeonGenerator());

        Assert.False(File.Exists(_tempSavePath));

        // Create initial save
        state.SaveGame(state.SavePath);
        Assert.True(File.Exists(_tempSavePath));

        var beforeWrite = File.GetLastWriteTimeUtc(_tempSavePath);

        // Wait a tiny bit to ensure timestamp changes
        Thread.Sleep(50);

        // Perform a state-changing action
        handler.Execute(new TurnLeftCommand());

        var afterWrite = File.GetLastWriteTimeUtc(_tempSavePath);
        Assert.True(afterWrite > beforeWrite, "Ironman should auto-save after state-changing action");
    }

    [Fact]
    public void No_AutoSave_When_Not_Ironman()
    {
        var state = new GameState(seed: 42);
        state.IsIronman = false;
        var handler = new GameCommandHandler(state, new StubDungeonGenerator());

        state.SaveGame(_tempSavePath);
        var beforeWrite = File.GetLastWriteTimeUtc(_tempSavePath);

        Thread.Sleep(50);

        handler.Execute(new TurnLeftCommand());

        var afterWrite = File.GetLastWriteTimeUtc(_tempSavePath);
        Assert.Equal(beforeWrite, afterWrite);
    }

    [Fact]
    public void TPK_Deletes_Save_When_Ironman()
    {
        // Create an ironman save
        var state = new GameState(seed: 42);
        state.IsIronman = true;
        state.SavePath = _tempSavePath;
        SaveSystem.Save(state, _tempSavePath);
        Assert.True(File.Exists(_tempSavePath));

        // Simulate TPK by creating a combat where all players die
        // Use an existing party member as the hero so combat resolution updates the party
        var member = state.Party.Members[0];
        var hero = new Combatant(member.Id, member.Name, true, 1, 10, 5, 0, new List<StatusEffect>());
        var enemy = new Combatant(Guid.NewGuid(), "Enemy", false, 10, 10, 5, 0, new List<StatusEffect>(), 5, null, false, 0, null, "aggressive");

        var combatState = new CombatState(
            new[] { hero, enemy },
            1,
            new[] { enemy.Id, hero.Id },
            0,
            new List<CombatLogEntry>(),
            null,
            CombatPhase.Turn,
            10);

        var combatProp = typeof(GameState).GetProperty("Combat")!;
        combatProp.SetValue(state, combatState);
        state.Mode = GameMode.Combat;

        var service = new CombatService(null, null, new GameRandom(1));
        // Enemy attacks hero, hero dies
        var result = service.SubmitCombatAction(state, new CombatAction(enemy.Id, ActionType.Attack, hero.Id, null, null));

        Assert.True(result, "SubmitCombatAction should return true");

        // Combat should have ended with defeat
        Assert.Null(state.Combat);
        Assert.NotNull(state.LastCombatResult);
        Assert.False(state.LastCombatResult.Victory);

        // Save should be deleted
        Assert.False(File.Exists(_tempSavePath), "Ironman save should be deleted on TPK");
    }

    [Fact]
    public void TPK_Does_Not_Delete_Save_When_Not_Ironman()
    {
        var state = new GameState(seed: 42);
        state.IsIronman = false;
        state.SavePath = _tempSavePath;
        SaveSystem.Save(state, _tempSavePath);
        Assert.True(File.Exists(_tempSavePath));

        var member2 = state.Party.Members[0];
        var hero2 = new Combatant(member2.Id, member2.Name, true, 1, 10, 5, 0, new List<StatusEffect>());
        var enemy2 = new Combatant(Guid.NewGuid(), "Enemy", false, 10, 10, 5, 0, new List<StatusEffect>(), 5, null, false, 0, null, "aggressive");

        var combatState2 = new CombatState(
            new[] { hero2, enemy2 },
            1,
            new[] { enemy2.Id, hero2.Id },
            0,
            new List<CombatLogEntry>(),
            null,
            CombatPhase.Turn,
            10);

        var combatProp2 = typeof(GameState).GetProperty("Combat")!;
        combatProp2.SetValue(state, combatState2);
        state.Mode = GameMode.Combat;

        var service2 = new CombatService(null, null, new GameRandom(1));
        service2.SubmitCombatAction(state, new CombatAction(enemy2.Id, ActionType.Attack, hero2.Id, null, null));

        Assert.False(state.LastCombatResult?.Victory ?? true);
        Assert.True(File.Exists(_tempSavePath), "Non-ironman save should NOT be deleted on TPK");
    }
}

internal class StubDungeonGenerator : IDungeonGenerator
{
    public Dungeon Generate(string dungeonType, int? seed = null) => new Dungeon(10, 10, dungeonType);
}
