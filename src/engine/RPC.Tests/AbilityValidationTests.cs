using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

public class AbilityValidationTests
{
    private static ClassRegistry MakeTestRegistry()
    {
        var registry = new ClassRegistry();
        var json = """
            {
              "id": "test_striker",
              "name": "Test Striker",
              "description": "Test class with row-restricted abilities",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 5, "intelligence": 3, "willpower": 3 },
              "abilities": [
                { "id": "front_slash", "name": "Front Slash", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d8+PWR", "range": "melee" }, "tags": ["physical"], "requiredRow": "front" },
                { "id": "back_shot", "name": "Back Shot", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d6+PWR", "range": "far" }, "tags": ["physical"], "requiredRow": "back" },
                { "id": "neutral_stab", "name": "Neutral Stab", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4+PWR", "range": "melee" }, "tags": ["physical"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["front_slash", "back_shot", "neutral_stab"] }
              ]
            }
            """;
        registry.LoadFromJson("test_striker", json);
        return registry;
    }

    private static GameState MakeGameState(int seed, int charRow)
    {
        var registry = MakeTestRegistry();
        var gs = new GameState(seed: seed, classRegistry: registry);
        var character = new CharacterState(
            new Guid("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"), "Test", "test_striker", 1, 0,
            new BaseStats(5, 5, 5, 3, 3), 100, Equipment.Empty,
            new[] { "front_slash", "back_shot", "neutral_stab" }, charRow);
        gs.Party.SetMember(0, character);
        for (int i = 1; i < 6; i++)
            gs.Party.SetMember(i, default);
        return gs;
    }

    private static Dungeon CreateTinyDungeon()
    {
        var dungeon = new Dungeon(3, 3, "test");
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                dungeon.Tiles[x, y] = new Tile(TileType.Floor);
        return dungeon;
    }

    [Fact]
    public void RowFilter_FrontAbility_AllowedInFrontRow()
    {
        var gs = MakeGameState(1, 0);
        gs.EnterDungeon(CreateTinyDungeon(), "test");
        gs.TriggerEncounter(new EncounterDef("test", "Test", new[] { new EnemySpawn("rat", 3, 0) }));

        var combat = gs.Combat!;
        var player = combat.Combatants.First(c => c.IsPlayer);
        var enemy = combat.Combatants.First(c => !c.IsPlayer);

        var result = gs.SubmitCombatAction(
            new CombatAction(player.Id, ActionType.UseAbility, enemy.Id, "front_slash", null));

        Assert.True(result);
    }

    [Fact]
    public void RowFilter_FrontAbility_RejectedInBackRow()
    {
        var gs = MakeGameState(1, 1);
        gs.EnterDungeon(CreateTinyDungeon(), "test");
        gs.TriggerEncounter(new EncounterDef("test", "Test", new[] { new EnemySpawn("rat", 3, 0) }));

        var combat = gs.Combat!;
        var player = combat.Combatants.First(c => c.IsPlayer);
        var enemy = combat.Combatants.First(c => !c.IsPlayer);

        var result = gs.SubmitCombatAction(
            new CombatAction(player.Id, ActionType.UseAbility, enemy.Id, "front_slash", null));

        Assert.False(result);
    }

    [Fact]
    public void RowFilter_BackAbility_AllowedInBackRow()
    {
        var gs = MakeGameState(1, 1);
        gs.EnterDungeon(CreateTinyDungeon(), "test");
        gs.TriggerEncounter(new EncounterDef("test", "Test", new[] { new EnemySpawn("rat", 3, 0) }));

        var combat = gs.Combat!;
        var player = combat.Combatants.First(c => c.IsPlayer);
        var enemy = combat.Combatants.First(c => !c.IsPlayer);

        var result = gs.SubmitCombatAction(
            new CombatAction(player.Id, ActionType.UseAbility, enemy.Id, "back_shot", null));

        Assert.True(result);
    }

    [Fact]
    public void RowFilter_BackAbility_RejectedInFrontRow()
    {
        var gs = MakeGameState(1, 0);
        gs.EnterDungeon(CreateTinyDungeon(), "test");
        gs.TriggerEncounter(new EncounterDef("test", "Test", new[] { new EnemySpawn("rat", 3, 0) }));

        var combat = gs.Combat!;
        var player = combat.Combatants.First(c => c.IsPlayer);
        var enemy = combat.Combatants.First(c => !c.IsPlayer);

        var result = gs.SubmitCombatAction(
            new CombatAction(player.Id, ActionType.UseAbility, enemy.Id, "back_shot", null));

        Assert.False(result);
    }

    [Fact]
    public void RowFilter_NeutralAbility_AllowedInEitherRow()
    {
        var gsFront = MakeGameState(1, 0);
        gsFront.EnterDungeon(CreateTinyDungeon(), "test");
        gsFront.TriggerEncounter(new EncounterDef("test", "Test", new[] { new EnemySpawn("rat", 1, 0) }));

        var gsBack = MakeGameState(1, 1);
        gsBack.EnterDungeon(CreateTinyDungeon(), "test");
        gsBack.TriggerEncounter(new EncounterDef("test", "Test", new[] { new EnemySpawn("rat", 1, 0) }));

        var playerFront = gsFront.Combat!.Combatants.First(c => c.IsPlayer);
        var enemyFront = gsFront.Combat.Combatants.First(c => !c.IsPlayer);
        var playerBack = gsBack.Combat!.Combatants.First(c => c.IsPlayer);
        var enemyBack = gsBack.Combat.Combatants.First(c => !c.IsPlayer);

        Assert.True(gsFront.SubmitCombatAction(
            new CombatAction(playerFront.Id, ActionType.UseAbility, enemyFront.Id, "neutral_stab", null)));
        Assert.True(gsBack.SubmitCombatAction(
            new CombatAction(playerBack.Id, ActionType.UseAbility, enemyBack.Id, "neutral_stab", null)));
    }

    [Fact]
    public void RowFilter_CharacterMovedToBackRow_LosesFrontAbilities()
    {
        var gs = MakeGameState(1, 0);
        gs.EnterDungeon(CreateTinyDungeon(), "test");
        gs.TriggerEncounter(new EncounterDef("test", "Test", new[] { new EnemySpawn("rat", 3, 0) }));

        var combat = gs.Combat!;
        var player = combat.Combatants.First(c => c.IsPlayer);
        var enemy = combat.Combatants.First(c => !c.IsPlayer);

        // First, confirm ability works in front row
        Assert.True(gs.SubmitCombatAction(
            new CombatAction(player.Id, ActionType.UseAbility, enemy.Id, "front_slash", null)));

        // Find the character in party and move to back row
        var idx = Array.FindIndex(gs.Party.Members, m => m.Id == player.Id);
        gs.Party.SwapRows(idx);

        // Trigger a new encounter to get fresh combat state
        gs.TriggerEncounter(new EncounterDef("test", "Test", new[] { new EnemySpawn("rat", 3, 0) }));
        var combat2 = gs.Combat!;
        var player2 = combat2.Combatants.First(c => c.IsPlayer);
        var enemy2 = combat2.Combatants.First(c => !c.IsPlayer);

        var member = gs.Party.Members.First(m => m.Id == player2.Id);
        Assert.Equal(1, member.Row);
        Assert.Equal(1, player2.Row);

        var result = gs.SubmitCombatAction(
            new CombatAction(player2.Id, ActionType.UseAbility, enemy2.Id, "front_slash", null));

        Assert.False(result);
    }
}
