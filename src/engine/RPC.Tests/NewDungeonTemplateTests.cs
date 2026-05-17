using System.Text.Json;
using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

public class NewDungeonTemplateTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    private static string ContentPath(string relative)
        => $"../../../../../../content/{relative}";

    public static IEnumerable<object[]> NewDungeonIds => new[]
    {
        new object[] { "boneyard", "boneyard_entrance", "bone_nest", "by-d5-bone-lord" },
        new object[] { "sealed_vault", "vault_entrance", "vault_heart", "sv-d5-ward-keeper" },
        new object[] { "settlement_gone_wrong", "ruined_gate", "town_square", "sgw-d5-bloom-knight" },
        new object[] { "ossuary", "ossuary_entrance", "ancestral_hall", "os-d5-ancestor" }
    };

    [Theory]
    [MemberData(nameof(NewDungeonIds))]
    public void DungeonTemplate_Loads(string dungeonId, string entranceId, string bossId, string bossEncounterId)
    {
        var path = ContentPath($"campaigns/dungeons/{dungeonId.Replace('_', '-')}.json");
        Assert.True(File.Exists(path), $"Missing dungeon template: {path}");

        var json = File.ReadAllText(path);
        var template = JsonSerializer.Deserialize<DungeonTemplate>(json, JsonOptions);

        Assert.NotNull(template);
        Assert.Equal(dungeonId, template.Id);
        Assert.NotEmpty(template.Name);
        Assert.True(template.TargetRooms > 0);
        Assert.Equal(bossEncounterId, template.BossEncounterId);
        Assert.NotEmpty(template.EncounterTableId);
        Assert.NotEmpty(template.WanderingTableId);
        Assert.Contains(entranceId, template.SegmentPool);
        Assert.Contains(bossId, template.SegmentPool);
    }

    [Theory]
    [MemberData(nameof(NewDungeonIds))]
    public void Segments_Load(string dungeonId, string entranceId, string bossId, string bossEncounterId)
    {
        var dir = ContentPath($"segments/{dungeonId.Replace('_', '-')}");
        Assert.True(Directory.Exists(dir), $"Missing segment directory: {dir}");

        var segments = SegmentLoader.LoadFromDirectory(dir);
        Assert.Equal(5, segments.Count);
        Assert.Contains(segments, s => s.Id == entranceId);
        Assert.Contains(segments, s => s.Id == bossId);

        foreach (var segment in segments)
        {
            Assert.NotEmpty(segment.Name);
            Assert.NotEmpty(segment.Tiles);
            foreach (var tile in segment.Tiles.Where(t => t.IsExit))
            {
                Assert.NotNull(tile.ExitDirection);
                var border = tile.ExitDirection.Value switch
                {
                    Direction.North => tile.North,
                    Direction.South => tile.South,
                    Direction.East => tile.East,
                    Direction.West => tile.West,
                    _ => null
                };
                Assert.Equal(BorderType.Door, border);
            }
        }
    }

    [Theory]
    [MemberData(nameof(NewDungeonIds))]
    public void EncounterTable_Loads(string dungeonId, string entranceId, string bossId, string bossEncounterId)
    {
        var path = ContentPath($"encounters/{dungeonId}.json");
        Assert.True(File.Exists(path), $"Missing encounter table: {path}");

        var json = File.ReadAllText(path);
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson(dungeonId, json);

        var table = registry.Get(dungeonId);
        Assert.NotNull(table);
        Assert.NotEmpty(table.Entries);
        Assert.All(table.Entries, e => Assert.True(e.Weight > 0));
    }

    [Theory]
    [MemberData(nameof(NewDungeonIds))]
    public void BossEncounter_Exists(string dungeonId, string entranceId, string bossId, string bossEncounterId)
    {
        var path = ContentPath("encounters/boss.json");
        var json = File.ReadAllText(path);
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("boss", json);

        var table = registry.Get("boss");
        Assert.NotNull(table);
        Assert.Contains(table.Entries, e => e.Id == bossEncounterId);
    }
}
