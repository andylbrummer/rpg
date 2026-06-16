using System.Linq;
using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

/// <summary>
/// T51b acceptance #3: an AoE/area-damage ability resolving in combat reveals a breakable wall
/// adjacent to the party's dungeon tile — the same end-state as an explicit search
/// (Journal.IsDiscovered true, trigger 'area_damage', border BreakableWall -> CrackedWall).
/// Combat is range-band based and carries no tile coordinates, so the encounter's dungeon tile
/// (the party position) is the AoE origin. A non-area attack, or an area ability used away from
/// any breakable wall, reveals nothing.
/// </summary>
public class CombatAreaDamageRevealTests
{
    // Test class with one area-damage ability ("blast") and one single-target attack ("jab").
    private static ClassRegistry MakeRegistry()
    {
        var registry = new ClassRegistry();
        var json = """
            {
              "id": "test_blaster",
              "name": "Test Blaster",
              "description": "Test class with an area-damage ability",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 5, "intelligence": 3, "willpower": 3 },
              "abilities": [
                { "id": "blast", "name": "Blast", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d6+PWR", "range": "any", "target": "enemy_group" }, "tags": ["fire", "area"] },
                { "id": "jab", "name": "Jab", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d6+PWR", "range": "melee" }, "tags": ["physical"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["blast", "jab"] }
              ]
            }
            """;
        registry.LoadFromJson("test_blaster", json);
        return registry;
    }

    private static GameState WallDungeonState(Position player)
    {
        var registry = MakeRegistry();
        var gs = new GameState(seed: 1, classRegistry: registry);
        var character = new CharacterState(
            new Guid("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"), "Blaster", "test_blaster", 1, 0,
            new BaseStats(5, 5, 5, 3, 3), 100, Equipment.Empty,
            new[] { "blast", "jab" }, 0);
        gs.Party.SetMember(0, character);
        for (int i = 1; i < 6; i++)
            gs.Party.SetMember(i, default);

        var dungeon = new Dungeon(6, 6, "test");
        for (int x = 0; x < 6; x++)
            for (int y = 0; y < 6; y++)
                dungeon.Tiles[x, y] = new Tile(TileType.Floor);
        // Breakable wall on north border of (3,2) <-> south border of (3,1).
        dungeon.Tiles[3, 2] = new Tile(TileType.Floor, North: BorderType.BreakableWall);
        dungeon.Tiles[3, 1] = new Tile(TileType.Floor, South: BorderType.BreakableWall);

        gs.EnterDungeon(dungeon, "test");
        gs.Player.Position = player;
        gs.Secrets.Register(new SecretDef("crack", "breakable_wall", X: 3, Y: 2, Wall: "North"));
        return gs;
    }

    private static (Guid actor, Guid enemy) StartCombat(GameState gs)
    {
        gs.TriggerEncounter(new EncounterDef("test", "Test", new[] { new EnemySpawn("rat", 3, 0) }));
        var combat = gs.Combat!;
        var player = combat.Combatants.First(c => c.IsPlayer);
        var enemy = combat.Combatants.First(c => !c.IsPlayer);
        return (player.Id, enemy.Id);
    }

    [Fact]
    public void AreaDamage_AdjacentToBreakableWall_RevealsWall_WithAreaDamageTrigger()
    {
        var gs = WallDungeonState(new Position(3, 2));
        var (actor, enemy) = StartCombat(gs);

        Assert.False(gs.Journal.IsDiscovered("crack"));

        Assert.True(gs.SubmitCombatAction(
            new CombatAction(actor, ActionType.UseAbility, enemy, "blast", null)));

        Assert.True(gs.Journal.IsDiscovered("crack"));
        Assert.Equal(BorderType.CrackedWall, gs.CurrentDungeon!.Tiles[3, 2].North);
        Assert.Equal(BorderType.CrackedWall, gs.CurrentDungeon.Tiles[3, 1].South);
        Assert.Contains(gs.ActionLog,
            e => e.Type == "secret_discovered" && e.Payload.TryGetValue("trigger", out var t) && t == "area_damage");
    }

    [Fact]
    public void SingleTargetAttack_AdjacentToBreakableWall_RevealsNothing()
    {
        var gs = WallDungeonState(new Position(3, 2));
        var (actor, enemy) = StartCombat(gs);

        Assert.True(gs.SubmitCombatAction(
            new CombatAction(actor, ActionType.UseAbility, enemy, "jab", null)));

        Assert.False(gs.Journal.IsDiscovered("crack"));
        Assert.Equal(BorderType.BreakableWall, gs.CurrentDungeon!.Tiles[3, 2].North);
    }

    [Fact]
    public void AreaDamage_AwayFromAnyWall_RevealsNothing()
    {
        // Party is across the map from the breakable wall at (3,2): Chebyshev distance > 1.
        var gs = WallDungeonState(new Position(0, 5));
        var (actor, enemy) = StartCombat(gs);

        Assert.True(gs.SubmitCombatAction(
            new CombatAction(actor, ActionType.UseAbility, enemy, "blast", null)));

        Assert.False(gs.Journal.IsDiscovered("crack"));
        Assert.Equal(BorderType.BreakableWall, gs.CurrentDungeon!.Tiles[3, 2].North);
    }
}
