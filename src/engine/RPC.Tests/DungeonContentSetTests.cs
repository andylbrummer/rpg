using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

public class DungeonContentSetTests
{
    private static RoomSegment Seg(string id) => new()
    {
        Id = id,
        Name = id,
        Tiles = new List<SegmentTile> { new() { X = 0, Y = 0 } }
    };

    private static EncounterTableRegistry Encounters(params string[] ids)
    {
        var registry = new EncounterTableRegistry();
        foreach (var id in ids)
            registry.LoadFromJson(id, "{\"entries\":[{\"id\":\"e1\",\"weight\":1}]}");
        return registry;
    }

    private static DungeonTemplate Template(string id, string dir, string[] pool, string encounterTable, string name = "Name")
        => new(
            Id: id,
            Name: name,
            SegmentPool: pool,
            SegmentPriority: pool,
            TargetRooms: 6,
            BossEncounterId: "boss",
            EncounterTableId: encounterTable,
            SegmentDirectory: dir);

    private static DungeonContentSet SetOf(params DungeonTemplate[] templates)
        => new(templates.ToDictionary(t => t.Id));

    [Fact]
    public void Validate_PassesForWellFormedTemplates()
    {
        var set = SetOf(
            Template("a", "segments/alpha", new[] { "a1", "a2" }, "alpha"),
            Template("b", "segments/beta", new[] { "b1" }, "beta"));

        // Should not throw.
        set.Validate(new[] { Seg("a1"), Seg("a2"), Seg("b1") }, Encounters("alpha", "beta"));
    }

    [Fact]
    public void Validate_ThrowsForUnknownSegmentId()
    {
        var set = SetOf(Template("a", "segments/alpha", new[] { "a1", "missing" }, "alpha"));

        var ex = Assert.Throws<InvalidOperationException>(
            () => set.Validate(new[] { Seg("a1") }, Encounters("alpha")));
        Assert.Contains("unknown segment id 'missing'", ex.Message);
    }

    [Fact]
    public void Validate_ThrowsForUnknownEncounterTable()
    {
        var set = SetOf(Template("a", "segments/alpha", new[] { "a1" }, "nope"));

        var ex = Assert.Throws<InvalidOperationException>(
            () => set.Validate(new[] { Seg("a1") }, Encounters("alpha")));
        Assert.Contains("unknown encounter table 'nope'", ex.Message);
    }

    [Fact]
    public void Validate_ThrowsForMissingNameAndDirectory()
    {
        var set = SetOf(Template("a", "", new[] { "a1" }, "alpha", name: "  "));

        var ex = Assert.Throws<InvalidOperationException>(
            () => set.Validate(new[] { Seg("a1") }, Encounters("alpha")));
        Assert.Contains("no display name", ex.Message);
        Assert.Contains("no segment directory", ex.Message);
    }

    [Fact]
    public void SegmentDirectories_AreDistinctAndTrimmed()
    {
        var set = SetOf(
            Template("a", "segments/shared/", new[] { "a1" }, "alpha"),
            Template("b", "segments/shared", new[] { "b1" }, "beta"),
            Template("c", "segments/other", new[] { "c1" }, "gamma"));

        Assert.Equal(new[] { "segments/other", "segments/shared" }, set.SegmentDirectories);
    }

    [Fact]
    public void TemplatesForDirectory_ReturnsAllSharingTemplates()
    {
        var set = SetOf(
            Template("a", "segments/shared", new[] { "a1" }, "alpha"),
            Template("b", "segments/shared/", new[] { "b1" }, "beta"),
            Template("c", "segments/other", new[] { "c1" }, "gamma"));

        var shared = set.TemplatesForDirectory("segments/shared").Select(t => t.Id).OrderBy(x => x);
        Assert.Equal(new[] { "a", "b" }, shared);
        Assert.Empty(set.TemplatesForDirectory("segments/none"));
    }
}
