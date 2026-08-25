using System.Text.Json;

namespace RPC.Engine.Save;

/// <summary>
/// Cross-campaign meta-progression: persists between runs, separate from the per-run save. Tracks
/// the multi-run arc — dungeons conquered across all runs and accumulated faction power shifts.
/// </summary>
public class MetaProgression
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public int RunsCompleted { get; set; }
    public HashSet<string> ConqueredDungeons { get; set; } = new();
    /// <summary>Faction id -> accumulated reputation/power across runs.</summary>
    public Dictionary<string, int> FactionPower { get; set; } = new();
}

/// <summary>Loads/saves <see cref="MetaProgression"/> to a standalone meta-save file.</summary>
public static class MetaProgressionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RPC", "meta.json");

    public static MetaProgression Load(string? path = null)
    {
        var p = path ?? DefaultPath;
        try
        {
            if (!File.Exists(p)) return new MetaProgression();
            var meta = JsonSerializer.Deserialize<MetaProgression>(File.ReadAllText(p), JsonOptions);
            return meta ?? new MetaProgression();
        }
        catch
        {
            return new MetaProgression(); // corrupt/unreadable -> fresh meta
        }
    }

    public static void Save(MetaProgression meta, string? path = null)
    {
        var p = path ?? DefaultPath;
        var dir = Path.GetDirectoryName(p);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(p, JsonSerializer.Serialize(meta, JsonOptions));
    }

    /// <summary>
    /// Fold a finished run into the meta-progression: count the run, record every dungeon the party
    /// completed this run, and accumulate end-of-run faction reputation into faction power.
    /// </summary>
    public static MetaProgression RecordCampaignEnd(MetaProgression meta, GameState state)
    {
        meta.RunsCompleted++;

        foreach (var entry in state.ActionLog)
        {
            if (entry.Type == "dungeon_completed"
                && entry.Payload.TryGetValue("dungeonType", out var dt)
                && !string.IsNullOrEmpty(dt))
            {
                meta.ConqueredDungeons.Add(dt);
            }
        }

        foreach (var (faction, value) in state.Reputation)
        {
            meta.FactionPower[faction] = meta.FactionPower.GetValueOrDefault(faction) + value;
        }

        return meta;
    }
}
