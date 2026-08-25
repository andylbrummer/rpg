using System.Text.Json;
using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

/// <summary>
/// Smoke tests for The Underway dungeon: flooded rail tunnels with
/// Cartography / Stillness presence. Mirrors the Contested Ruin checks.
/// </summary>
public class UnderwayContentTests
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
        var path = ContentPath("campaigns/dungeons/underway.json");
        Assert.True(File.Exists(path), $"Missing template: {path}");

        var template = JsonSerializer.Deserialize<DungeonTemplate>(File.ReadAllText(path), JsonOptions);
        Assert.NotNull(template);
        Assert.Equal("underway", template.Id);
        Assert.Equal("The Underway", template.Name);
        Assert.True(template.TargetRooms >= 6);
        Assert.Equal("uw-boss-leviathan", template.BossEncounterId);
        Assert.Equal("underway", template.EncounterTableId);
        Assert.Equal("underway", template.WanderingTableId);

        Assert.Contains("underway_entrance", template.SegmentPool);
        Assert.Contains("underway_terminus", template.SegmentPool);
        Assert.Contains("underway_junction", template.SegmentPool);
    }

    [Fact]
    public void Segments_Load_With_Exit_Door_Invariant()
    {
        var dir = ContentPath("segments/underway");
        Assert.True(Directory.Exists(dir), $"Missing dir: {dir}");

        var segments = SegmentLoader.LoadFromDirectory(dir);
        Assert.True(segments.Count >= 5, $"Need >=5 segments, got {segments.Count}");

        Assert.Contains(segments, s => s.Id == "underway_entrance");
        Assert.Contains(segments, s => s.Id == "underway_tunnel");
        Assert.Contains(segments, s => s.Id == "underway_terminus");

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
        var segments = SegmentLoader.LoadFromDirectory(ContentPath("segments/underway"));

        var hasBreakable = segments.Any(s => s.Tiles.Any(t =>
            t.North == BorderType.BreakableWall || t.South == BorderType.BreakableWall ||
            t.East == BorderType.BreakableWall || t.West == BorderType.BreakableWall));
        var hasSecret = segments.Any(s => s.Tiles.Any(t =>
            t.North == BorderType.SecretDoor || t.South == BorderType.SecretDoor ||
            t.East == BorderType.SecretDoor || t.West == BorderType.SecretDoor));

        Assert.True(hasBreakable, "Underway segments must include at least one BreakableWall");
        Assert.True(hasSecret, "Underway segments must include at least one SecretDoor");
    }

    [Fact]
    public void EncounterTable_Loads_With_Cartography_And_Stillness_Mix()
    {
        var path = ContentPath("encounters/underway.json");
        Assert.True(File.Exists(path));

        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("underway", File.ReadAllText(path));

        var table = registry.Get("underway");
        Assert.NotNull(table);
        Assert.NotEmpty(table.Entries);
        Assert.All(table.Entries, e => Assert.True(e.Weight > 0));

        var factions = table.Entries
            .Select(e => e.FactionId)
            .Where(f => !string.IsNullOrEmpty(f))
            .Distinct()
            .ToList();
        Assert.Contains("cartography", factions);
        Assert.Contains("stillness", factions);

        Assert.Contains(table.Entries, e => e.Id == "uw-boss-leviathan");
    }

    [Fact]
    public void Template_Segment_Pool_References_Exist()
    {
        var template = JsonSerializer.Deserialize<DungeonTemplate>(
            File.ReadAllText(ContentPath("campaigns/dungeons/underway.json")), JsonOptions);
        var segments = SegmentLoader.LoadFromDirectory(ContentPath("segments/underway"));
        var ids = segments.Select(s => s.Id).ToHashSet();

        foreach (var poolId in template!.SegmentPool)
        {
            Assert.Contains(poolId, ids);
        }
    }
}
