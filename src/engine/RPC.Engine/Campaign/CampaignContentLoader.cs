using System.Text.Json;
using RPC.Engine.Content;

namespace RPC.Engine.Campaign;

public static class CampaignContentLoader
{
    private static readonly JsonSerializerOptions JsonOptions = ContentJsonOptions.Standard;

    public static List<SchemeDef> LoadSchemes(string? contentDir = null)
    {
        var dir = contentDir ?? FindContentDir("schemes");
        if (dir == null || !Directory.Exists(dir))
            return new List<SchemeDef>();

        var defs = new List<SchemeDef>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(f => f))
        {
            var json = File.ReadAllText(file);
            var def = JsonSerializer.Deserialize<SchemeDef>(json, JsonOptions);
            if (def != null)
                defs.Add(def);
        }
        return defs;
    }

    public static List<ComplicationDef> LoadComplications(string? contentDir = null)
    {
        var dir = contentDir ?? FindContentDir("complications");
        if (dir == null || !Directory.Exists(dir))
            return new List<ComplicationDef>();

        var defs = new List<ComplicationDef>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(f => f))
        {
            var json = File.ReadAllText(file);
            var def = JsonSerializer.Deserialize<ComplicationDef>(json, JsonOptions);
            if (def != null)
                defs.Add(def);
        }
        return defs;
    }

    /// <summary>
    /// Catalog-driven scheme load. Used by the host composition root (which picks the
    /// pack/loose <see cref="IContentCatalog"/>) so the engine never infers a content
    /// directory off the filesystem on the production path.
    /// </summary>
    public static List<SchemeDef> LoadSchemes(IContentCatalog catalog)
    {
        var defs = new List<SchemeDef>();
        foreach (var file in catalog.EnumerateFiles("schemes", "*.json"))
        {
            var json = catalog.GetString(file) ?? catalog.GetString($"schemes/{Path.GetFileName(file)}");
            if (json == null)
                continue;
            var def = JsonSerializer.Deserialize<SchemeDef>(json, JsonOptions);
            if (def != null)
                defs.Add(def);
        }
        return defs;
    }

    /// <summary>Catalog-driven complication load. See <see cref="LoadSchemes(IContentCatalog)"/>.</summary>
    public static List<ComplicationDef> LoadComplications(IContentCatalog catalog)
    {
        var defs = new List<ComplicationDef>();
        foreach (var file in catalog.EnumerateFiles("complications", "*.json"))
        {
            var json = catalog.GetString(file) ?? catalog.GetString($"complications/{Path.GetFileName(file)}");
            if (json == null)
                continue;
            var def = JsonSerializer.Deserialize<ComplicationDef>(json, JsonOptions);
            if (def != null)
                defs.Add(def);
        }
        return defs;
    }

    public static SchemeDef? GetSchemeById(string id, string? contentDir = null)
    {
        return LoadSchemes(contentDir).FirstOrDefault(s => s.Id == id);
    }

    public static ComplicationDef? GetComplicationById(string id, string? contentDir = null)
    {
        return LoadComplications(contentDir).FirstOrDefault(c => c.Id == id);
    }

    private static string? FindContentDir(string subDir)
    {
        var baseDir = AppContext.BaseDirectory;
        for (int ups = 0; ups <= 8; ups++)
        {
            var parts = new List<string> { baseDir };
            for (int i = 0; i < ups; i++) parts.Add("..");
            parts.AddRange(new[] { "content", subDir });
            var candidate = Path.GetFullPath(Path.Combine(parts.ToArray()));
            if (Directory.Exists(candidate))
                return candidate;
        }
        return null;
    }
}
