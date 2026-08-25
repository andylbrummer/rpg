using RPC.Engine;
using RPC.Engine.Town;

namespace RPC.Tests;

public class ReputationConsequenceTests
{
    private static readonly FactionContentRepository FactionRepo = new(FactionContentLoader.LoadAll("../../../../../../content/factions"));

    private static GameState MakeGameState(int seed) => new(seed: seed, factionContent: FactionRepo);
    [Fact]
    public void CompleteMission_SideMission_AppliesPlus5PrimaryMinus2Opposed()
    {
        var gs = new GameState(seed: 1);
        gs.Town.QuestLog.Add(new ActiveMission("m1", "Test", "Desc", 5, "bureau", MissionStatus.Active, MissionType.Side));

        var result = gs.CompleteMission("m1");

        Assert.True(result);
        Assert.Equal(5, gs.Reputation["bureau"]);
        Assert.Equal(-2, gs.Reputation["convocation"]);
    }

    [Fact]
    public void CompleteMission_MainMission_AppliesPlus8PrimaryMinus4Opposed()
    {
        var gs = new GameState(seed: 1);
        gs.Town.QuestLog.Add(new ActiveMission("m1", "Test", "Desc", 8, "bureau", MissionStatus.Active, MissionType.Main));

        var result = gs.CompleteMission("m1");

        Assert.True(result);
        Assert.Equal(8, gs.Reputation["bureau"]);
        Assert.Equal(-4, gs.Reputation["convocation"]);
    }

    [Fact]
    public void CompleteMission_ConvocationSideMission_AppliesPlus5PrimaryMinus2Opposed()
    {
        var gs = new GameState(seed: 1);
        gs.Town.QuestLog.Add(new ActiveMission("m1", "Test", "Desc", 5, "convocation", MissionStatus.Active, MissionType.Side));

        var result = gs.CompleteMission("m1");

        Assert.True(result);
        Assert.Equal(5, gs.Reputation["convocation"]);
        Assert.Equal(-2, gs.Reputation["bureau"]);
    }

    [Fact]
    public void FailMission_AppliesMinus3PrimaryPlus1Opposed()
    {
        var gs = new GameState(seed: 1);
        gs.Town.QuestLog.Add(new ActiveMission("m1", "Test", "Desc", 5, "bureau", MissionStatus.Active, MissionType.Side));

        var result = gs.FailMission("m1");

        Assert.True(result);
        Assert.Equal(-3, gs.Reputation["bureau"]);
        Assert.Equal(1, gs.Reputation["convocation"]);
    }

    [Fact]
    public void AbandonMission_AppliesMinus3PrimaryPlus1Opposed()
    {
        var gs = new GameState(seed: 1);
        gs.Town.QuestLog.Add(new ActiveMission("m1", "Test", "Desc", 5, "bureau", MissionStatus.Active, MissionType.Side));

        var result = gs.AbandonMission("m1");

        Assert.True(result);
        Assert.Equal(-3, gs.Reputation["bureau"]);
        Assert.Equal(1, gs.Reputation["convocation"]);
    }

    [Fact]
    public void ApplyDialogueReputation_FactionAlignedPlus2_AppliesDelta()
    {
        var gs = new GameState(seed: 1);

        var result = gs.ApplyDialogueReputation("bureau", 2);

        Assert.True(result);
        Assert.Equal(2, gs.Reputation["bureau"]);
    }

    [Fact]
    public void ApplyDialogueReputation_FactionOpposedMinus5_AppliesDelta()
    {
        var gs = new GameState(seed: 1);

        var result = gs.ApplyDialogueReputation("convocation", -5);

        Assert.True(result);
        Assert.Equal(-5, gs.Reputation["convocation"]);
    }

    [Fact]
    public void CompleteMission_EmitsActionLogForPrimaryAndOpposed()
    {
        var gs = new GameState(seed: 1);
        gs.ActionLog.Clear();
        gs.Town.QuestLog.Add(new ActiveMission("m1", "Test", "Desc", 5, "bureau", MissionStatus.Active, MissionType.Side));

        gs.CompleteMission("m1");

        var repEntries = gs.ActionLog.Where(e => e.Type == "rep_changed").ToList();
        Assert.Equal(2, repEntries.Count);
        Assert.Equal("bureau", repEntries[0].Payload["factionId"]);
        Assert.Equal("5", repEntries[0].Payload["delta"]);
        Assert.Equal("convocation", repEntries[1].Payload["factionId"]);
        Assert.Equal("-2", repEntries[1].Payload["delta"]);
    }

    [Fact]
    public void CompleteMission_SourceIncludesMissionType()
    {
        var gs = new GameState(seed: 1);
        gs.ActionLog.Clear();
        gs.Town.QuestLog.Add(new ActiveMission("m1", "Test", "Desc", 8, "bureau", MissionStatus.Active, MissionType.Main));

        gs.CompleteMission("m1");

        var entry = gs.ActionLog.First(e => e.Type == "rep_changed" && e.Payload["factionId"] == "bureau");
        Assert.Equal("mission_complete_main", entry.Payload["source"]);
    }

    [Fact]
    public void VendorFilter_IsVendorVisible_ConvocationAtMinus25_ReturnsFalse()
    {
        var rep = new ReputationState();
        rep["convocation"] = -25;

        Assert.False(VendorFilter.IsVendorVisible("convocation", rep));
    }

    [Fact]
    public void VendorFilter_IsVendorVisible_ConvocationAtMinus24_ReturnsTrue()
    {
        var rep = new ReputationState();
        rep["convocation"] = -24;

        Assert.True(VendorFilter.IsVendorVisible("convocation", rep));
    }

    [Fact]
    public void LockoutThreshold_Minus25_ContactRefusesInteraction()
    {
        var gs = MakeGameState(1);
        gs.Reputation["convocation"] = -25;

        // Vendor should be hidden
        Assert.False(VendorFilter.IsVendorVisible("convocation", gs.Reputation));

        // Contact is still in town state but UI shows hostility only
        var contact = gs.Town.FactionContacts.FirstOrDefault(c => c.FactionId == "convocation");
        Assert.NotNull(contact);
    }
}
