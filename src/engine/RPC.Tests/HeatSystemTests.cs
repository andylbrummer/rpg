using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Combat;
using RPC.Engine.Character;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Town;

namespace RPC.Tests;

public class HeatSystemTests
{
    [Theory]
    [InlineData(0, HeatTier.None)]
    [InlineData(15, HeatTier.None)]
    [InlineData(20, HeatTier.None)]
    [InlineData(21, HeatTier.PricePenalty)]
    [InlineData(35, HeatTier.PricePenalty)]
    [InlineData(40, HeatTier.PricePenalty)]
    [InlineData(41, HeatTier.Patrols)]
    [InlineData(55, HeatTier.Patrols)]
    [InlineData(60, HeatTier.Patrols)]
    [InlineData(61, HeatTier.ContactsRefuse)]
    [InlineData(75, HeatTier.ContactsRefuse)]
    [InlineData(80, HeatTier.ContactsRefuse)]
    [InlineData(81, HeatTier.Lockdown)]
    [InlineData(99, HeatTier.Lockdown)]
    [InlineData(100, HeatTier.Lockdown)]
    public void HeatTier_CorrectForValue(int value, HeatTier expected)
    {
        var heat = new HeatState { Value = value };
        Assert.Equal(expected, heat.Tier);
    }

    [Fact]
    public void HeatValue_ClampsToRange()
    {
        var heat = new HeatState { Value = 50 };
        heat.Add(100);
        Assert.Equal(100, heat.Value);

        heat.Add(-200);
        Assert.Equal(0, heat.Value);
    }

    [Fact]
    public void Heat_HasEffects_AtCorrectTiers()
    {
        var low = new HeatState { Value = 10 };
        Assert.False(low.HasPricePenalty);
        Assert.False(low.HasPatrols);
        Assert.False(low.ContactsRefuse);
        Assert.False(low.IsLockdown);

        var penalty = new HeatState { Value = 30 };
        Assert.True(penalty.HasPricePenalty);
        Assert.False(penalty.HasPatrols);

        var patrols = new HeatState { Value = 50 };
        Assert.True(patrols.HasPatrols);
        Assert.False(patrols.ContactsRefuse);

        var refuse = new HeatState { Value = 70 };
        Assert.True(refuse.ContactsRefuse);
        Assert.False(refuse.IsLockdown);

        var lockdown = new HeatState { Value = 90 };
        Assert.True(lockdown.IsLockdown);
    }

    [Fact]
    public void Travel_BlockedDuringLockdown()
    {
        var gs = new GameState(seed: 1);
        var config = CampaignConfig.Roll(new GameRandom(1));
        config.EvidenceChain = Enumerable.Repeat("test", 10).ToList();
        config.NpcCasting = new Dictionary<string, string> { { "npc1", "role1" } };
        gs.GenerateOverworld(config);

        gs.Heat.Value = 90; // lockdown
        var result = gs.Travel(gs.Overworld.Nodes.Keys.First(n => n != gs.Overworld.CurrentNodeId));

        Assert.False(result);
    }

    [Fact]
    public void DungeonEntry_BlockedDuringLockdown()
    {
        var gs = new GameState(seed: 1);
        gs.Heat.Value = 90; // lockdown

        var dungeon = new Dungeon(5, 5, "test");
        gs.EnterDungeon(dungeon, "test");

        Assert.Null(gs.CurrentDungeon);
    }

    [Fact]
    public void Heat_NaturalDecay_OnTurnIncrement()
    {
        var gs = new GameState(seed: 1);
        var config = CampaignConfig.Roll(new GameRandom(1));
        config.EvidenceChain = Enumerable.Repeat("test", 10).ToList();
        config.NpcCasting = new Dictionary<string, string> { { "npc1", "role1" } };
        gs.GenerateOverworld(config);

        gs.Heat.Value = 30;
        gs.IncrementTurns(1);

        Assert.Equal(25, gs.Heat.Value);
    }

    [Fact]
    public void Heat_LayLow_ReducesHeat()
    {
        var gs = new GameState(seed: 1);
        gs.Heat.Value = 50;
        gs.Party.SetMember(0, default);

        // Need a valid character to perform downtime
        var character = new RPC.Engine.Character.CharacterState(
            Guid.NewGuid(), "Test", "bonewarden", 1, 0,
            new RPC.Engine.Character.BaseStats(4, 4, 4, 4, 4),
            20, RPC.Engine.Character.Equipment.Empty, Array.Empty<string>(), 0);
        gs.Party.SetMember(0, character);

        var result = gs.PerformDowntimeAction(character.Id, RPC.Engine.Town.DowntimeAction.LayLow);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(20, gs.Heat.Value);
    }
}
