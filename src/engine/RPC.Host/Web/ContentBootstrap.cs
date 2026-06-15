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
    List<FactionContentDef> FactionContent,
    DungeonLootTableRegistry LootTables,
    List<RoomSegment> Segments);

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

        return new HostContent(
            Catalog: catalog,
            ContentHash: contentHash,
            EncounterTables: LoadEncounterTables(catalog),
            ClassRegistry: LoadClassRegistry(catalog),
            ItemRegistry: LoadItemRegistry(catalog),
            Synergies: LoadSynergies(catalog),
            DungeonTemplates: LoadDungeonTemplates(catalog),
            FactionContent: LoadFactionContent(catalog),
            LootTables: LoadLootTables(catalog),
            Segments: LoadSegments(catalog));
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
                registry.LoadFromJson(json);
        }
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
                var items = JsonSerializer.Deserialize<ItemDef[]>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (items != null)
                {
                    foreach (var item in items)
                        registry.Register(item);
                }
            }
        }
        return registry;
    }

    private static List<FactionContentDef> LoadFactionContent(IContentCatalog catalog)
    {
        var defs = new List<FactionContentDef>();
        foreach (var file in catalog.EnumerateFiles("factions", "*.json"))
        {
            var json = catalog.GetString(file) ?? catalog.GetString($"factions/{Path.GetFileName(file)}");
            if (json != null)
            {
                var def = JsonSerializer.Deserialize<FactionContentDef>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true });
                if (def != null)
                    defs.Add(def);
            }
        }
        return defs;
    }

    /// <summary>
    /// Loads all room segments. Public so the host's segment hot-reload watcher can re-run it
    /// when files change on disk.
    /// </summary>
    public static List<RoomSegment> LoadSegments(IContentCatalog catalog)
    {
        var segments = new List<RoomSegment>();
        foreach (var dir in new[] { "segments", "segments/broken-engine", "segments/bloom-site", "segments/boneyard", "segments/sealed-vault", "segments/settlement-gone-wrong", "segments/ossuary", "segments/contested-ruin", "segments/underway" })
        {
            foreach (var file in catalog.EnumerateFiles(dir, "*.json"))
            {
                var json = catalog.GetString(file) ?? catalog.GetString($"{dir.TrimEnd('/')}/{Path.GetFileName(file)}");
                if (json != null)
                {
                    var segment = JsonSerializer.Deserialize<RoomSegment>(json, _segmentOptions);
                    if (segment != null)
                        segments.Add(segment);
                }
            }
        }
        return segments;
    }

    private static Dictionary<string, DungeonTemplate> LoadDungeonTemplates(IContentCatalog catalog)
    {
        var templates = new Dictionary<string, DungeonTemplate>();
        foreach (var file in catalog.EnumerateFiles("campaigns/dungeons", "*.json"))
        {
            var json = catalog.GetString(file);
            if (json != null)
            {
                var template = JsonSerializer.Deserialize<DungeonTemplate>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true });
                if (template != null)
                    templates[template.Id] = template;
            }
        }
        return templates;
    }
}
