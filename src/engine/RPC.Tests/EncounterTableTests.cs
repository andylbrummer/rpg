using RPC.Engine;
using RPC.Engine.Combat;

namespace RPC.Tests;

public class EncounterTableTests
{
    private const string TableJson = """
    {
      "id": "test_table",
      "name": "Test Table",
      "entries": [
        { "weight": 50, "enemies": [{ "enemyId": "rat", "count": 1 }] },
        { "weight": 30, "enemies": [{ "enemyId": "rat", "count": 2 }] },
        { "weight": 20, "enemies": [{ "enemyId": "goblin", "count": 1 }] }
      ]
    }
    """;

    [Fact]
    public void EncounterTableRegistry_LoadFromJson_ParsesCorrectly()
    {
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("test_table", TableJson);

        var table = registry.Get("test_table");
        Assert.NotNull(table);
        Assert.Equal("Test Table", table.Name);
        Assert.Equal(3, table.Entries.Length);
        Assert.Equal(100, table.Entries.Sum(e => e.Weight));
    }

    [Fact]
    public void EncounterTableRegistry_RollEncounter_RespectsWeights()
    {
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("test_table", TableJson);

        // Roll many times and check distribution
        var counts = new Dictionary<string, int>();
        for (int seed = 0; seed < 1000; seed++)
        {
            var enc = registry.RollEncounter("test_table", new GameRandom(seed));
            var key = string.Join(",", enc.Enemies.Select(e => $"{e.EnemyId}x{e.Count}"));
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        // ratx1 should be ~50%, ratx2 ~30%, goblinx1 ~20%
        Assert.True(counts.GetValueOrDefault("ratx1") > 400, "ratx1 should be ~50%");
        Assert.True(counts.GetValueOrDefault("ratx2") > 200, "ratx2 should be ~30%");
        Assert.True(counts.GetValueOrDefault("goblinx1") > 100, "goblinx1 should be ~20%");
    }

    [Fact]
    public void EncounterTableRegistry_RollUnknown_ReturnsDefault()
    {
        var registry = new EncounterTableRegistry();
        var enc = registry.RollEncounter("missing", new GameRandom(1));
        Assert.Equal("default", enc.Id);
        Assert.Empty(enc.Enemies);
    }

    [Fact]
    public void GameState_DungeonEncounterTable_UsedWhenTriggering()
    {
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("broken_engine", TableJson);

        var gs = new GameState(seed: 42, encounterTables: registry);
        var dungeon = new RPC.Engine.Models.Dungeons.Dungeon(3, 3, "test");
        dungeon.EncounterTableId = "broken_engine";
        gs.EnterDungeon(dungeon, "broken_engine");

        gs.TriggerEncounter();

        Assert.Equal(GameMode.Combat, gs.Mode);
        Assert.NotNull(gs.Combat);
        // Should have spawned enemies from the table, not the default
        Assert.NotEmpty(gs.Combat.Combatants.Where(c => !c.IsPlayer));
    }
}
