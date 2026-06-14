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
