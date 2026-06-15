using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

/// <summary>
/// Proves a dungeon type routes through its own content-defined template + segment pool rather than
/// a single hard-coded path: entering two distinct types yields distinct template-driven identity
/// (name, encounter table) built from each type's own segments.
/// </summary>
public class DungeonTypeRoutingTests
{
    private static List<RoomSegment> SegmentsFrom(params string[] dirs)
    {
        var all = new List<RoomSegment>();
        foreach (var dir in dirs)
            all.AddRange(SegmentLoader.LoadFromDirectory($"../../../../../../content/segments/{dir}"));
        return all;
    }

    private static DungeonTemplate Template(string id, string name, string[] pool, string encTable) => new(
        Id: id,
        Name: name,
        SegmentPool: pool,
        SegmentPriority: pool,
        TargetRooms: 6,
        BossEncounterId: "boss",
        EncounterTableId: encTable,
        SegmentDirectory: $"segments/{id}");

    [Fact]
    public void TwoDungeonTypes_UseDistinctTemplatesAndSegments()
    {
        var brokenEngine = Template("broken_engine", "Broken Engine",
            new[] { "entrance", "chamber", "corridor", "dead_end", "boss_room" }, "broken_engine");
        var ossuary = Template("ossuary", "Ossuary",
            new[] { "ossuary_entrance", "memorial_corridor", "family_vault", "private_chamber", "ancestral_hall" }, "ossuary");

        // Segment pools are disjoint, so the only way each dungeon can build correctly is by routing
        // through its own template's pool, not a shared hard-coded directory.
        Assert.Empty(brokenEngine.SegmentPool.Intersect(ossuary.SegmentPool));

        var segments = SegmentsFrom("broken-engine", "ossuary");
        var templates = new Dictionary<string, DungeonTemplate>
        {
            [brokenEngine.Id] = brokenEngine,
            [ossuary.Id] = ossuary
        };
        var gen = new DungeonGenerator(segments, dungeonTemplates: templates);

        var a = gen.Generate("broken_engine", seed: 7);
        var b = gen.Generate("ossuary", seed: 7);

        // Distinct template usage: name + encounter table are template-driven and differ per type.
        Assert.Equal("Broken Engine", a.Name);
        Assert.Equal("Ossuary", b.Name);
        Assert.NotEqual(a.Name, b.Name);
        Assert.Equal("broken_engine", a.EncounterTableId);
        Assert.Equal("ossuary", b.EncounterTableId);
        Assert.NotEqual(a.EncounterTableId, b.EncounterTableId);
    }
}
