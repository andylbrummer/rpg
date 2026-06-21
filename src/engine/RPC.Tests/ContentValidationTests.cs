using System.Text.Json;
using System.Text.Json.Serialization;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Content;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Town;

namespace RPC.Tests;

public class ContentValidationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true
    };

    public static IEnumerable<object[]> ClassFiles => Directory
        .GetFiles("../../../../../../content/classes", "*.json")
        .Select(f => new object[] { Path.GetFileNameWithoutExtension(f) });

    [Theory]
    [MemberData(nameof(ClassFiles))]
    public void ClassJson_IsValid(string classId)
    {
        var path = $"../../../../../../content/classes/{classId}.json";
        Assert.True(File.Exists(path), $"Missing class file: {path}");

        var json = File.ReadAllText(path);
        var classDef = JsonSerializer.Deserialize<ClassDef>(json, JsonOptions);

        Assert.NotNull(classDef);
        Assert.Equal(classId, classDef.Id);
        Assert.NotEmpty(classDef.Name);
        Assert.NotEmpty(classDef.Description);
        Assert.True(classDef.BaseStats.Constitution > 0);
        Assert.InRange(classDef.Abilities.Length, 3, 12);
        Assert.All(classDef.Abilities, a =>
        {
            Assert.False(string.IsNullOrEmpty(a.Id));
            Assert.False(string.IsNullOrEmpty(a.Name));
            Assert.NotEmpty(a.Tags);
        });
        Assert.All(classDef.Abilities, a =>
            Assert.Single(classDef.Abilities, x => x.Id == a.Id)); // unique IDs
        Assert.InRange(classDef.LevelTable.Length, 5, 10);
        Assert.Contains(classDef.LevelTable, e => e.Level == 1);

        // Branch integrity: every declared branch must be backed by abilities,
        // every ability branch tag must reference a declared branch, and level-6
        // branches must chain to a valid level-3 branch with sound faction gates.
        var level3 = classDef.AvailableBranches ?? Array.Empty<string>();
        var level6 = classDef.Branches?.Select(b => b.Id).ToArray() ?? Array.Empty<string>();
        var declaredBranches = level3.Concat(level6).ToHashSet();
        var abilityBranches = classDef.Abilities
            .Where(a => !string.IsNullOrEmpty(a.Branch))
            .Select(a => a.Branch!)
            .ToHashSet();

        foreach (var branch in declaredBranches)
            Assert.Contains(branch, abilityBranches); // every branch grants >=1 ability
        foreach (var tag in abilityBranches)
            Assert.Contains(tag, declaredBranches); // no orphan branch-tagged abilities

        foreach (var b in classDef.Branches ?? Array.Empty<BranchDef>())
        {
            Assert.Contains(b.RequiresBranch, level3); // level-6 chains from a level-3 branch
            if (b.FallbackBranch != null)
                Assert.Contains(b.FallbackBranch, declaredBranches);
            if (b.FactionGate != null)
            {
                Assert.False(string.IsNullOrEmpty(b.FactionGate.FactionId));
                Assert.True(b.FactionGate.Threshold > 0);
            }
        }
    }

    public static IEnumerable<object[]> EnemyFiles => Directory
        .GetFiles("../../../../../../content/enemies", "*.json")
        .Select(f => new object[] { Path.GetFileNameWithoutExtension(f) });

    [Theory]
    [MemberData(nameof(EnemyFiles))]
    public void EnemyJson_IsValid(string enemyId)
    {
        var path = $"../../../../../../content/enemies/{enemyId}.json";
        Assert.True(File.Exists(path), $"Missing enemy file: {path}");

        var json = File.ReadAllText(path);
        var enemyDef = JsonSerializer.Deserialize<EnemyDef>(json, JsonOptions);

        Assert.NotNull(enemyDef);
        Assert.Equal(enemyId, enemyDef.Id);
        Assert.NotEmpty(enemyDef.Name);
        Assert.NotEmpty(enemyDef.Description);
        Assert.True(enemyDef.HpBase > 0);
        Assert.True(enemyDef.Speed > 0);
        Assert.NotEmpty(enemyDef.Ai);
        Assert.NotEmpty(enemyDef.Abilities);
        Assert.All(enemyDef.LootTable, l =>
        {
            Assert.False(string.IsNullOrEmpty(l.ItemId));
            Assert.InRange(l.Chance, 0.0, 1.0);
        });
    }

    public static IEnumerable<object[]> EncounterFiles => Directory
        .GetFiles("../../../../../../content/encounters", "*.json")
        .Select(f => new object[] { Path.GetFileNameWithoutExtension(f) });

    [Theory]
    [MemberData(nameof(EncounterFiles))]
    public void EncounterJson_IsValid(string tableId)
    {
        var path = $"../../../../../../content/encounters/{tableId}.json";
        Assert.True(File.Exists(path), $"Missing encounter file: {path}");

        var json = File.ReadAllText(path);
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson(tableId, json);

        var table = registry.Get(tableId);
        Assert.NotNull(table);
        Assert.Equal(tableId, table.Id);
        Assert.NotEmpty(table.Name);
        Assert.NotEmpty(table.Entries);

        var enemyFiles = Directory.GetFiles("../../../../../../content/enemies", "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToHashSet();

        Assert.All(table.Entries, e =>
        {
            Assert.True(e.Weight > 0, "Entry weight must be positive");
            Assert.NotEmpty(e.Enemies);
            Assert.All(e.Enemies, enemy =>
            {
                Assert.False(string.IsNullOrEmpty(enemy.EnemyId));
                Assert.True(enemy.Count > 0);
                Assert.True(enemyFiles.Contains(enemy.EnemyId),
                    $"Referenced enemy not found: {enemy.EnemyId}");
            });
        });
    }

    [Fact]
    public void BloomSiteEncounterJson_HasDangerRatingGroups()
    {
        var path = "../../../../../../content/encounters/bloom_site.json";
        var json = File.ReadAllText(path);
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("bloom_site", json);

        var table = registry.Get("bloom_site");
        Assert.NotNull(table);
        var groups = table.Entries.GroupBy(e => e.DangerRating).ToDictionary(g => g.Key, g => g.ToArray());
        for (int dr = 1; dr <= 5; dr++)
        {
            Assert.True(groups.ContainsKey(dr), $"Missing dangerRating {dr} entries");
            Assert.NotEmpty(groups[dr]);
        }
    }

    [Fact]
    public void BloomSiteSegments_AreValid()
    {
        var dir = "../../../../../../content/segments/bloom-site";
        Assert.True(Directory.Exists(dir), "Missing bloom-site segments directory");

        var segments = SegmentLoader.LoadFromDirectory(dir);
        Assert.Equal(5, segments.Count);

        var expectedIds = new[] { "bloom_entrance", "spore_corridor", "bloom_chamber", "decay_lab", "spore_nest" };
        foreach (var id in expectedIds)
        {
            var segment = segments.FirstOrDefault(s => s.Id == id);
            Assert.NotNull(segment);
            Assert.NotEmpty(segment.Name);
            Assert.NotEmpty(segment.Tiles);
        }

        foreach (var segment in segments)
        {
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

    [Fact]
    public void AllClasses_AbilityIdsAreGloballyUnique()
    {
        var classFiles = Directory.GetFiles("../../../../../../content/classes", "*.json");
        var allAbilityIds = new HashSet<string>();

        foreach (var file in classFiles)
        {
            var json = File.ReadAllText(file);
            var classDef = JsonSerializer.Deserialize<ClassDef>(json, JsonOptions);
            Assert.NotNull(classDef);

            foreach (var ability in classDef.Abilities)
            {
                Assert.True(allAbilityIds.Add(ability.Id),
                    $"Duplicate ability ID: {ability.Id} in {Path.GetFileName(file)}");
            }
        }
    }

    // ---- Exhaustive content-source guard ------------------------------------
    //
    // These tests enforce, from disk, that every content/**/*.json file belongs
    // to a category that has a typed validator and deserializes to its typed
    // definition. They mirror the category dispatch in tools/content-pack so the
    // packer can never silently emit an unvalidated category. Adding a new
    // content directory without wiring a validator here (and in the packer) is a
    // hard failure, not a silent pass.

    private const string ContentRoot = "../../../../../../content";

    private static readonly JsonSerializerOptions PermissiveOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Every category present under content/ must appear here. "schemas" is the
    // only directory intentionally excluded (JSON Schema definitions, not game
    // content) and is skipped by CategoryOf returning null.
    private static readonly HashSet<string> ValidatedCategories = new()
    {
        "classes", "enemies", "encounters", "factions", "synergies", "items",
        "loot", "npcs", "rumors", "schemes", "complications", "segments",
        "campaigns", "dungeons", "archives", "secrets", "dialogue"
    };

    private static string? CategoryOf(string relativePath)
    {
        var parts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;
        var category = parts[0];
        if (category == "schemas") return null; // not game content
        if (category == "campaigns" && parts.Length > 1 && parts[1] == "dungeons")
            return "dungeons";
        return category;
    }

    private static IEnumerable<string> AllContentFilePaths() => Directory
        .EnumerateFiles(ContentRoot, "*.json", SearchOption.AllDirectories)
        .OrderBy(f => f);

    public static IEnumerable<object[]> AllContentFiles => AllContentFilePaths()
        .Select(f => new object[] { Path.GetRelativePath(ContentRoot, f).Replace('\\', '/') });

    [Fact]
    public void EveryContentCategory_HasAValidator()
    {
        Assert.True(Directory.Exists(ContentRoot), $"Missing content root: {ContentRoot}");

        var orphans = AllContentFilePaths()
            .Select(f => Path.GetRelativePath(ContentRoot, f).Replace('\\', '/'))
            .Select(CategoryOf)
            .Where(c => c != null && !ValidatedCategories.Contains(c!))
            .Distinct()
            .ToArray();

        Assert.True(orphans.Length == 0,
            $"Content categories without a validator (add one to tools/content-pack and ValidatedCategories): {string.Join(", ", orphans)}");
    }

    [Theory]
    [MemberData(nameof(AllContentFiles))]
    public void EveryContentFile_RoutesToValidatorAndDeserializes(string relativePath)
    {
        var category = CategoryOf(relativePath);
        if (category == null) return; // schemas/ — intentionally skipped

        Assert.True(ValidatedCategories.Contains(category),
            $"No validator for category '{category}' ({relativePath})");

        var json = File.ReadAllText(Path.Combine(ContentRoot, relativePath));

        switch (category)
        {
            case "classes":
                Assert.NotNull(JsonSerializer.Deserialize<ClassDef>(json, PermissiveOptions));
                break;
            case "enemies":
                Assert.NotNull(JsonSerializer.Deserialize<EnemyDef>(json, PermissiveOptions));
                break;
            case "encounters":
            {
                var tableId = Path.GetFileNameWithoutExtension(relativePath);
                var registry = new EncounterTableRegistry();
                registry.LoadFromJson(tableId, json);
                Assert.NotNull(registry.Get(tableId));
                break;
            }
            case "factions":
                Assert.NotNull(JsonSerializer.Deserialize<FactionContentDef>(json, PermissiveOptions));
                break;
            case "synergies":
                Assert.NotNull(JsonSerializer.Deserialize<SynergyDef>(json, PermissiveOptions));
                break;
            case "items":
                Assert.NotEmpty(JsonSerializer.Deserialize<ItemDef[]>(json, PermissiveOptions)!);
                break;
            case "loot":
                Assert.NotNull(JsonSerializer.Deserialize<DungeonLootTableDef>(json, PermissiveOptions));
                break;
            case "npcs":
                Assert.NotEmpty(JsonSerializer.Deserialize<NpcDef[]>(json, PermissiveOptions)!);
                break;
            case "rumors":
                Assert.NotEmpty(JsonSerializer.Deserialize<RumorDef[]>(json, PermissiveOptions)!);
                break;
            case "schemes":
                Assert.NotNull(JsonSerializer.Deserialize<SchemeDef>(json, PermissiveOptions));
                break;
            case "complications":
                Assert.NotNull(JsonSerializer.Deserialize<ComplicationDef>(json, PermissiveOptions));
                break;
            case "segments":
                Assert.NotNull(JsonSerializer.Deserialize<RoomSegment>(json, PermissiveOptions));
                break;
            case "dungeons":
                Assert.NotNull(JsonSerializer.Deserialize<DungeonTemplate>(json, PermissiveOptions));
                break;
            case "campaigns":
                Assert.NotNull(JsonSerializer.Deserialize<CampaignConfig>(json, PermissiveOptions));
                break;
            case "archives":
            {
                var archive = JsonSerializer.Deserialize<RPC.Engine.Campaign.FamilyArchiveDef>(json, PermissiveOptions);
                Assert.NotNull(archive);
                Assert.False(string.IsNullOrEmpty(archive!.Id));
                Assert.False(string.IsNullOrEmpty(archive.FactionId));
                break;
            }
            case "secrets":
            {
                var secret = JsonSerializer.Deserialize<RPC.Engine.Dungeons.SecretDef>(json, PermissiveOptions);
                Assert.NotNull(secret);
                Assert.False(string.IsNullOrEmpty(secret!.Id));
                Assert.False(string.IsNullOrEmpty(secret.Type));
                break;
            }
            case "dialogue":
                Assert.NotEmpty(JsonSerializer.Deserialize<DialogueDef[]>(json, PermissiveOptions)!);
                break;
            default:
                Assert.Fail($"Unhandled validated category '{category}' for {relativePath}");
                break;
        }
    }
}
