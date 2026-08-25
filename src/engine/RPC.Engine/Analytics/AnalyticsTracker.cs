using System.Text.Json;

namespace RPC.Engine.Analytics;

/// <summary>
/// Local analytics tracker. Accumulates anonymized aggregates in memory and persists them to
/// analytics.json at campaign milestones. No PII, no session IDs, no free-text.
/// <para>
/// Persistence is opt-in by construction: a tracker built without a path never touches disk. That
/// keeps headless tests — which build a <see cref="GameState"/> per case, in parallel — off the
/// shared per-user analytics file, mirroring <c>GameState.MetaPersistenceEnabled</c>.
/// </para>
/// </summary>
public class AnalyticsTracker
{
    private readonly string? _path;
    private AnalyticsData _data;
    private bool _dirty;

    /// <summary>Per-user analytics file the desktop host records to.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RPC", "analytics.json");

    /// <summary>
    /// Build a tracker. A null <paramref name="path"/> yields an in-memory tracker that neither
    /// reads nor writes disk; a path both seeds the aggregates from that file and makes it the
    /// <see cref="Flush"/> target.
    /// </summary>
    public AnalyticsTracker(string? path = null)
    {
        _path = path;
        _data = _path is null ? new AnalyticsData() : Load(_path);
    }

    public void RecordCampaignStart(string campaignId, string scheme, string[] partyClasses)
    {
        _data.CampaignsStarted++;
        _data.SchemesEncountered.Add(scheme);
        foreach (var cls in partyClasses)
            _data.ClassesPlayed.Add(cls);
        _dirty = true;
        Flush(); // campaign start is a milestone: make the run durable up front
    }

    public void RecordSynergyDiscovered(string synergyId)
    {
        _data.SynergiesDiscovered.Add(synergyId);
        _dirty = true;
    }

    public void RecordSecretDiscovered(string secretId)
    {
        _data.SecretsDiscovered.Add(secretId);
        _dirty = true;
    }

    public void RecordDocumentRead(string documentId)
    {
        _data.DocumentsRead.Add(documentId);
        _dirty = true;
    }

    public void RecordBranchChosen(string classId, string branch, int level)
    {
        _data.BranchesChosen.Add($"{classId}:{branch}:{level}");
        _dirty = true;
    }

    public void RecordFactionEndState(string factionId, int reputation)
    {
        _data.FactionEndStates[factionId] = reputation;
        _dirty = true;
    }

    public void RecordCampaignEnd(bool mastermindExposed, bool schemeStopped, bool betrayal, int turns, int deaths)
    {
        _data.CampaignsCompleted++;
        if (mastermindExposed) _data.MastermindsExposed++;
        if (schemeStopped) _data.SchemesStopped++;
        if (betrayal) _data.Betrayals++;
        _data.TotalTurns += turns;
        _data.TotalDeaths += deaths;
        _dirty = true;
        Flush(); // campaign end is the milestone the aggregates exist for
    }

    public void RecordOptionalDungeonUnlocked(string dungeonId)
    {
        _data.OptionalDungeonsUnlocked.Add(dungeonId);
        _dirty = true;
    }

    public AnalyticsData GetData() => _data;

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Reads the stored aggregates. A file that cannot be read or parsed is set aside rather than
    /// ignored: starting from empty and then saving over it would destroy the player's whole
    /// analytics history on the next recorded event, turning a transient read problem into
    /// permanent data loss.
    /// </summary>
    private AnalyticsData Load(string path)
    {
        if (!File.Exists(path)) return new AnalyticsData();

        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<AnalyticsData>(json);
            if (loaded != null) return loaded;
            Quarantine(path, "file did not contain analytics data");
        }
        catch (Exception ex)
        {
            Quarantine(path, ex.Message);
        }

        return new AnalyticsData();
    }

    private static void Quarantine(string path, string reason)
    {
        try
        {
            var quarantinePath = AtomicFile.Quarantine(path, "corrupt");
            Console.Error.WriteLine($"[Analytics] Unreadable analytics file ({reason}); moved to {quarantinePath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Analytics] Unreadable analytics file ({reason}) and it could not be set aside: {ex.Message}");
        }
    }

    /// <summary>
    /// Persist the accumulated aggregates if anything changed since the last write. A no-op for an
    /// in-memory tracker (null path) and for a clean one, so callers may flush freely.
    /// <para>
    /// A write failure is reported but not thrown: analytics are incidental to play and must never
    /// take a run down. Reporting is what makes the difference between incidental and invisible.
    /// </para>
    /// </summary>
    public void Flush()
    {
        if (_path is null || !_dirty) return;

        try
        {
            AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(_data, WriteOptions));
            _dirty = false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Analytics] Failed to write {_path}: {ex.Message}");
        }
    }
}

public class AnalyticsData
{
    public int CampaignsStarted { get; set; }
    public int CampaignsCompleted { get; set; }
    public int MastermindsExposed { get; set; }
    public int SchemesStopped { get; set; }
    public int Betrayals { get; set; }
    public int TotalTurns { get; set; }
    public int TotalDeaths { get; set; }
    public HashSet<string> SynergiesDiscovered { get; set; } = new();
    public HashSet<string> SecretsDiscovered { get; set; } = new();
    public HashSet<string> DocumentsRead { get; set; } = new();
    public HashSet<string> SchemesEncountered { get; set; } = new();
    public HashSet<string> ClassesPlayed { get; set; } = new();
    public HashSet<string> BranchesChosen { get; set; } = new();
    public HashSet<string> OptionalDungeonsUnlocked { get; set; } = new();
    public Dictionary<string, int> FactionEndStates { get; set; } = new();
}
