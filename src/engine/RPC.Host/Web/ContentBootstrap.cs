using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using RPC.Content;
using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Content;
using RPC.Engine.Dungeons;
using RPC.Engine.Inventory;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Town;

namespace RPC.Host.Web;

/// <summary>
/// All content registries loaded from a content pack at host startup. Produced by
/// <see cref="ContentBootstrap.Load"/> so the <see cref="GameServer"/> ctor can wire the
/// game state without owning the loading logic itself.
/// </summary>
internal sealed record HostContent(
    IContentCatalog Catalog,
    string? ContentHash,
    EncounterTableRegistry EncounterTables,
    ClassRegistry ClassRegistry,
    ItemRegistry ItemRegistry,
    SynergyRegistry Synergies,
    Dictionary<string, DungeonTemplate> DungeonTemplates,
    DungeonContentSet DungeonContent,
    List<FactionContentDef> FactionContent,
    DungeonLootTableRegistry LootTables,
    List<RoomSegment> Segments,
    CampaignContentRegistry CampaignContent,
    SecretRegistry Secrets,
    ArchiveRegistry Archives);

/// <summary>
/// Locates the content pack and loads every registry the host needs. Keeps the file/JSON
/// concerns out of <see cref="GameServer"/> so the composition root just calls
/// <see cref="Load"/> and wires the result.
/// </summary>
internal static class ContentBootstrap
{
    private static readonly JsonSerializerOptions _segmentOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static HostContent Load()
    {
        var rpkPath = FindRpkPath();
        var catalog = rpkPath != null ? (IContentCatalog)new RpkCatalog(rpkPath) : new FileSystemCatalog();
        var contentHash = ReadContentHash(rpkPath);
        LogContentPackInfo(rpkPath, contentHash);

        var encounterTables = LoadEncounterTables(catalog);
        var dungeonTemplates = LoadDungeonTemplates(catalog);
        var dungeonContent = new DungeonContentSet(dungeonTemplates);
        var segments = LoadSegments(catalog, dungeonContent.SegmentDirectories);

        // Fail-fast: a content pack that wires a dungeon template to missing segments, an unknown
        // encounter table, or no display name/watcher path must not start the host with silently
        // broken dungeons.
        dungeonContent.Validate(segments, encounterTables);

        return new HostContent(
            Catalog: catalog,
            ContentHash: contentHash,
            EncounterTables: encounterTables,
            ClassRegistry: LoadClassRegistry(catalog),
            ItemRegistry: LoadItemRegistry(catalog),
            Synergies: LoadSynergies(catalog),
            DungeonTemplates: dungeonTemplates,
            DungeonContent: dungeonContent,
            FactionContent: FactionContentLoader.LoadAll(catalog),
            LootTables: LoadLootTables(catalog),
            Segments: segments,
            CampaignContent: CampaignContentRegistry.FromCatalog(catalog),
            Secrets: LoadSecrets(catalog),
            Archives: LoadArchives(catalog));
    }

    private static string? FindRpkPath()
    {
        var baseDir = AppContext.BaseDirectory;
        for (int ups = 0; ups <= 8; ups++)
        {
            var parts = new List<string> { baseDir };
            for (int i = 0; i < ups; i++) parts.Add("..");
            parts.Add("content.rpk");
            var candidate = Path.GetFullPath(Path.Combine(parts.ToArray()));
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static string? ReadContentHash(string? rpkPath)
    {
        if (rpkPath == null) return null;
        var manifestPath = Path.Combine(Path.GetDirectoryName(rpkPath)!, "manifest.json");
        if (!File.Exists(manifestPath)) return null;
        try
        {
            var json = File.ReadAllText(manifestPath);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("contentHash").GetString();
        }
        catch
        {
            return null;
        }
    }

    private static void LogContentPackInfo(string? rpkPath, string? contentHash)
    {
        if (rpkPath == null)
        {
            Console.WriteLine("[Content] Running from loose files (no .rpk found)");
            return;
        }

        var manifestPath = Path.Combine(Path.GetDirectoryName(rpkPath)!, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            Console.WriteLine($"[Content] Loaded pack: {rpkPath} (no manifest found)");
            return;
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var version = root.GetProperty("version").GetInt32();
            var hash = root.GetProperty("contentHash").GetString();
            var fileCount = root.GetProperty("files").GetArrayLength();
            Console.WriteLine($"[Content] Loaded pack v{version}, hash {hash?[..16]}.., {fileCount} files ({rpkPath})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Content] Loaded pack: {rpkPath} (manifest read failed: {ex.Message})");
        }
    }

    private static EncounterTableRegistry LoadEncounterTables(IContentCatalog catalog)
    {
        var registry = new EncounterTableRegistry();
        foreach (var file in catalog.EnumerateFiles("encounters", "*.json"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var json = catalog.GetString(file) ?? catalog.GetString($"encounters/{Path.GetFileName(file)}");
            if (json != null)
                registry.LoadFromJson(id, json);
        }
        return registry;
    }

    private static DungeonLootTableRegistry LoadLootTables(IContentCatalog catalog)
    {
        var registry = new DungeonLootTableRegistry();
        foreach (var file in catalog.EnumerateFiles("loot", "*.json"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var json = catalog.GetString(file) ?? catalog.GetString($"loot/{Path.GetFileName(file)}");
            if (json != null)
                registry.LoadFromJson(id, json);
        }
        return registry;
    }

    private static ClassRegistry LoadClassRegistry(IContentCatalog catalog)
    {
        var registry = new ClassRegistry();
        foreach (var file in catalog.EnumerateFiles("classes", "*.json"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var json = catalog.GetString(file) ?? catalog.GetString($"classes/{Path.GetFileName(file)}");
            if (json != null)
                registry.LoadFromJson(id, json);
        }
        return registry;
    }

    private static SynergyRegistry LoadSynergies(IContentCatalog catalog)
    {
        var registry = new SynergyRegistry();
        foreach (var file in catalog.EnumerateFiles("synergies", "*.json"))
        {
            var json = catalog.GetString(file) ?? catalog.GetString($"synergies/{Path.GetFileName(file)}");
            if (json != null)
                registry.LoadFromJson(json, Path.GetFileName(file));
        }
        return registry;
    }

    private static SecretRegistry LoadSecrets(IContentCatalog catalog)
    {
        var registry = new SecretRegistry();
        registry.LoadFromCatalog(catalog);
        return registry;
    }

    private static ArchiveRegistry LoadArchives(IContentCatalog catalog)
    {
        var registry = new ArchiveRegistry();
        registry.LoadFromCatalog(catalog);
        return registry;
    }

    private static ItemRegistry LoadItemRegistry(IContentCatalog catalog)
    {
        var registry = new ItemRegistry();
        foreach (var file in catalog.EnumerateFiles("items", "*.json"))
        {
            var json = catalog.GetString(file) ?? catalog.GetString($"items/{Path.GetFileName(file)}");
            if (json != null)
            {
                var items = Deserialize<ItemDef[]>(json, CaseInsensitive, file);
                if (items != null)
                {
                    foreach (var item in items)
                        registry.Register(item);
                }
            }
        }
        return registry;
    }

    /// <summary>
    /// Loads all room segments from the shared root plus the directories the dungeon templates
    /// declare. Public so the host's segment hot-reload watcher can re-run it when files change.
    ///
    /// The directory list comes from the templates rather than being written out here, so it
    /// cannot drift from the set the hot-reload watcher observes — both now read
    /// <see cref="DungeonContentSet.SegmentDirectories"/>. Adding a dungeon means adding its
    /// template; nothing here needs to change.
    /// </summary>
    public static List<RoomSegment> LoadSegments(IContentCatalog catalog, IEnumerable<string> segmentDirectories)
    {
        var segments = new List<RoomSegment>();
        var directories = new List<string> { "segments" };
        directories.AddRange(segmentDirectories
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.TrimEnd('/'))
            .Where(d => !string.Equals(d, "segments", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal));

        foreach (var dir in directories)
        {
            foreach (var file in catalog.EnumerateFiles(dir, "*.json"))
            {
                var json = catalog.GetString(file) ?? catalog.GetString($"{dir.TrimEnd('/')}/{Path.GetFileName(file)}");
                if (json == null) continue;

                var segment = Deserialize<RoomSegment>(json, _segmentOptions, file);
                if (segment != null)
                    segments.Add(segment);
            }
        }
        return segments;
    }

    /// <summary>
    /// Deserializes a content file, naming the file when it fails. The raw JsonException reports a
    /// path and offset but not which of several hundred content files produced it, which is the
    /// one thing needed to fix it.
    /// </summary>
    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions CaseInsensitiveLenient = new() { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true };

    private static T? Deserialize<T>(string json, JsonSerializerOptions options, string file)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, options);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Content file '{file}' is not valid {typeof(T).Name} JSON: {ex.Message}", ex);
        }
    }

    private static Dictionary<string, DungeonTemplate> LoadDungeonTemplates(IContentCatalog catalog)
    {
        var templates = new Dictionary<string, DungeonTemplate>();
        foreach (var file in catalog.EnumerateFiles("campaigns/dungeons", "*.json"))
        {
            var json = catalog.GetString(file);
            if (json != null)
            {
                var template = Deserialize<DungeonTemplate>(json, CaseInsensitiveLenient, file);
                if (template is null) continue;
                if (string.IsNullOrWhiteSpace(template.Id))
                    throw new InvalidOperationException($"Dungeon template '{file}' has no id.");
                if (templates.TryGetValue(template.Id, out var existing))
                    Console.Error.WriteLine($"[Content] Dungeon template id '{template.Id}' is declared twice; '{file}' replaces the earlier '{existing.Name}'.");
                templates[template.Id] = template;
            }
        }
        return templates;
    }
}
