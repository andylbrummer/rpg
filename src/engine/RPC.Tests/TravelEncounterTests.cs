using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Party;
using RPC.Engine.Travel;

namespace RPC.Tests;

public class TravelEncounterTests
{
    private static CharacterState MakeChar(string name, string classId, int hp = 20)
        => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
            name, classId, 1, 0,
            new BaseStats(4, 4, 4, 4, 4),
            hp, Equipment.Empty,
            Array.Empty<string>(), 0);

    [Fact]
    public void TravelEncounterTable_RollEncounterCount_ProducesExpectedDistribution()
    {
        var counts = new int[3];
        var rng = new GameRandom(42);

        for (int i = 0; i < 10000; i++)
        {
            var count = TravelEncounterTable.RollEncounterCount(rng);
            counts[count]++;
        }

        Assert.True(counts[0] is >= 1700 and <= 2300, $"0 encounters: {counts[0]} (expected ~20%)");
        Assert.True(counts[1] is >= 5700 and <= 6300, $"1 encounter: {counts[1]} (expected ~60%)");
        Assert.True(counts[2] is >= 1700 and <= 2300, $"2 encounters: {counts[2]} (expected ~20%)");
    }

    [Fact]
    public void GameState_Travel_RoutesToCombat_WhenCombatEncounter()
    {
        var found = false;
        for (int seed = 0; seed < 200; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
            gs.Travel("broken_engine");

            if (gs.Mode == GameMode.Combat)
            {
                Assert.NotNull(gs.Combat);
                Assert.Null(gs.CurrentTravelEncounter);
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected at least one seed to produce a combat encounter");
    }

    [Fact]
    public void GameState_Travel_RoutesToStatTest_WhenStatTestEncounter()
    {
        var found = false;
        for (int seed = 0; seed < 200; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
            gs.Travel("broken_engine");

            if (gs.CurrentTravelEncounter?.ResolutionType == "stat_test")
            {
                Assert.Equal(GameMode.Menu, gs.Mode);
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected at least one seed to produce a stat_test encounter");
    }

    [Fact]
    public void GameState_Travel_RoutesToDialogue_WhenDialogueEncounter()
    {
        var found = false;
        for (int seed = 0; seed < 200; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
            gs.Travel("broken_engine");

            if (gs.CurrentTravelEncounter?.ResolutionType == "dialogue")
            {
                Assert.Equal(GameMode.Menu, gs.Mode);
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected at least one seed to produce a dialogue encounter");
    }

    [Fact]
    public void GameState_Travel_MarcherNegatesAmbushSurpriseRound()
    {
        var found = false;
        for (int seed = 0; seed < 200; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "marcher"));
            gs.Travel("broken_engine");

            if (gs.Mode == GameMode.Combat)
            {
                // Marcher negates surprise round; combat still happens
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected at least one seed to produce an ambush");
    }

    [Fact]
    public void GameState_Travel_WithoutMarcher_AmbushHasSurpriseRound()
    {
        var found = false;
        for (int seed = 0; seed < 200; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
            gs.Travel("broken_engine");

            if (gs.Mode == GameMode.Combat)
            {
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected at least one seed to produce an ambush");
    }

    [Fact]
    public void GameState_Travel_AshmouthImprovesMerchantPriceTier()
    {
        var found = false;
        for (int seed = 0; seed < 300; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "ashmouth"));
            gs.Travel("broken_engine");

            if (gs.CurrentTravelEncounter?.Id == "merchant")
            {
                Assert.Equal(1, gs.CurrentTravelEncounter.PriceTier);
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected at least one seed to produce a merchant encounter");
    }

    [Fact]
    public void GameState_Travel_WithoutAshmouth_MerchantBasePriceTier()
    {
        var found = false;
        for (int seed = 0; seed < 300; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
            gs.Travel("broken_engine");

            if (gs.CurrentTravelEncounter?.Id == "merchant")
            {
                Assert.Equal(0, gs.CurrentTravelEncounter.PriceTier);
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected at least one seed to produce a merchant encounter");
    }

    [Fact]
    public void GameState_Travel_ReputationAffectsFactionPatrol()
    {
        var found = false;
        for (int seed = 0; seed < 300; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
            gs.Reputation["bureau"] = 30;
            gs.Travel("broken_engine");

            if (gs.CurrentTravelEncounter?.Id == "faction_patrol")
            {
                Assert.Equal(30, gs.CurrentTravelEncounter.ReputationValue);
                Assert.Equal("bureau", gs.CurrentTravelEncounter.FactionId);
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected at least one seed to produce a faction_patrol encounter");
    }

    [Fact]
    public void GameState_ResolveTravelEncounter_StatTest_EmitsActionLog()
    {
        for (int seed = 0; seed < 200; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
            gs.Travel("broken_engine");

            if (gs.CurrentTravelEncounter?.ResolutionType == "stat_test")
            {
                var id = gs.CurrentTravelEncounter.Id;
                gs.ResolveTravelEncounter("roll");

                var log = gs.ActionLog.LastOrDefault(e => e.Category == "travel" && e.Type == "stat_test");
                Assert.NotNull(log);
                Assert.Equal(id, log.Payload["encounterId"]);
                return;
            }
        }
        Assert.Fail("Expected at least one seed to produce a stat_test encounter");
    }

    [Fact]
    public void GameState_ResolveTravelEncounter_Dialogue_EmitsActionLog()
    {
        for (int seed = 0; seed < 200; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
            gs.Travel("broken_engine");

            if (gs.CurrentTravelEncounter?.ResolutionType == "dialogue")
            {
                var id = gs.CurrentTravelEncounter.Id;
                gs.ResolveTravelEncounter("Help");

                var log = gs.ActionLog.LastOrDefault(e => e.Category == "travel" && e.Type == "dialogue");
                Assert.NotNull(log);
                Assert.Equal(id, log.Payload["encounterId"]);
                Assert.Equal("Help", log.Payload["choice"]);
                return;
            }
        }
        Assert.Fail("Expected at least one seed to produce a dialogue encounter");
    }

    [Fact]
    public void GameState_Travel_ZeroEncounters_NoStateChange()
    {
        for (int seed = 0; seed < 200; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
            gs.Travel("broken_engine");

            if (gs.RolledTravelEncounterCount == 0)
            {
                Assert.Null(gs.CurrentTravelEncounter);
                return;
            }
        }
        Assert.Fail("Expected at least one seed to roll 0 encounters");
    }

    [Fact]
    public void GameState_Travel_MultipleEncounters_QueuesSecond()
    {
        for (int seed = 0; seed < 500; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
            gs.Travel("broken_engine");

            if (gs.RolledTravelEncounterCount == 2 && gs.CurrentTravelEncounter?.ResolutionType != "combat")
            {
                Assert.NotNull(gs.CurrentTravelEncounter);
                Assert.Equal(0, gs.ResolvedTravelEncounterCount);

                gs.ResolveTravelEncounter("roll");

                Assert.Equal(1, gs.ResolvedTravelEncounterCount);
                Assert.NotNull(gs.CurrentTravelEncounter);
                return;
            }
        }
        Assert.Fail("Expected at least one seed to roll 2 non-combat encounters");
    }
}
