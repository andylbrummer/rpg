using RPC.Engine;
using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

public class GameStateCombatTests
{
    private static Dungeon CreateTinyDungeon()
    {
        var dungeon = new Dungeon(3, 3, "test");
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                dungeon.Tiles[x, y] = new Tile(TileType.Floor);
        return dungeon;
    }

    [Fact]
    public void GameState_ExplorationMode_ByDefault()
    {
        var gs = new GameState(seed: 42);
        Assert.Equal(GameMode.Exploration, gs.Mode);
    }

    [Fact]
    public void GameState_TriggerEncounter_SwitchesToCombat()
    {
        var gs = new GameState(seed: 42);
        gs.TriggerEncounter();

        Assert.Equal(GameMode.Combat, gs.Mode);
        Assert.NotNull(gs.Combat);
        Assert.NotEmpty(gs.Combat.Combatants);
    }

    [Fact]
    public void GameState_CombatVictory_ReturnsToExploration()
    {
        var gs = new GameState(seed: 42);
        // Empty encounter = immediate victory
        gs.TriggerEncounter(new EncounterDef("empty", "None", System.Array.Empty<EnemySpawn>()));

        Assert.Equal(GameMode.Exploration, gs.Mode);
        Assert.Null(gs.Combat);
    }

    [Fact]
    public void GameState_MoveForward_DoesNotWorkInCombat()
    {
        var gs = new GameState(seed: 42);
        gs.EnterDungeon(CreateTinyDungeon());
        gs.TriggerEncounter();

        Assert.False(gs.TryMoveForward());
    }

    [Fact]
    public void GameState_SubmitCombatAction_ResolvesTurn()
    {
        var gs = new GameState(seed: 42);
        gs.EnterDungeon(CreateTinyDungeon());
        gs.TriggerEncounter(new EncounterDef("test", "Test", new[]
        {
            new EnemySpawn("rat", 1, 0)
        }));

        var combat = gs.Combat!;
        var player = combat.Combatants.First(c => c.IsPlayer);
        var enemy = combat.Combatants.First(c => !c.IsPlayer);

        var result = gs.SubmitCombatAction(new CombatAction(player.Id, ActionType.Attack, enemy.Id, null, null));
        Assert.True(result);
    }

    [Fact]
    public void GameState_FleeCombat_ReturnsToExploration()
    {
        var gs = new GameState(seed: 42);
        gs.TriggerEncounter();
        Assert.Equal(GameMode.Combat, gs.Mode);

        gs.FleeCombat();
        Assert.Equal(GameMode.Exploration, gs.Mode);
        Assert.Null(gs.Combat);
    }
}
