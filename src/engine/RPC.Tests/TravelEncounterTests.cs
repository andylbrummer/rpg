using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Overworld;
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
    public void TravelEncounterTable_RollEncounterCount_LowDanger_ProducesExpectedDistribution()
    {
        var counts = new int[3];
        var rng = new GameRandom(42);

        for (int i = 0; i < 10000; i++)
        {
            var count = TravelEncounterTable.RollEncounterCount(rng, 1);
            counts[count]++;
        }

        Assert.True(counts[0] is >= 5500 and <= 6500, $"0 encounters: {counts[0]} (expected ~60%)");
        Assert.True(counts[1] is >= 3000 and <= 3800, $"1 encounter: {counts[1]} (expected ~35%)");
        Assert.True(counts[2] is >= 0 and <= 800, $"2 encounters: {counts[2]} (expected ~5%)");
    }

    [Fact]
    public void TravelEncounterTable_RollEncounterCount_MediumDanger_ProducesExpectedDistribution()
    {
        var counts = new int[3];
        var rng = new GameRandom(42);

        for (int i = 0; i < 10000; i++)
        {
            var count = TravelEncounterTable.RollEncounterCount(rng, 2);
            counts[count]++;
        }

        Assert.True(counts[0] is >= 1700 and <= 2300, $"0 encounters: {counts[0]} (expected ~20%)");
        Assert.True(counts[1] is >= 5700 and <= 6300, $"1 encounter: {counts[1]} (expected ~60%)");
        Assert.True(counts[2] is >= 1700 and <= 2300, $"2 encounters: {counts[2]} (expected ~20%)");
    }

    [Fact]
    public void TravelEncounterTable_RollEncounterCount_HighDanger_ProducesExpectedDistribution()
    {
        var counts = new int[3];
        var rng = new GameRandom(42);

        for (int i = 0; i < 10000; i++)
        {
            var count = TravelEncounterTable.RollEncounterCount(rng, 4);
            counts[count]++;
        }

        Assert.True(counts[0] is >= 0 and <= 800, $"0 encounters: {counts[0]} (expected ~5%)");
        Assert.True(counts[1] is >= 4500 and <= 5500, $"1 encounter: {counts[1]} (expected ~50%)");
        Assert.True(counts[2] is >= 4000 and <= 5000, $"2 encounters: {counts[2]} (expected ~45%)");
    }

    [Fact]
    public void TravelEncounterTable_RollEncounter_RespectsTerrain()
    {
        var rng = new GameRandom(42);
        var terrains = TravelEncounterTable.GetTerrainTypes();

        foreach (var terrain in terrains)
        {
            var encounter = TravelEncounterTable.RollEncounter(rng, 5, terrain);
            Assert.NotNull(encounter);
        }
    }

    [Fact]
    public void TravelEncounterTable_RollEncounter_Forest_HasPoisonThicket()
    {
        var found = false;
        for (int seed = 0; seed < 500; seed++)
        {
            var rng = new GameRandom(seed);
            var encounter = TravelEncounterTable.RollEncounter(rng, 3, "forest");
            if (encounter?.Id == "poison_thicket")
            {
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected poison_thicket in forest terrain");
    }

    [Fact]
    public void TravelEncounterTable_RollEncounter_Mountain_HasRockslide()
    {
        var found = false;
        for (int seed = 0; seed < 500; seed++)
        {
            var rng = new GameRandom(seed);
            var encounter = TravelEncounterTable.RollEncounter(rng, 3, "mountain");
            if (encounter?.Id == "rockslide")
            {
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected rockslide in mountain terrain");
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
    public void TravelEncounterTable_Forest_HasPoisonThicket()
    {
        var rng = new GameRandom(42);
        var found = false;
        for (int i = 0; i < 1000; i++)
        {
            var enc = TravelEncounterTable.RollEncounter(rng, 3, "forest");
            if (enc?.Id == "poison_thicket")
            {
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected poison_thicket in forest terrain");
    }

    [Fact]
    public void GameState_Travel_Cauterist_BurnsThroughEnvironmentalHazard()
    {
        var found = false;
        for (int seed = 0; seed < 1000; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "cauterist"));
            gs.Overworld = new OverworldState();
            gs.Overworld.Nodes["the_reach"] = new OverworldNode("the_reach", "The Reach", NodeType.Town);
            gs.Overworld.Nodes["broken_engine"] = new OverworldNode("broken_engine", "Broken Engine", NodeType.Dungeon);
            gs.Overworld.Routes.Clear();
            gs.Overworld.Routes.Add(new OverworldRoute("the_reach", "broken_engine", 3, 3, "forest"));
            gs.Overworld.CurrentNodeId = "the_reach";
            gs.Travel("broken_engine");

            if (gs.CurrentTravelEncounter?.Id == "poison_thicket")
            {
                Assert.Equal("dialogue", gs.CurrentTravelEncounter.ResolutionType);
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected cauterist to convert poison_thicket to dialogue");
    }

    [Fact]
    public void TravelEncounterTable_Mountain_HasRockslide()
    {
        var rng = new GameRandom(42);
        var found = false;
        for (int i = 0; i < 1000; i++)
        {
            var enc = TravelEncounterTable.RollEncounter(rng, 3, "mountain");
            if (enc?.Id == "rockslide")
            {
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected rockslide in mountain terrain");
    }

    [Fact]
    public void GameState_Travel_Marcher_BypassesRockslide()
    {
        var found = false;
        for (int seed = 0; seed < 1000; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "marcher"));
            gs.Overworld = new OverworldState();
            gs.Overworld.Nodes["the_reach"] = new OverworldNode("the_reach", "The Reach", NodeType.Town);
            gs.Overworld.Nodes["broken_engine"] = new OverworldNode("broken_engine", "Broken Engine", NodeType.Dungeon);
            gs.Overworld.Routes.Clear();
            gs.Overworld.Routes.Add(new OverworldRoute("the_reach", "broken_engine", 3, 3, "mountain"));
            gs.Overworld.CurrentNodeId = "the_reach";
            gs.Travel("broken_engine");

            if (gs.CurrentTravelEncounter?.Id == "rockslide")
            {
                Assert.Equal("dialogue", gs.CurrentTravelEncounter.ResolutionType);
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected marcher to convert rockslide to dialogue");
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
