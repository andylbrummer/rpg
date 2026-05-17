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

    private AnalyticsData Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<AnalyticsData>(json) ?? new AnalyticsData();
            }
        }
        catch { }
        return new AnalyticsData();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch { }
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
    public HashSet<string> SchemesEncountered { get; set; } = new();
    public HashSet<string> ClassesPlayed { get; set; } = new();
    public HashSet<string> BranchesChosen { get; set; } = new();
    public HashSet<string> OptionalDungeonsUnlocked { get; set; } = new();
    public Dictionary<string, int> FactionEndStates { get; set; } = new();
}
