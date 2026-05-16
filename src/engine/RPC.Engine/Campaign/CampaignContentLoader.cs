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
