using System.Text.Json;

namespace RPC.Engine.Analytics;

/// <summary>
/// Local analytics tracker. Writes anonymized aggregates to analytics.json.
/// No PII, no session IDs, no free-text.
/// </summary>
public class AnalyticsTracker
{
    private readonly string _path;
    private AnalyticsData _data;

    public AnalyticsTracker(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RPC", "analytics.json");
        _data = Load();
    }

    public void RecordCampaignStart(string campaignId, string scheme, string[] partyClasses)
    {
        _data.CampaignsStarted++;
        _data.SchemesEncountered.Add(scheme);
        foreach (var cls in partyClasses)
            _data.ClassesPlayed.Add(cls);
        Save();
    }

    public void RecordSynergyDiscovered(string synergyId)
    {
        _data.SynergiesDiscovered.Add(synergyId);
        Save();
    }

    public void RecordSecretDiscovered(string secretId)
    {
        _data.SecretsDiscovered.Add(secretId);
        Save();
    }

    public void RecordDocumentRead(string documentId)
    {
        _data.DocumentsRead.Add(documentId);
        Save();
    }

    public void RecordBranchChosen(string classId, string branch, int level)
    {
        _data.BranchesChosen.Add($"{classId}:{branch}:{level}");
        Save();
    }

    public void RecordFactionEndState(string factionId, int reputation)
    {
        _data.FactionEndStates[factionId] = reputation;
        Save();
    }

    public void RecordCampaignEnd(bool mastermindExposed, bool schemeStopped, bool betrayal, int turns, int deaths)
    {
        _data.CampaignsCompleted++;
        if (mastermindExposed) _data.MastermindsExposed++;
        if (schemeStopped) _data.SchemesStopped++;
        if (betrayal) _data.Betrayals++;
        _data.TotalTurns += turns;
        _data.TotalDeaths += deaths;
        Save();
    }

    public void RecordOptionalDungeonUnlocked(string dungeonId)
    {
        _data.OptionalDungeonsUnlocked.Add(dungeonId);
        Save();
    }

    public AnalyticsData GetData() => _data;

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Reads the stored aggregates. A file that cannot be read or parsed is set aside rather than
    /// ignored: starting from empty and then saving over it would destroy the player's whole
    /// analytics history on the next recorded event, turning a transient read problem into
    /// permanent data loss.
    /// </summary>
    private AnalyticsData Load()
    {
        if (!File.Exists(_path)) return new AnalyticsData();

        try
        {
            var json = File.ReadAllText(_path);
            var loaded = JsonSerializer.Deserialize<AnalyticsData>(json);
            if (loaded != null) return loaded;
            Quarantine("file did not contain analytics data");
        }
        catch (Exception ex)
        {
            Quarantine(ex.Message);
        }

        return new AnalyticsData();
    }

    private void Quarantine(string reason)
    {
        var quarantinePath = $"{_path}.corrupt.{DateTime.UtcNow:yyyyMMddTHHmmss}";
        try
        {
            File.Move(_path, quarantinePath, overwrite: true);
            Console.Error.WriteLine($"[Analytics] Unreadable analytics file ({reason}); moved to {quarantinePath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Analytics] Unreadable analytics file ({reason}) and it could not be set aside: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes via a temp file and a rename so a crash mid-write cannot leave a half-written file
    /// behind — the previous whole-file write could, and the truncated result then read back as
    /// corrupt. Analytics are recorded only at campaign and discovery milestones, so the durable
    /// flush costs nothing on any hot path.
    ///
    /// A write failure is reported but not thrown: analytics are incidental to play and must
    /// never take a run down. Reporting is what makes the difference between incidental and
    /// invisible.
    /// </summary>
    private void Save()
    {
        var tmpPath = _path + ".tmp";
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_data, WriteOptions);
            File.WriteAllText(tmpPath, json);
            using (var fs = new FileStream(tmpPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                fs.Flush(flushToDisk: true);
            }
            File.Move(tmpPath, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Analytics] Failed to write {_path}: {ex.Message}");
            TryDelete(tmpPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Analytics] Failed to clean up {path}: {ex.Message}");
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
