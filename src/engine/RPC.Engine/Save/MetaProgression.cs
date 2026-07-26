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

    /// <summary>
    /// Read the stored meta-progression, or a fresh one when there is nothing to read.
    /// <para>
    /// An unreadable file is set aside rather than ignored. Returning a fresh instance and then
    /// saving over the original destroyed every run the player had ever completed — and silently,
    /// because a fresh meta is indistinguishable from a first launch. Quarantining preserves the
    /// file under a timestamped name and says so, so a transient read problem stays transient.
    /// </para>
    /// </summary>
    public static MetaProgression Load(string? path = null)
    {
        var p = path ?? DefaultPath;
        if (!File.Exists(p)) return new MetaProgression();

        try
        {
            var meta = JsonSerializer.Deserialize<MetaProgression>(File.ReadAllText(p), JsonOptions);
            if (meta != null) return meta;
            Quarantine(p, "file did not contain meta-progression");
        }
        catch (Exception ex)
        {
            Quarantine(p, ex.Message);
        }

        return new MetaProgression();
    }

    /// <summary>
    /// Durably replace the meta-progression file. The previous whole-file write could be
    /// interrupted, and the truncated result then read back as corrupt — which, before the
    /// quarantine above, meant the player's whole cross-run history was discarded on next launch.
    /// </summary>
    public static void Save(MetaProgression meta, string? path = null)
        => AtomicFile.WriteAllText(path ?? DefaultPath, JsonSerializer.Serialize(meta, JsonOptions));

    private static void Quarantine(string path, string reason)
    {
        try
        {
            var quarantinePath = AtomicFile.Quarantine(path, "corrupt");
            Console.Error.WriteLine($"[Meta] Unreadable meta-progression ({reason}); moved to {quarantinePath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Meta] Unreadable meta-progression ({reason}) and it could not be set aside: {ex.Message}");
        }
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
