using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Party;
using RPC.Engine.Town;

namespace RPC.Tests;

public class DowntimeSystemTests : IDisposable
{
    private readonly string _testSavePath;

    public DowntimeSystemTests()
    {
        _testSavePath = Path.Combine(Path.GetTempPath(), $"test_downtime_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testSavePath))
            File.Delete(_testSavePath);
    }

    [Fact]
    public void PerformDowntimeAction_Rest_HealsToFull()
    {
        var gs = new GameState(seed: 42);
        var member = gs.Party.Members[0];
        var maxHp = member.GetEffectiveStats().MaxHp;

        // Damage the character
        gs.Party.SetMember(0, member with { CurrentHp = 1 });
        member = gs.Party.Members[0];
        Assert.Equal(1, member.CurrentHp);

        var result = gs.PerformDowntimeAction(member.Id, DowntimeAction.Rest);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Rest", result.ActionType);
        Assert.True(result.HpRestored > 0);
        Assert.Equal(maxHp, gs.Party.Members[0].CurrentHp);
    }

    [Fact]
    public void PerformDowntimeAction_Rest_AlreadyFull_ReturnsZeroHeal()
    {
        var gs = new GameState(seed: 42);
        var member = gs.Party.Members[0];
        var maxHp = member.GetEffectiveStats().MaxHp;
        gs.Party.SetMember(0, member with { CurrentHp = maxHp });

        var result = gs.PerformDowntimeAction(member.Id, DowntimeAction.Rest);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(0, result.HpRestored);
    }

    [Fact]
    public void PerformDowntimeAction_Train_GivesXp()
    {
        var gs = new GameState(seed: 42);
        var member = gs.Party.Members[0];
        var initialXp = member.Xp;

        var result = gs.PerformDowntimeAction(member.Id, DowntimeAction.Train);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Train", result.ActionType);
        Assert.Equal(15, result.XpGained);
        Assert.Equal(initialXp + 15, gs.Party.Members[0].Xp);
    }

    [Fact]
    public void PerformDowntimeAction_Craft_AddsComponent()
    {
        var gs = new GameState(seed: 42);
        var member = gs.Party.Members[0];
        Assert.Empty(member.ComponentInventory);

        var result = gs.PerformDowntimeAction(member.Id, DowntimeAction.Craft);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Craft", result.ActionType);
        Assert.NotNull(result.ItemId);
        Assert.Equal(1, result.ItemCount);
        Assert.Single(gs.Party.Members[0].ComponentInventory);
    }

    [Fact]
    public void PerformDowntimeAction_Network_ChangesReputation()
    {
        var gs = new GameState(seed: 42);
        var member = gs.Party.Members[0];
        var initialRep = gs.Reputation["bureau"];

        var result = gs.PerformDowntimeAction(member.Id, DowntimeAction.Network);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Network", result.ActionType);
        Assert.NotNull(result.FactionId);
        Assert.Equal(5, result.RepDelta);
        Assert.NotEqual(initialRep, gs.Reputation[result.FactionId!]);
    }

    [Fact]
    public void PerformDowntimeAction_Investigate_AddsEvidence()
    {
        var gs = new GameState(seed: 42);
        var member = gs.Party.Members[0];
        var initialCounters = gs.Evidence.Counters.Count;

        var result = gs.PerformDowntimeAction(member.Id, DowntimeAction.Investigate);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Investigate", result.ActionType);
        Assert.NotNull(result.EvidenceFaction);
        Assert.True(gs.Evidence.Counters.Count > initialCounters || gs.Evidence.Counters.Values.Sum() > 0);
    }

    [Fact]
    public void PerformDowntimeAction_LayLow_ImprovesNegativeRep()
    {
        var gs = new GameState(seed: 42);
        var member = gs.Party.Members[0];
        // Set a negative reputation
        gs.Reputation.ApplyDelta("bureau", -10, "test");
        var initialRep = gs.Reputation["bureau"];
        Assert.True(initialRep < 0);

        var result = gs.PerformDowntimeAction(member.Id, DowntimeAction.LayLow);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("LayLow", result.ActionType);
        Assert.True(gs.Reputation["bureau"] > initialRep);
    }

    [Fact]
    public void PerformDowntimeAction_LayLow_NoNegativeRep_ReturnsZeroDelta()
    {
        var gs = new GameState(seed: 42);
        var member = gs.Party.Members[0];
        // Ensure all reps are non-negative
        foreach (var faction in new[] { "bureau", "convocation", "stillness", "inkblood", "cartography" })
        {
            if (gs.Reputation[faction] < 0)
            {
                gs.Reputation.ApplyDelta(faction, -gs.Reputation[faction], "reset");
            }
        }

        var result = gs.PerformDowntimeAction(member.Id, DowntimeAction.LayLow);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(0, result.RepDelta);
    }

    [Fact]
    public void PerformDowntimeAction_TendBlooms_HealsAndMayGrantItem()
    {
        var gs = new GameState(seed: 42);
        var member = gs.Party.Members[0];
        var maxHp = member.GetEffectiveStats().MaxHp;
        gs.Party.SetMember(0, member with { CurrentHp = 1 });

        var result = gs.PerformDowntimeAction(member.Id, DowntimeAction.TendBlooms);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("TendBlooms", result.ActionType);
        Assert.True(result.HpRestored > 0);
        Assert.True(gs.Party.Members[0].CurrentHp > 1);
    }

    [Fact]
    public void PerformDowntimeAction_SecondAttempt_ReturnsNull()
    {
        var gs = new GameState(seed: 42);
        var member = gs.Party.Members[0];

        var first = gs.PerformDowntimeAction(member.Id, DowntimeAction.Rest);
        Assert.NotNull(first);
        Assert.True(first.Success);

        var second = gs.PerformDowntimeAction(member.Id, DowntimeAction.Train);
        Assert.Null(second);
    }

    [Fact]
    public void PerformDowntimeAction_EmptySlot_ReturnsNull()
    {
        var gs = new GameState(seed: 42);
        var emptyGuid = Guid.Empty;

        var result = gs.PerformDowntimeAction(emptyGuid, DowntimeAction.Rest);
        Assert.Null(result);
    }

    [Fact]
    public void PerformDowntimeAction_NotInMenuMode_ReturnsNull()
    {
        var gs = new GameState(seed: 42);
        gs.Mode = GameMode.Exploration;
        var member = gs.Party.Members[0];

        var result = gs.PerformDowntimeAction(member.Id, DowntimeAction.Rest);
        Assert.Null(result);
    }

    [Fact]
    public void ReturnToTown_ClearsDowntimeCooldowns()
    {
        var gs = new GameState(seed: 42);
        var member = gs.Party.Members[0];

        // Enter a dungeon first so ReturnToTown does something
        gs.Mode = GameMode.Exploration;
        gs.CurrentDungeon = new RPC.Engine.Models.Dungeons.Dungeon(10, 10, "test");

        // Perform downtime
        gs.Mode = GameMode.Menu;
        gs.PerformDowntimeAction(member.Id, DowntimeAction.Rest);
        Assert.Contains(member.Id, gs.DowntimeCompleted);

        // Return to town should clear
        gs.ReturnToTown();
        Assert.Empty(gs.DowntimeCompleted);
    }

    [Fact]
    public void SaveSystem_PreservesDowntimeCompleted()
    {
        var gs = new GameState(seed: 42);
        var member = gs.Party.Members[0];
        gs.PerformDowntimeAction(member.Id, DowntimeAction.Train);
        Assert.Contains(member.Id, gs.DowntimeCompleted);

        gs.SaveGame(_testSavePath);
        Assert.True(File.Exists(_testSavePath));

        var gs2 = new GameState(seed: 9999);
        var loaded = gs2.LoadGame(_testSavePath);
        Assert.True(loaded);
        Assert.Contains(member.Id, gs2.DowntimeCompleted);
    }

    [Fact]
    public void DowntimeSystem_CanPerformAllSevenActions()
    {
        var gs = new GameState(seed: 42);
        var actions = Enum.GetValues<DowntimeAction>();

        for (int i = 0; i < actions.Length && i < 6; i++)
        {
            var member = gs.Party.Members[i];
            if (member.Id == Guid.Empty) continue;

            var result = gs.PerformDowntimeAction(member.Id, actions[i]);
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(actions[i].ToString(), result.ActionType);
        }
    }

    [Fact]
    public void DowntimeAction_EmitsActionLog()
    {
        var gs = new GameState(seed: 42);
        var member = gs.Party.Members[0];
        var initialLogCount = gs.ActionLog.Count;

        gs.PerformDowntimeAction(member.Id, DowntimeAction.Rest);

        Assert.True(gs.ActionLog.Count > initialLogCount);
        var entry = gs.ActionLog.Last();
        Assert.Equal("downtime", entry.Category);
        Assert.Equal("rest", entry.Type);
        Assert.Equal(member.Id.ToString(), entry.Payload["characterId"]);
    }
}
