using RPC.Engine;
using RPC.Engine.Campaign;

namespace RPC.Tests;

public class EvidenceTests : IDisposable
{
    private readonly string _testSavePath;

    public EvidenceTests()
    {
        _testSavePath = Path.Combine(Path.GetTempPath(), $"test_evidence_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testSavePath))
            File.Delete(_testSavePath);
    }

    [Fact]
    public void AddEvidence_IncrementsCounter()
    {
        var evidence = new EvidenceState();
        var result = evidence.AddEvidence("bureau", "document", 2);

        Assert.Equal("bureau", result.FactionId);
        Assert.Equal(2, result.Amount);
        Assert.Equal(2, result.NewValue);
        Assert.Equal("document", result.Source);
        Assert.Equal(0, result.ThresholdReached);
    }

    [Fact]
    public void GetThreshold_ReturnsCorrectLevel()
    {
        var evidence = new EvidenceState();
        evidence.AddEvidence("bureau", "test", 2);
        Assert.Equal(0, evidence.GetThreshold("bureau"));

        evidence.AddEvidence("bureau", "test", 1);
        Assert.Equal(3, evidence.GetThreshold("bureau"));

        evidence.AddEvidence("bureau", "test", 2);
        Assert.Equal(5, evidence.GetThreshold("bureau"));

        evidence.AddEvidence("bureau", "test", 2);
        Assert.Equal(7, evidence.GetThreshold("bureau"));

        evidence.AddEvidence("bureau", "test", 3);
        Assert.Equal(10, evidence.GetThreshold("bureau"));
    }

    [Fact]
    public void HasReachedThreshold_AtExactThreshold_ReturnsTrue()
    {
        var evidence = new EvidenceState();
        evidence.AddEvidence("convocation", "test", 3);

        Assert.True(evidence.HasReachedThreshold("convocation", 3));
        Assert.False(evidence.HasReachedThreshold("convocation", 5));
    }

    [Fact]
    public void SuspectedFaction_FirstToReachThree()
    {
        var evidence = new EvidenceState();
        Assert.Null(evidence.SuspectedFaction);

        evidence.AddEvidence("fieldwright", "test", 3);
        Assert.Equal("fieldwright", evidence.SuspectedFaction);

        evidence.AddEvidence("inkblood", "test", 5);
        Assert.Equal("fieldwright", evidence.SuspectedFaction);
    }

    [Fact]
    public void AddEvidence_MultipleFactions_TracksSeparately()
    {
        var evidence = new EvidenceState();
        evidence.AddEvidence("bureau", "test", 5);
        evidence.AddEvidence("convocation", "test", 3);

        Assert.Equal(5, evidence.Counters["bureau"]);
        Assert.Equal(3, evidence.Counters["convocation"]);
        Assert.True(evidence.HasReachedThreshold("bureau", 5));
        Assert.False(evidence.HasReachedThreshold("convocation", 5));
    }

    [Fact]
    public void GameState_AddEvidence_EmitsActionLog()
    {
        var gs = new GameState(seed: 42);
        gs.AddEvidence("bureau", "dungeon_document");

        var entry = gs.ActionLog.FirstOrDefault(e => e.Type == "evidence_added");
        Assert.NotNull(entry);
        Assert.Equal("evidence", entry.Category);
        Assert.Equal("bureau", entry.Payload["factionId"]);
        Assert.Equal("1", entry.Payload["amount"]);
        Assert.Equal("1", entry.Payload["newValue"]);
        Assert.Equal("dungeon_document", entry.Payload["source"]);
        Assert.Equal("0", entry.Payload["threshold"]);
    }

    [Fact]
    public void GameState_AddEvidence_ThresholdCrossed_EmitsCorrectThreshold()
    {
        var gs = new GameState(seed: 42);
        gs.AddEvidence("bureau", "test", 3);

        var entry = gs.ActionLog.Last(e => e.Type == "evidence_added");
        Assert.Equal("3", entry.Payload["threshold"]);
    }

    [Fact]
    public void SaveLoad_PreservesEvidenceCounters()
    {
        var gs = new GameState(seed: 1);
        gs.AddEvidence("bureau", "test", 5);
        gs.AddEvidence("convocation", "test", 3);

        gs.SaveGame(_testSavePath);

        var gs2 = new GameState(seed: 2);
        var loaded = gs2.LoadGame(_testSavePath);
        Assert.True(loaded);

        Assert.Equal(5, gs2.Evidence.Counters["bureau"]);
        Assert.Equal(3, gs2.Evidence.Counters["convocation"]);
        Assert.Equal("bureau", gs2.Evidence.SuspectedFaction);
    }

    [Fact]
    public void Reset_ClearsEvidence()
    {
        var gs = new GameState(seed: 1);
        gs.AddEvidence("bureau", "test", 5);
        gs.Reset();

        Assert.Empty(gs.Evidence.Counters);
        Assert.Null(gs.Evidence.SuspectedFaction);
    }
}
