using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Travel;

namespace RPC.Tests;

public class ReputationImpactTests
{
    private static CharacterState MakeChar(string name, string classId, int hp = 20)
        => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
            name, classId, 1, 0,
            new BaseStats(4, 4, 4, 4, 4),
            hp, Equipment.Empty,
            Array.Empty<string>(), 0);

    [Theory]
    [InlineData(-100, AttitudeTier.Hostile)]
    [InlineData(-25, AttitudeTier.Hostile)]
    [InlineData(-24, AttitudeTier.Suspicious)]
    [InlineData(0, AttitudeTier.Suspicious)]
    [InlineData(1, AttitudeTier.Neutral)]
    [InlineData(24, AttitudeTier.Neutral)]
    [InlineData(25, AttitudeTier.Friendly)]
    [InlineData(49, AttitudeTier.Friendly)]
    [InlineData(50, AttitudeTier.Allied)]
    [InlineData(100, AttitudeTier.Allied)]
    public void Reputation_GetAttitudeTier_ReturnsCorrectTier(int repValue, AttitudeTier expected)
    {
        var rep = new ReputationState();
        rep["bureau"] = repValue;

        Assert.Equal(expected, rep.GetAttitudeTier("bureau"));
    }

    [Fact]
    public void GameState_Travel_FactionPatrol_HighRep_OffersFriendlyOptions()
    {
        var found = false;
        for (int seed = 0; seed < 300; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
            gs.Reputation["bureau"] = 30;
            gs.Travel("broken_engine");

            if (gs.CurrentTravelEncounter?.Id is "faction_patrol" or "bureau_patrol")
            {
                Assert.Equal("dialogue", gs.CurrentTravelEncounter.ResolutionType);
                Assert.Contains("Request intel", gs.CurrentTravelEncounter.Options!);
                Assert.Contains("Pass safely", gs.CurrentTravelEncounter.Options!);
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected at least one seed to produce a bureau patrol");
    }

    [Fact]
    public void GameState_Travel_FactionPatrol_LowRep_OffersNeutralOptions()
    {
        var found = false;
        for (int seed = 0; seed < 300; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
            gs.Reputation["bureau"] = 10;
            gs.Travel("broken_engine");

            if (gs.CurrentTravelEncounter?.Id is "faction_patrol" or "bureau_patrol")
            {
                Assert.Equal("dialogue", gs.CurrentTravelEncounter.ResolutionType);
                Assert.Contains("Show papers", gs.CurrentTravelEncounter.Options!);
                Assert.Contains("Attack", gs.CurrentTravelEncounter.Options!);
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected at least one seed to produce a bureau patrol");
    }

    [Fact]
    public void GameState_Travel_FactionPatrol_NegativeRep_AttacksImmediately()
    {
        var found = false;
        for (int seed = 0; seed < 300; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
            gs.Reputation["bureau"] = -30;
            gs.Travel("broken_engine");

            if (gs.Mode == GameMode.Combat)
            {
                Assert.Null(gs.CurrentTravelEncounter);
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected at least one seed to produce a hostile bureau patrol");
    }

    [Fact]
    public void GameState_Travel_AttackPatrol_AppliesNegativeRep()
    {
        for (int seed = 0; seed < 300; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
            gs.Reputation["bureau"] = 10;
            gs.Travel("broken_engine");

            if (gs.CurrentTravelEncounter?.Id is "faction_patrol" or "bureau_patrol")
            {
                gs.ResolveTravelEncounter("Attack");
                Assert.Equal(5, gs.Reputation["bureau"]);
                return;
            }
        }
        Assert.Fail("Expected at least one seed to produce a bureau patrol");
    }

    [Fact]
    public void GameState_Travel_CooperateWithPatrol_AppliesPositiveRep()
    {
        for (int seed = 0; seed < 300; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
            gs.Reputation["bureau"] = 30;
            gs.Travel("broken_engine");

            if (gs.CurrentTravelEncounter?.Id is "faction_patrol" or "bureau_patrol")
            {
                gs.ResolveTravelEncounter("Request intel");
                Assert.Equal(32, gs.Reputation["bureau"]);
                return;
            }
        }
        Assert.Fail("Expected at least one seed to produce a bureau patrol");
    }

    [Fact]
    public void GameState_Travel_HelpRefugees_AppliesBureauRep()
    {
        for (int seed = 0; seed < 300; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
            gs.Travel("broken_engine");

            if (gs.CurrentTravelEncounter?.Id == "refugees")
            {
                gs.ResolveTravelEncounter("Help");
                Assert.Equal(2, gs.Reputation["bureau"]);
                return;
            }
        }
        Assert.Fail("Expected at least one seed to produce refugees");
    }

    [Fact]
    public void GameState_Dungeon_FactionSoldier_HighRep_OffersParley()
    {
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("test", @"{
            ""id"": ""test"",
            ""name"": ""Test"",
            ""entries"": [
                { ""id"": ""fs-1"", ""weight"": 100, ""factionId"": ""bureau"", ""enemies"": [{""enemyId"": ""bureau_soldier"", ""count"": 2}] }
            ]
        }");

        var gs = new GameState(seed: 1, encounterTables: registry);
        gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
        gs.Reputation["bureau"] = 30;
        var dungeon = new Dungeon(5, 5, "test") { WanderingTableId = "test" };
        gs.EnterDungeon(dungeon, "test");

        // Trigger the faction soldier encounter
        gs.TriggerEncounter();

        Assert.NotNull(gs.CurrentParley);
        Assert.Equal("bureau", gs.CurrentParley.FactionId);
        Assert.Contains("Parley", gs.CurrentParley.Options);
        Assert.Equal(GameMode.Exploration, gs.Mode);
        Assert.Null(gs.Combat);
    }

    [Fact]
    public void GameState_Dungeon_FactionSoldier_LowRep_BuffsEncounter()
    {
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("test", @"{
            ""id"": ""test"",
            ""name"": ""Test"",
            ""entries"": [
                { ""id"": ""fs-1"", ""weight"": 100, ""factionId"": ""bureau"", ""enemies"": [{""enemyId"": ""bureau_soldier"", ""count"": 2}] }
            ]
        }");

        var gs = new GameState(seed: 1, encounterTables: registry);
        gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
        gs.Reputation["bureau"] = -30;
        var dungeon = new Dungeon(5, 5, "test") { WanderingTableId = "test" };
        gs.EnterDungeon(dungeon, "test");

        gs.TriggerEncounter();

        Assert.Null(gs.CurrentParley);
        Assert.Equal(GameMode.Combat, gs.Mode);
        Assert.NotNull(gs.Combat);
        // Original 2 soldiers + 1 reinforcement = 3 enemies
        Assert.Equal(3, gs.Combat.Combatants.Count(c => !c.IsPlayer));
    }

    [Fact]
    public void GameState_Dungeon_Parley_Accepted_ResolvesPeacefully()
    {
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("test", @"{
            ""id"": ""test"",
            ""name"": ""Test"",
            ""entries"": [
                { ""id"": ""fs-1"", ""weight"": 100, ""factionId"": ""bureau"", ""enemies"": [{""enemyId"": ""bureau_soldier"", ""count"": 2}] }
            ]
        }");

        var gs = new GameState(seed: 1, encounterTables: registry);
        gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
        gs.Reputation["bureau"] = 30;
        var dungeon = new Dungeon(5, 5, "test") { WanderingTableId = "test" };
        gs.EnterDungeon(dungeon, "test");

        gs.TriggerEncounter();
        Assert.NotNull(gs.CurrentParley);

        var result = gs.ResolveParley("parley");

        Assert.True(result);
        Assert.Null(gs.CurrentParley);
        Assert.Null(gs.Combat);
        Assert.Equal(GameMode.Exploration, gs.Mode);
        Assert.Contains(gs.ActionLog, e => e.Type == "encounter_parleyed");
    }

    [Fact]
    public void GameState_Dungeon_Parley_Declined_EntersCombat()
    {
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("test", @"{
            ""id"": ""test"",
            ""name"": ""Test"",
            ""entries"": [
                { ""id"": ""fs-1"", ""weight"": 100, ""factionId"": ""bureau"", ""enemies"": [{""enemyId"": ""bureau_soldier"", ""count"": 2}] }
            ]
        }");

        var gs = new GameState(seed: 1, encounterTables: registry);
        gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
        gs.Reputation["bureau"] = 30;
        var dungeon = new Dungeon(5, 5, "test") { WanderingTableId = "test" };
        gs.EnterDungeon(dungeon, "test");

        gs.TriggerEncounter();
        Assert.NotNull(gs.CurrentParley);

        var result = gs.ResolveParley("fight");

        Assert.True(result);
        Assert.Null(gs.CurrentParley);
        Assert.Equal(GameMode.Combat, gs.Mode);
        Assert.NotNull(gs.Combat);
    }

    [Fact]
    public void GameState_Dungeon_NonFactionEncounter_NoParley()
    {
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("test", @"{
            ""id"": ""test"",
            ""name"": ""Test"",
            ""entries"": [
                { ""id"": ""rat-1"", ""weight"": 100, ""enemies"": [{""enemyId"": ""rat"", ""count"": 2}] }
            ]
        }");

        var gs = new GameState(seed: 1, encounterTables: registry);
        gs.Party.SetMember(0, MakeChar("Hero", "stillblade"));
        var dungeon = new Dungeon(5, 5, "test") { WanderingTableId = "test" };
        gs.EnterDungeon(dungeon, "test");

        gs.TriggerEncounter();

        Assert.Null(gs.CurrentParley);
        Assert.Equal(GameMode.Combat, gs.Mode);
    }
}
