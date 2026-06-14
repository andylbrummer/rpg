using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using Xunit;

namespace RPC.Tests;

public class DungeonGeneratorLootTests
{
    // Load the real broken-engine segment pool from disk, mirroring NewDungeonTemplateTests.
    private static List<RoomSegment> Segments() =>
        SegmentLoader.LoadFromDirectory("../../../../../../content/segments/broken-engine");

    private static DungeonTemplate Template() => new(
        Id: "broken_engine",
        Name: "Broken Engine",
        SegmentPool: new[] { "entrance", "chamber", "corridor", "dead_end", "boss_room" },
        SegmentPriority: new[] { "entrance" },
        TargetRooms: 6,
        BossEncounterId: "boss-1",
        EncounterTableId: "broken_engine",
        WanderingTableId: null,
        UnlockConditions: null,
        LootTableId: "broken_engine");

    private static DungeonGenerator MakeGen()
    {
        var loot = new DungeonLootTableRegistry();
        loot.LoadFromJson("broken_engine", """{ "id":"broken_engine", "entries":[ {"itemId":"scrap_plating","weight":1} ] }""");
        var templates = new Dictionary<string, DungeonTemplate> { ["broken_engine"] = Template() };
        return new DungeonGenerator(Segments(), dungeonTemplates: templates, encounterTables: null, lootTables: loot);
    }

    [Fact]
    public void Generated_dungeon_places_at_least_one_loot_when_table_available()
    {
        // The biased placement is probabilistic per room, so over a spread of seeds at least
        // some generated dungeons must carry loot when a loot table resolves.
        var gen = MakeGen();
        int withLoot = 0;
        for (int seed = 0; seed < 30; seed++)
            if (CountLoot(gen.Generate("broken_engine", seed)) >= 1) withLoot++;

        Assert.True(withLoot >= 1, "expected at least one generated dungeon to carry loot");
    }

    [Fact]
    public void Loot_layout_is_deterministic_for_same_seed()
    {
        var gen = MakeGen();
        var a = gen.Generate("broken_engine", seed: 99);
        var b = gen.Generate("broken_engine", seed: 99);
        Assert.Equal(LootSignature(a), LootSignature(b));
    }

    [Fact]
    public void Procedural_pacer_path_places_spread_loot_and_never_in_boss_room()
    {
        // Regression for the boss-misidentification bug: on the procedural (pacer) path the pacer
        // tags MANY tiles with encounter ids before decoration. A classifier that picks the FIRST
        // encounter tile as the boss latches onto a near-entrance encounter, mislabels the genuine
        // boss room as a loot-eligible role, and lets loot land in the boss room. Identifying the
        // boss by its specific encounter id ("boss-encounter-1") keeps the boss room excluded.
        //
        // No template is registered, so generation takes the procedural fallback and the pacer runs
        // (encounter table id == dungeon type). The boss tile is tagged with the procedural default
        // "boss-encounter-1".
        var loot = new DungeonLootTableRegistry();
        loot.LoadFromJson("broken_engine", """{ "id":"broken_engine", "entries":[ {"itemId":"scrap_plating","weight":1} ] }""");
        var encounters = new EncounterTableRegistry();
        encounters.LoadFromJson("broken_engine", """
        { "id":"broken_engine","name":"Broken Engine","entries":[
          { "id":"e1","weight":1,"enemies":[],"dangerRating":1 },
          { "id":"e2","weight":1,"enemies":[],"dangerRating":5 } ] }
        """);
        var gen = new DungeonGenerator(Segments(), dungeonTemplates: null,
            encounterTables: encounters, lootTables: loot);

        int withLoot = 0, bossRoomLoot = 0;
        var distinctLootRooms = new HashSet<string>();
        for (int seed = 0; seed < 30; seed++)
        {
            var d = gen.Generate("broken_engine", seed);
            int bossRoom = BossRoomId(d);
            int seedLoot = 0;
            for (int x = 0; x < d.Width; x++)
                for (int y = 0; y < d.Height; y++)
                {
                    var t = d.Tiles[x, y];
                    if (t.LootId is null) continue;
                    seedLoot++;
                    distinctLootRooms.Add($"{seed}:{t.RoomId}");
                    if (t.RoomId == bossRoom) bossRoomLoot++;
                }
            if (seedLoot >= 1) withLoot++;
        }

        Assert.True(withLoot >= 1, "pacer path should still place loot");
        // Loot must reach more than a single collapsed cluster of rooms.
        Assert.True(distinctLootRooms.Count > 1, "loot should spread across multiple rooms, not collapse");
        // The genuine boss room is always excluded once the boss is identified by id.
        Assert.Equal(0, bossRoomLoot);
    }

    private static int BossRoomId(Dungeon d)
    {
        for (int x = 0; x < d.Width; x++)
            for (int y = 0; y < d.Height; y++)
                if (d.Tiles[x, y].EncounterId == "boss-encounter-1") return d.Tiles[x, y].RoomId;
        return int.MinValue;
    }

    private static int CountLoot(Dungeon d)
    {
        int n = 0;
        for (int x = 0; x < d.Width; x++)
            for (int y = 0; y < d.Height; y++)
                if (d.Tiles[x, y].LootId != null) n++;
        return n;
    }

    private static string LootSignature(Dungeon d)
    {
        var parts = new List<string>();
        for (int x = 0; x < d.Width; x++)
            for (int y = 0; y < d.Height; y++)
                if (d.Tiles[x, y].LootId is { } id) parts.Add($"{x},{y}={id}");
        return string.Join("|", parts);
    }
}
