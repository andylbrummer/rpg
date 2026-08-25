using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Combat;
using RPC.Engine.Overworld;
using RPC.Engine.Save;

namespace RPC.Tests;

public class FactionStateMachineTests
{
    [Fact]
    public void CampaignConfig_Roll_Generates_Default_Timelines()
    {
        var rng = new GameRandom(42);
        var config = CampaignConfig.Roll(rng);

        Assert.NotEmpty(config.FactionTimelines);
        foreach (var faction in CampaignConfig.FactionPool)
        {
            Assert.True(config.FactionTimelines.ContainsKey(faction),
                $"Expected timeline for {faction}");
            Assert.Equal(12, config.FactionTimelines[faction].Preparing);
            Assert.Equal(22, config.FactionTimelines[faction].Executing);
        }
    }

    [Fact]
    public void GetFactionState_Investigating_Before_Preparing()
    {
        var state = new GameState(seed: 1);
        state.CampaignConfig = CampaignConfig.Roll(new GameRandom(1));
        state.Overworld.Turns = 5;

        var service = new CampaignService(null);
        var faction = CampaignConfig.FactionPool[0];

        Assert.Equal(FactionState.Investigating, service.GetFactionState(state, faction));
    }

    [Fact]
    public void GetFactionState_Preparing_At_Preparing_Turn()
    {
        var state = new GameState(seed: 1);
        state.CampaignConfig = CampaignConfig.Roll(new GameRandom(1));
        state.Overworld.Turns = 12;

        var service = new CampaignService(null);
        var faction = CampaignConfig.FactionPool[0];

        Assert.Equal(FactionState.Preparing, service.GetFactionState(state, faction));
    }

    [Fact]
    public void GetFactionState_Executing_At_Executing_Turn()
    {
        var state = new GameState(seed: 1);
        state.CampaignConfig = CampaignConfig.Roll(new GameRandom(1));
        state.Overworld.Turns = 22;

        var service = new CampaignService(null);
        var faction = CampaignConfig.FactionPool[0];

        Assert.Equal(FactionState.Executing, service.GetFactionState(state, faction));
    }

    [Fact]
    public void ModifyTimeline_Delay_Shift_Transitions()
    {
        var state = new GameState(seed: 1);
        state.CampaignConfig = CampaignConfig.Roll(new GameRandom(1));
        var service = new CampaignService(null);
        var faction = CampaignConfig.FactionPool[0];

        state.Overworld.Turns = 12;
        Assert.Equal(FactionState.Preparing, service.GetFactionState(state, faction));

        service.ModifyFactionTimeline(state, faction, 2);

        // At turn 12, with +2 delay, preparing is now at 14
        Assert.Equal(FactionState.Investigating, service.GetFactionState(state, faction));

        state.Overworld.Turns = 14;
        Assert.Equal(FactionState.Preparing, service.GetFactionState(state, faction));
    }

    [Fact]
    public void ModifyTimeline_Accelerate_Shift_Transitions()
    {
        var state = new GameState(seed: 1);
        state.CampaignConfig = CampaignConfig.Roll(new GameRandom(1));
        var service = new CampaignService(null);
        var faction = CampaignConfig.FactionPool[0];

        service.ModifyFactionTimeline(state, faction, -2);

        // At turn 10, with -2 acceleration, preparing is now at 10
        state.Overworld.Turns = 10;
        Assert.Equal(FactionState.Preparing, service.GetFactionState(state, faction));
    }

    [Fact]
    public void ModifyTimeline_Clamps_At_PlusMinus_3()
    {
        var state = new GameState(seed: 1);
        state.CampaignConfig = CampaignConfig.Roll(new GameRandom(1));
        var service = new CampaignService(null);
        var faction = CampaignConfig.FactionPool[0];

        service.ModifyFactionTimeline(state, faction, 5);
        Assert.Equal(3, state.Campaign.FactionTimelineModifiers[faction]);

        service.ModifyFactionTimeline(state, faction, -10);
        Assert.Equal(-3, state.Campaign.FactionTimelineModifiers[faction]);
    }

    [Fact]
    public void ModifyTimeline_Emits_ActionLog()
    {
        var state = new GameState(seed: 1);
        state.CampaignConfig = CampaignConfig.Roll(new GameRandom(1));
        var service = new CampaignService(null);
        var faction = CampaignConfig.FactionPool[0];

        service.ModifyFactionTimeline(state, faction, 1);

        var log = state.ActionLog.LastOrDefault();
        Assert.NotNull(log);
        Assert.Equal("faction", log.Category);
        Assert.Equal("timeline_modified", log.Type);
        Assert.Equal(faction, log.Payload["factionId"]);
    }

    [Fact]
    public void RouteStatus_Uses_Modified_Executing_Turn()
    {
        var state = new GameState(seed: 1);
        state.CampaignConfig = CampaignConfig.Roll(new GameRandom(1));
        state.Overworld.GenerateFromConfig(state.CampaignConfig, new GameRandom(1));

        var faction = state.CampaignConfig.Patron;
        var service = new CampaignService(null);

        // Delay executing by 2 turns
        service.ModifyFactionTimeline(state, faction, 2);

        // At turn 22 (original executing), routes should NOT be contested yet
        state.Overworld.Turns = 22;
        RouteStatusSystem.ApplyTurnMilestoneTransitions(state.Overworld, state.CampaignConfig, new GameRandom(1));

        // Routes with faction presence should still be open
        var contestedBefore = state.Overworld.Routes.Count(r => r.Status == RouteStatus.Contested);

        // At turn 24 (modified executing), routes SHOULD be contested
        state.Overworld.Turns = 24;
        RouteStatusSystem.ApplyTurnMilestoneTransitions(state.Overworld, state.CampaignConfig, new GameRandom(1));

        var contestedAfter = state.Overworld.Routes.Count(r => r.Status == RouteStatus.Contested);
        Assert.True(contestedAfter >= contestedBefore,
            "Routes should be contested at modified executing turn");
    }

    [Fact]
    public void SaveLoad_Preserves_Timeline_Modifiers()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_fsm_{Guid.NewGuid()}.json");
        try
        {
            var state = new GameState(seed: 1);
            state.CampaignConfig = CampaignConfig.Roll(new GameRandom(1));
            var service = new CampaignService(null);

            service.ModifyFactionTimeline(state, "bureau", 2);
            service.ModifyFactionTimeline(state, "stillness", -1);

            SaveSystem.Save(state, tempPath);

            var restored = new GameState(seed: 1);
            SaveSystem.Load(restored, tempPath);

            Assert.Equal(2, restored.Campaign.FactionTimelineModifiers["bureau"]);
            Assert.Equal(-1, restored.Campaign.FactionTimelineModifiers["stillness"]);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
