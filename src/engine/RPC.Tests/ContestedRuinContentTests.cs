using System.Text.Json;
using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

/// <summary>
/// Smoke tests for the Contested Ruin dungeon template — segment loading,
/// border-type compatibility (BreakableWall + SecretDoor present), encounter
/// table integrity, and the multi-faction encounter mix the spec requires.
/// </summary>
public class ContestedRuinContentTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    private static string ContentPath(string relative)
        => $"../../../../../../content/{relative}";

    [Fact]
    public void DungeonTemplate_Loads()
    {
        var path = ContentPath("campaigns/dungeons/contested-ruin.json");
        Assert.True(File.Exists(path), $"Missing template: {path}");

        var template = JsonSerializer.Deserialize<DungeonTemplate>(File.ReadAllText(path), JsonOptions);
        Assert.NotNull(template);
        Assert.Equal("contested_ruin", template.Id);
        Assert.Equal("Contested Ruin", template.Name);
        Assert.True(template.TargetRooms >= 6);
        Assert.Equal("cr-boss-arbiter", template.BossEncounterId);
        Assert.Equal("contested_ruin", template.EncounterTableId);
        Assert.Equal("contested_ruin", template.WanderingTableId);

        Assert.Contains("ruin_entrance", template.SegmentPool);
        Assert.Contains("ruin_boss_room", template.SegmentPool);
        Assert.Contains("contested_hall", template.SegmentPool);
    }

    [Fact]
    public void Segments_Load_With_Required_Variety()
    {
        var dir = ContentPath("segments/contested-ruin");
        Assert.True(Directory.Exists(dir), $"Missing dir: {dir}");

        var segments = SegmentLoader.LoadFromDirectory(dir);
        Assert.True(segments.Count >= 5, $"Need >=5 segments, got {segments.Count}");

        Assert.Contains(segments, s => s.Id == "ruin_entrance");
        Assert.Contains(segments, s => s.Id == "ruin_corridor");
        Assert.Contains(segments, s => s.Id == "ruin_boss_room");
        Assert.Contains(segments, s => s.Id == "contested_hall");

        foreach (var segment in segments)
        {
            Assert.NotEmpty(segment.Name);
            Assert.NotEmpty(segment.Tiles);
            foreach (var tile in segment.Tiles.Where(t => t.IsExit))
            {
                Assert.NotNull(tile.ExitDirection);
                var border = tile.ExitDirection!.Value switch
                {
                    Direction.North => tile.North,
                    Direction.South => tile.South,
                    Direction.East => tile.East,
                    Direction.West => tile.West,
                    _ => (BorderType?)null
                };
                Assert.Equal(BorderType.Door, border);
            }
        }
    }

    [Fact]
    public void Segments_Use_BreakableWall_And_SecretDoor()
    {
        var dir = ContentPath("segments/contested-ruin");
        var segments = SegmentLoader.LoadFromDirectory(dir);

        var hasBreakable = segments.Any(s => s.Tiles.Any(t =>
            t.North == BorderType.BreakableWall || t.South == BorderType.BreakableWall ||
            t.East == BorderType.BreakableWall || t.West == BorderType.BreakableWall));
        var hasSecret = segments.Any(s => s.Tiles.Any(t =>
            t.North == BorderType.SecretDoor || t.South == BorderType.SecretDoor ||
            t.East == BorderType.SecretDoor || t.West == BorderType.SecretDoor));

        Assert.True(hasBreakable, "Contested Ruin segments must include at least one BreakableWall");
        Assert.True(hasSecret, "Contested Ruin segments must include at least one SecretDoor");
    }

    [Fact]
    public void EncounterTable_Loads_And_Has_Multi_Faction_Mix()
    {
        var path = ContentPath("encounters/contested_ruin.json");
        Assert.True(File.Exists(path), $"Missing encounters: {path}");

        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("contested_ruin", File.ReadAllText(path));

        var table = registry.Get("contested_ruin");
        Assert.NotNull(table);
        Assert.NotEmpty(table.Entries);
        Assert.All(table.Entries, e => Assert.True(e.Weight > 0));

        var factions = table.Entries
            .Select(e => e.FactionId)
            .Where(f => !string.IsNullOrEmpty(f))
            .Distinct()
            .ToList();
        Assert.Contains("bureau", factions);
        Assert.Contains("convocation", factions);

        Assert.Contains(table.Entries, e => e.Id == "cr-boss-arbiter");
    }

    [Fact]
    public void Template_Segment_Pool_References_Exist()
    {
        var template = JsonSerializer.Deserialize<DungeonTemplate>(
            File.ReadAllText(ContentPath("campaigns/dungeons/contested-ruin.json")), JsonOptions);
        var segments = SegmentLoader.LoadFromDirectory(ContentPath("segments/contested-ruin"));
        var segmentIds = segments.Select(s => s.Id).ToHashSet();

        foreach (var poolId in template!.SegmentPool)
        {
            Assert.Contains(poolId, segmentIds);
        }
    }
}
