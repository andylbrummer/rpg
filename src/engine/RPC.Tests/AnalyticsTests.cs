using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Analytics;

namespace RPC.Tests;

public class AnalyticsTests : IDisposable
{
    private readonly string _tempPath;

    public AnalyticsTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"rpc_analytics_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath))
            File.Delete(_tempPath);
        // Also sweep the sidecars a failed run can strand next to it (unique-named temps, and any
        // quarantine copy), so a failure does not leave litter in the shared temp directory.
        foreach (var sidecar in Directory.GetFiles(Path.GetDirectoryName(_tempPath)!, $"{Path.GetFileName(_tempPath)}.*"))
            File.Delete(sidecar);
    }

    [Fact]
    public void RecordCampaignStart_IncrementsCounter()
    {
        var tracker = new AnalyticsTracker(_tempPath);
        tracker.RecordCampaignStart("test", "the_vault", new[] { "bonewarden", "hollow" });

        var data = tracker.GetData();
        Assert.Equal(1, data.CampaignsStarted);
        Assert.Contains("the_vault", data.SchemesEncountered);
        Assert.Contains("bonewarden", data.ClassesPlayed);
        Assert.Contains("hollow", data.ClassesPlayed);
    }

    [Fact]
    public void RecordSynergyDiscovered_AddsToSet()
    {
        var tracker = new AnalyticsTracker(_tempPath);
        tracker.RecordSynergyDiscovered("bonewarden_hollow_bone_shiv");
        tracker.RecordSynergyDiscovered("bonewarden_hollow_bone_shiv"); // idempotent

        var data = tracker.GetData();
        Assert.Single(data.SynergiesDiscovered);
        Assert.Contains("bonewarden_hollow_bone_shiv", data.SynergiesDiscovered);
    }

    [Fact]
    public void RecordCampaignEnd_Aggregates()
    {
        var tracker = new AnalyticsTracker(_tempPath);
        tracker.RecordCampaignEnd(mastermindExposed: true, schemeStopped: true, betrayal: false, turns: 15, deaths: 2);
        tracker.RecordCampaignEnd(mastermindExposed: false, schemeStopped: true, betrayal: true, turns: 20, deaths: 1);

        var data = tracker.GetData();
        Assert.Equal(2, data.CampaignsCompleted);
        Assert.Equal(1, data.MastermindsExposed);
        Assert.Equal(2, data.SchemesStopped);
        Assert.Equal(1, data.Betrayals);
        Assert.Equal(35, data.TotalTurns);
        Assert.Equal(3, data.TotalDeaths);
    }

    [Fact]
    public void Persistence_RoundTrip()
    {
        var tracker1 = new AnalyticsTracker(_tempPath);
        tracker1.RecordCampaignStart("test", "the_vault", new[] { "bonewarden" });
        tracker1.RecordSynergyDiscovered("test_synergy");
        tracker1.Flush();

        var tracker2 = new AnalyticsTracker(_tempPath);
        var data = tracker2.GetData();
        Assert.Equal(1, data.CampaignsStarted);
        Assert.Contains("test_synergy", data.SynergiesDiscovered);
    }

    /// <summary>
    /// Campaign start and end are the milestones the aggregates exist for, so they are durable
    /// without an explicit flush. Everything in between is coalesced.
    /// </summary>
    [Fact]
    public void CampaignMilestones_Persist_Without_An_Explicit_Flush()
    {
        var tracker = new AnalyticsTracker(_tempPath);
        tracker.RecordCampaignStart("test", "the_vault", new[] { "bonewarden" });

        Assert.Equal(1, new AnalyticsTracker(_tempPath).GetData().CampaignsStarted);

        tracker.RecordCampaignEnd(mastermindExposed: true, schemeStopped: false, betrayal: false, turns: 3, deaths: 0);

        Assert.Equal(1, new AnalyticsTracker(_tempPath).GetData().CampaignsCompleted);
    }

    /// <summary>
    /// Discovery events fire from inside the exploration command path, under the game-state lock.
    /// Each one used to serialize the whole aggregate blob and fsync it — milliseconds of blocking
    /// disk I/O per secret found. They must now coalesce into a single write.
    /// </summary>
    [Fact]
    public void Discovery_Events_Do_Not_Touch_Disk_Until_Flushed()
    {
        var tracker = new AnalyticsTracker(_tempPath);
        tracker.RecordSecretDiscovered("secret-a");
        tracker.RecordDocumentRead("doc-a");
        tracker.RecordOptionalDungeonUnlocked("dungeon-a");

        Assert.False(File.Exists(_tempPath));

        tracker.Flush();

        var reloaded = new AnalyticsTracker(_tempPath).GetData();
        Assert.Contains("secret-a", reloaded.SecretsDiscovered);
        Assert.Contains("doc-a", reloaded.DocumentsRead);
        Assert.Contains("dungeon-a", reloaded.OptionalDungeonsUnlocked);
    }

    /// <summary>
    /// A tracker with no path is the engine default, so a headless <see cref="GameState"/> must not
    /// read or write the shared per-user analytics file just by existing.
    /// </summary>
    [Fact]
    public void A_Pathless_Tracker_Never_Touches_Disk()
    {
        var before = Directory.Exists(Path.GetDirectoryName(AnalyticsTracker.DefaultPath))
            ? Directory.GetFiles(Path.GetDirectoryName(AnalyticsTracker.DefaultPath)!).Length
            : 0;

        var gs = new GameState(seed: 1);
        gs.DiscoverSecret("breakable_wall", "secret-headless");
        gs.Analytics.Flush();

        Assert.Contains("secret-headless", gs.Analytics.GetData().SecretsDiscovered);

        var after = Directory.Exists(Path.GetDirectoryName(AnalyticsTracker.DefaultPath))
            ? Directory.GetFiles(Path.GetDirectoryName(AnalyticsTracker.DefaultPath)!).Length
            : 0;
        Assert.Equal(before, after);
    }

    /// <summary>
    /// Concurrent trackers on one path used to share a fixed "analytics.json.tmp": the second
    /// writer truncated the temp file while the first renamed it into place, so a partial file
    /// landed at the real path and every later read quarantined it — destroying the player's
    /// history. A reader must always observe a whole, parseable file.
    /// </summary>
    [Fact]
    public async Task Concurrent_Writers_Never_Leave_A_Partial_File_Behind()
    {
        var trackers = Enumerable.Range(0, 8)
            .Select(_ => new AnalyticsTracker(_tempPath))
            .ToArray();

        var readFailures = 0;
        var stop = false;
        var reader = Task.Run(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                if (!File.Exists(_tempPath)) continue;
                try
                {
                    var json = File.ReadAllText(_tempPath);
                    if (json.Length > 0 && JsonSerializer.Deserialize<AnalyticsData>(json) is null)
                        Interlocked.Increment(ref readFailures);
                }
                catch (JsonException) { Interlocked.Increment(ref readFailures); }
                catch (IOException) { /* the rename raced this open; not a partial-file defect */ }
            }
        });

        Parallel.For(0, trackers.Length, i =>
        {
            for (int n = 0; n < 40; n++)
            {
                trackers[i].RecordSecretDiscovered($"secret-{i}-{n}");
                trackers[i].Flush();
            }
        });

        Volatile.Write(ref stop, true);
        await reader;

        Assert.Equal(0, readFailures);
        Assert.NotNull(JsonSerializer.Deserialize<AnalyticsData>(File.ReadAllText(_tempPath)));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(_tempPath)!, $"{Path.GetFileName(_tempPath)}.corrupt.*"));
    }
}
