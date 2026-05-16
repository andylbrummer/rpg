using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Party;
using RPC.Engine.Save;
using RPC.Engine.Town;

namespace RPC.Tests;

public class WildCardTests
{
    private static GameState CreateState(int seed = 42)
    {
        var registry = new ClassRegistry();
        foreach (var classFile in Directory.GetFiles("../../../../../../content/classes", "*.json"))
        {
            var json = File.ReadAllText(classFile);
            var classDef = System.Text.Json.JsonSerializer.Deserialize<ClassDef>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true
            });
            if (classDef != null)
                registry.LoadFromJson(classDef.Id, json);
        }
        return new GameState(seed, null, registry);
    }

    [Fact]
    public void CampaignConfig_Roll_SetsWildcardTrigger()
    {
        var rng = new GameRandom(1);
        var config = CampaignConfig.Roll(rng);

        Assert.False(string.IsNullOrEmpty(config.WildCard));
        Assert.NotNull(config.WildcardTrigger);
        Assert.Equal(config.WildCard, config.WildcardTrigger.FactionId);
        Assert.InRange(config.WildcardTrigger.TurnThreshold, 18, 24);
    }

    [Fact]
    public void CheckWildCardTrigger_BeforeThreshold_DoesNotTrigger()
    {
        var gs = CreateState();
        var config = CampaignConfig.Roll(new GameRandom(1));
        gs.GenerateOverworld(config);
        gs.Overworld.Turns = config.WildcardTrigger!.TurnThreshold - 1;
        gs.Reputation[config.WildcardTrigger.FactionId] = 25;

        var triggered = gs.CheckWildCardTrigger();
        Assert.False(triggered);
        Assert.Equal(WildCardAllianceStatus.None, gs.WildCardAllianceStatus);
    }

    [Fact]
    public void CheckWildCardTrigger_LowRep_DoesNotTrigger()
    {
        var gs = CreateState();
        var config = CampaignConfig.Roll(new GameRandom(1));
        gs.GenerateOverworld(config);
        gs.Overworld.Turns = config.WildcardTrigger!.TurnThreshold;
        gs.Reputation[config.WildcardTrigger.FactionId] = 19;

        var triggered = gs.CheckWildCardTrigger();
        Assert.False(triggered);
        Assert.Equal(WildCardAllianceStatus.None, gs.WildCardAllianceStatus);
    }

    [Fact]
    public void CheckWildCardTrigger_AtThreshold_WithRep_Triggers()
    {
        var gs = CreateState();
        var config = CampaignConfig.Roll(new GameRandom(1));
        gs.GenerateOverworld(config);
        gs.Overworld.Turns = config.WildcardTrigger!.TurnThreshold;
        gs.Reputation[config.WildcardTrigger.FactionId] = 20;

        var triggered = gs.CheckWildCardTrigger();
        Assert.True(triggered);
        Assert.Equal(WildCardAllianceStatus.Offered, gs.WildCardAllianceStatus);
        Assert.Equal(gs.Overworld.Turns, gs.WildCardAllianceTurn);
        Assert.Contains(gs.ActionLog, e => e.Type == "wildcard_alliance_offered");
    }

    [Fact]
    public void AcceptWildCardAlliance_Success()
    {
        var gs = CreateState();
        var config = CampaignConfig.Roll(new GameRandom(1));
        gs.GenerateOverworld(config);
        gs.Overworld.Turns = config.WildcardTrigger!.TurnThreshold;
        gs.Reputation[config.WildcardTrigger.FactionId] = 20;
        gs.CheckWildCardTrigger();

        var result = gs.AcceptWildCardAlliance();
        Assert.True(result);
        Assert.Equal(WildCardAllianceStatus.Accepted, gs.WildCardAllianceStatus);
        Assert.Contains(gs.ActionLog, e => e.Type == "wildcard_alliance_accepted");
        Assert.Contains(gs.Town.QuestLog, q => q.Id == $"wildcard_quest_{config.WildcardTrigger.FactionId}");
    }

    [Fact]
    public void RefuseWildCardAlliance_Success()
    {
        var gs = CreateState();
        var config = CampaignConfig.Roll(new GameRandom(1));
        gs.GenerateOverworld(config);
        gs.Overworld.Turns = config.WildcardTrigger!.TurnThreshold;
        gs.Reputation[config.WildcardTrigger.FactionId] = 20;
        gs.CheckWildCardTrigger();

        var result = gs.RefuseWildCardAlliance();
        Assert.True(result);
        Assert.Equal(WildCardAllianceStatus.Refused, gs.WildCardAllianceStatus);
        Assert.Contains(gs.ActionLog, e => e.Type == "wildcard_alliance_refused");
    }

    [Fact]
    public void IgnoreWildCardAlliance_Success()
    {
        var gs = CreateState();
        var config = CampaignConfig.Roll(new GameRandom(1));
        gs.GenerateOverworld(config);
        gs.Overworld.Turns = config.WildcardTrigger!.TurnThreshold;
        gs.Reputation[config.WildcardTrigger.FactionId] = 20;
        gs.CheckWildCardTrigger();

        var result = gs.IgnoreWildCardAlliance();
        Assert.True(result);
        Assert.Equal(WildCardAllianceStatus.Ignored, gs.WildCardAllianceStatus);
        Assert.Contains(gs.ActionLog, e => e.Type == "wildcard_alliance_ignored");
    }

    [Fact]
    public void Accept_WhenNotOffered_Fails()
    {
        var gs = CreateState();
        var result = gs.AcceptWildCardAlliance();
        Assert.False(result);
    }

    [Fact]
    public void EnterCombat_WithAlliance_SummonsAlly()
    {
        var gs = CreateState();
        var config = CampaignConfig.Roll(new GameRandom(1));
        gs.GenerateOverworld(config);
        gs.Overworld.Turns = config.WildcardTrigger!.TurnThreshold;
        gs.Reputation[config.WildcardTrigger.FactionId] = 20;
        gs.CheckWildCardTrigger();
        gs.AcceptWildCardAlliance();

        var encounter = new EncounterDef("test", "Test", new[] { new EnemySpawn("rat", 1) });
        gs.TriggerEncounter(encounter);

        Assert.Equal(GameMode.Combat, gs.Mode);
        Assert.Contains(gs.CombatLog, l => l.Message.Contains("summoned", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(gs.ActionLog, e => e.Type == "wildcard_ally_summoned");
        Assert.True(gs.Combat!.Combatants.Any(c => c.IsSummoned));
    }

    [Fact]
    public void EnterCombat_WithoutAlliance_NoSummon()
    {
        var gs = CreateState();
        var config = CampaignConfig.Roll(new GameRandom(1));
        gs.GenerateOverworld(config);

        var encounter = new EncounterDef("test", "Test", new[] { new EnemySpawn("rat", 1) });
        gs.TriggerEncounter(encounter);

        Assert.Equal(GameMode.Combat, gs.Mode);
        Assert.DoesNotContain(gs.ActionLog, e => e.Type == "wildcard_ally_summoned");
    }

    [Fact]
    public void PurchaseVendorItem_WithAlliance_AppliesDiscount()
    {
        var gs = CreateState();
        var config = CampaignConfig.Roll(new GameRandom(1));
        gs.GenerateOverworld(config);
        gs.Overworld.Turns = config.WildcardTrigger!.TurnThreshold;
        gs.Reputation[config.WildcardTrigger.FactionId] = 20;
        gs.CheckWildCardTrigger();
        gs.AcceptWildCardAlliance();

        gs.Town.VendorStock.Add(new VendorItem("test_item", "Test Item", 100, 1));
        gs.PartyGold = 100;

        var result = gs.PurchaseVendorItem("test_item");
        Assert.True(result);
        Assert.Equal(25, gs.PartyGold); // 100 - 75 (25% discount)
    }

    [Fact]
    public void PurchaseVendorItem_WithoutAlliance_NoDiscount()
    {
        var gs = CreateState();
        gs.Town.VendorStock.Add(new VendorItem("test_item", "Test Item", 100, 1));
        gs.PartyGold = 100;

        var result = gs.PurchaseVendorItem("test_item");
        Assert.True(result);
        Assert.Equal(0, gs.PartyGold); // 100 - 100 (no discount)
    }

    [Fact]
    public void SaveLoad_PreservesAllianceState()
    {
        var gs = CreateState();
        var config = CampaignConfig.Roll(new GameRandom(1));
        gs.GenerateOverworld(config);
        gs.Overworld.Turns = config.WildcardTrigger!.TurnThreshold;
        gs.Reputation[config.WildcardTrigger.FactionId] = 20;
        gs.CheckWildCardTrigger();
        gs.AcceptWildCardAlliance();

        var tempPath = Path.GetTempFileName();
        SaveSystem.Save(gs, tempPath);

        var loaded = CreateState();
        var success = SaveSystem.Load(loaded, tempPath);
        Assert.True(success);
        Assert.Equal(WildCardAllianceStatus.Accepted, loaded.WildCardAllianceStatus);
        Assert.Equal(gs.WildCardAllianceTurn, loaded.WildCardAllianceTurn);

        File.Delete(tempPath);
    }

    [Fact]
    public void ReturnToTown_TriggersCheck()
    {
        var gs = CreateState();
        var config = CampaignConfig.Roll(new GameRandom(1));
        gs.GenerateOverworld(config);
        gs.Overworld.Turns = config.WildcardTrigger!.TurnThreshold;
        gs.Reputation[config.WildcardTrigger.FactionId] = 20;

        gs.ReturnToTown();
        Assert.Equal(WildCardAllianceStatus.Offered, gs.WildCardAllianceStatus);
    }
}
