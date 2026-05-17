using System.Text.Json;
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

        var tracker2 = new AnalyticsTracker(_tempPath);
        var data = tracker2.GetData();
        Assert.Equal(1, data.CampaignsStarted);
        Assert.Contains("test_synergy", data.SynergiesDiscovered);
    }
}
