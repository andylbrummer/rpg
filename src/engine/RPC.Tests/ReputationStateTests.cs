using RPC.Engine;

namespace RPC.Tests;

public class ReputationStateTests : IDisposable
{
    private readonly string _testSavePath;

    public ReputationStateTests()
    {
        _testSavePath = Path.Combine(Path.GetTempPath(), $"test_rep_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testSavePath))
            File.Delete(_testSavePath);
    }

    [Fact]
    public void ApplyDelta_PositiveDelta_IncreasesValue()
    {
        var rep = new ReputationState();
        var changes = rep.ApplyDelta("unaffiliated", 5, "test");

        Assert.Equal(5, rep["unaffiliated"]);
        Assert.Single(changes);
        Assert.Equal("unaffiliated", changes[0].FactionId);
        Assert.Equal(5, changes[0].Delta);
        Assert.Equal(5, changes[0].NewValue);
        Assert.Equal("test", changes[0].Source);
    }

    [Fact]
    public void ApplyDelta_NegativeDelta_DecreasesValue()
    {
        var rep = new ReputationState();
        rep["unaffiliated"] = 10;
        var changes = rep.ApplyDelta("unaffiliated", -3, "test");

        Assert.Equal(7, rep["unaffiliated"]);
        Assert.Single(changes);
        Assert.Equal(-3, changes[0].Delta);
        Assert.Equal(7, changes[0].NewValue);
    }

    [Fact]
    public void ApplyDelta_DeltaExceeds100_ClampedTo100()
    {
        var rep = new ReputationState();
        var changes = rep.ApplyDelta("bureau", 150, "test");

        Assert.Equal(100, rep["bureau"]);
        Assert.Equal(100, changes[0].Delta);
    }

    [Fact]
    public void ApplyDelta_DeltaBelowNegative100_ClampedToNegative100()
    {
        var rep = new ReputationState();
        var changes = rep.ApplyDelta("bureau", -150, "test");

        Assert.Equal(-100, rep["bureau"]);
        Assert.Equal(-100, changes[0].Delta);
    }

    [Fact]
    public void ApplyDelta_ValueAtCap_ExtraDeltaDiscardedButPropagated()
    {
        var rep = new ReputationState();
        rep["bureau"] = 98;
        rep["convocation"] = 0;

        var changes = rep.ApplyDelta("bureau", 10, "test");

        Assert.Equal(100, rep["bureau"]);
        Assert.Equal(-4, rep["convocation"]);
        Assert.Equal(2, changes.Count);
        Assert.Equal(2, changes[0].Delta);
        Assert.Equal(100, changes[0].NewValue);
        Assert.Equal(-4, changes[1].Delta);
        Assert.Equal(-4, changes[1].NewValue);
    }

    [Fact]
    public void ApplyDelta_BureauPlus5_PropagatesMinus2ToConvocation()
    {
        var rep = new ReputationState();
        var changes = rep.ApplyDelta("bureau", 5, "side_mission");

        Assert.Equal(5, rep["bureau"]);
        Assert.Equal(-2, rep["convocation"]);
        Assert.Equal(2, changes.Count);

        Assert.Equal("bureau", changes[0].FactionId);
        Assert.Equal(5, changes[0].Delta);

        Assert.Equal("convocation", changes[1].FactionId);
        Assert.Equal(-2, changes[1].Delta);
        Assert.Equal(-2, changes[1].NewValue);
        Assert.Equal("side_mission", changes[1].Source);
    }

    [Fact]
    public void ApplyDelta_FieldwrightPlus10_PropagatesMinus4ToInkblood()
    {
        var rep = new ReputationState();
        var changes = rep.ApplyDelta("fieldwright", 10, "test");

        Assert.Equal(10, rep["fieldwright"]);
        Assert.Equal(-4, rep["inkblood"]);
    }

    [Fact]
    public void ApplyDelta_ConvocationPlus5_PropagatesMinus2ToBureau()
    {
        var rep = new ReputationState();
        var changes = rep.ApplyDelta("convocation", 5, "test");

        Assert.Equal(5, rep["convocation"]);
        Assert.Equal(-2, rep["bureau"]);
    }

    [Fact]
    public void ApplyDelta_InkbloodPlus5_PropagatesMinus2ToFieldwright()
    {
        var rep = new ReputationState();
        var changes = rep.ApplyDelta("inkblood", 5, "test");

        Assert.Equal(5, rep["inkblood"]);
        Assert.Equal(-2, rep["fieldwright"]);
    }

    [Fact]
    public void ApplyDelta_NoOpposedPair_NoPropagation()
    {
        var rep = new ReputationState();
        var changes = rep.ApplyDelta("unaffiliated", 10, "test");

        Assert.Equal(10, rep["unaffiliated"]);
        Assert.Single(changes);
    }

    [Fact]
    public void ApplyDelta_OpposedPropagationRounded()
    {
        var rep = new ReputationState();
        var changes = rep.ApplyDelta("bureau", 3, "test");

        Assert.Equal(3, rep["bureau"]);
        Assert.Equal(-1, rep["convocation"]);
    }

    [Fact]
    public void ApplyDelta_OpposedAlreadyAtCap_Clamped()
    {
        var rep = new ReputationState();
        rep["convocation"] = -100;

        var changes = rep.ApplyDelta("bureau", 50, "test");

        Assert.Equal(50, rep["bureau"]);
        Assert.Equal(-100, rep["convocation"]);
        Assert.Equal(0, changes[1].Delta);
    }

    [Fact]
    public void GameState_ApplyReputationDelta_EmitsActionLog()
    {
        var gs = new GameState(seed: 42);
        gs.ApplyReputationDelta("bureau", 5, "side_mission");

        var entry = gs.ActionLog.FirstOrDefault(e => e.Payload["factionId"] == "bureau");
        Assert.NotNull(entry);
        Assert.Equal("faction", entry.Category);
        Assert.Equal("rep_changed", entry.Type);
        Assert.Equal("bureau", entry.Payload["factionId"]);
        Assert.Equal("5", entry.Payload["delta"]);
        Assert.Equal("5", entry.Payload["newValue"]);
        Assert.Equal("side_mission", entry.Payload["source"]);
    }

    [Fact]
    public void GameState_ApplyReputationDelta_OpposedEmitsSecondLogEntry()
    {
        var gs = new GameState(seed: 42);
        gs.ActionLog.Clear();

        gs.ApplyReputationDelta("bureau", 5, "side_mission");

        var entries = gs.ActionLog.Where(e => e.Type == "rep_changed").ToList();
        Assert.Equal(2, entries.Count);

        Assert.Equal("bureau", entries[0].Payload["factionId"]);
        Assert.Equal("convocation", entries[1].Payload["factionId"]);
        Assert.Equal("-2", entries[1].Payload["delta"]);
        Assert.Equal("-2", entries[1].Payload["newValue"]);
    }

    [Fact]
    public void SaveLoad_PreservesReputationValues()
    {
        var gs = new GameState(seed: 1);
        gs.Reputation["bureau"] = 42;
        gs.Reputation["convocation"] = -17;
        gs.Reputation["fieldwright"] = 33;
        gs.Reputation["inkblood"] = -8;

        gs.SaveGame(_testSavePath);

        var gs2 = new GameState(seed: 2);
        var loaded = gs2.LoadGame(_testSavePath);
        Assert.True(loaded);

        Assert.Equal(42, gs2.Reputation["bureau"]);
        Assert.Equal(-17, gs2.Reputation["convocation"]);
        Assert.Equal(33, gs2.Reputation["fieldwright"]);
        Assert.Equal(-8, gs2.Reputation["inkblood"]);
    }

    [Fact]
    public void Reset_ClearsReputation()
    {
        var gs = new GameState(seed: 1);
        gs.Reputation["bureau"] = 50;
        gs.Reset();

        Assert.Equal(0, gs.Reputation["bureau"]);
    }

    [Fact]
    public void ApplyDelta_PropagationRateZero_NoPropagation()
    {
        var rep = new ReputationState(propagationRate: 0.0);
        var changes = rep.ApplyDelta("bureau", 50, "test");

        Assert.Equal(50, rep["bureau"]);
        Assert.Equal(0, rep["convocation"]);
        Assert.Single(changes);
    }
}
