using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Save;
using RPC.Engine.Town;

namespace RPC.Tests;

public class ActionLogTests : IDisposable
{
    private readonly string _testSavePath;

    public ActionLogTests()
    {
        _testSavePath = Path.Combine(Path.GetTempPath(), $"test_action_log_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testSavePath))
            File.Delete(_testSavePath);
    }

    [Fact]
    public void EnterDungeon_Then_ReturnToTown_EmitsDungeonEventsInOrder()
    {
        var gs = new GameState(seed: 42);
        gs.EnterDungeon(new Dungeon(3, 3, "test"), "broken_engine");

        Assert.Single(gs.ActionLog);
        Assert.Equal("dungeon", gs.ActionLog[0].Category);
        Assert.Equal("dungeon_entered", gs.ActionLog[0].Type);
        Assert.Equal("broken_engine", gs.ActionLog[0].Payload["dungeonType"]);

        gs.ReturnToTown();

        Assert.Equal(2, gs.ActionLog.Count);
        Assert.Equal("dungeon", gs.ActionLog[1].Category);
        Assert.Equal("dungeon_completed", gs.ActionLog[1].Type);
        Assert.Equal("broken_engine", gs.ActionLog[1].Payload["dungeonType"]);
    }

    [Fact]
    public void TriggerEncounter_EmitsStartedAndWon_WithMatchingEncounterId()
    {
        var gs = new GameState(seed: 42);
        gs.EnterDungeon(new Dungeon(3, 3, "test"), "test");
        gs.ActionLog.Clear();

        var encounter = new EncounterDef("test_enc", "Test", new[]
        {
            new EnemySpawn("rat", 1)
        }, 0);

        gs.TriggerEncounter(encounter);

        var started = gs.ActionLog.FirstOrDefault(e => e.Type == "encounter_started");
        Assert.NotNull(started);
        var encounterId = started.Payload["encounterId"];
        Assert.False(string.IsNullOrEmpty(encounterId));

        // Combat should be active
        Assert.NotNull(gs.Combat);

        // Resolve combat by submitting Attack actions on player turns
        while (gs.Mode == GameMode.Combat)
        {
            var currentActor = gs.Combat!.CurrentActor;
            Assert.NotNull(currentActor);
            Assert.True(currentActor.Value.IsPlayer, "Expected player turn");

            var target = gs.Combat.Combatants.First(c => !c.IsPlayer && c.IsAlive);
            var action = new CombatAction(currentActor.Value.Id, ActionType.Attack, target.Id, null, null);
            gs.SubmitCombatAction(action);
        }

        Assert.Equal(GameMode.Exploration, gs.Mode);

        var won = gs.ActionLog.FirstOrDefault(e => e.Type == "encounter_won");
        Assert.NotNull(won);
        Assert.Equal(encounterId, won.Payload["encounterId"]);
    }

    [Fact]
    public void SaveLoad_PreservesEventOrdering()
    {
        var gs = new GameState(seed: 42);
        gs.EnterDungeon(new Dungeon(3, 3, "test"), "test");
        gs.ReturnToTown();

        gs.SaveGame(_testSavePath);

        var gs2 = new GameState(seed: 99);
        var loaded = gs2.LoadGame(_testSavePath);
        Assert.True(loaded);

        Assert.Equal(gs.ActionLog.Count, gs2.ActionLog.Count);
        for (int i = 0; i < gs.ActionLog.Count; i++)
        {
            Assert.Equal(gs.ActionLog[i].Turn, gs2.ActionLog[i].Turn);
            Assert.Equal(gs.ActionLog[i].Category, gs2.ActionLog[i].Category);
            Assert.Equal(gs.ActionLog[i].Type, gs2.ActionLog[i].Type);
            Assert.Equal(gs.ActionLog[i].Payload["dungeonType"], gs2.ActionLog[i].Payload["dungeonType"]);
        }
    }

    [Fact]
    public void Payload_Shape_HasRequiredKeys()
    {
        var gs = new GameState(seed: 42);
        gs.EnterDungeon(new Dungeon(3, 3, "test"), "crypt");

        var entry = gs.ActionLog[0];
        Assert.Equal("dungeon", entry.Category);
        Assert.Equal("dungeon_entered", entry.Type);
        Assert.True(entry.Payload.ContainsKey("dungeonType"));
        Assert.Equal("crypt", entry.Payload["dungeonType"]);
        Assert.True(entry.Turn > 0);
    }

    [Fact]
    public void CompleteMission_SideMission_EmitsMissionCompletedAndRepChanged()
    {
        var gs = new GameState(seed: 1);
        gs.ActionLog.Clear();
        gs.Town.QuestLog.Add(new ActiveMission("m1", "Test", "Desc", 5, "bureau", "active", MissionType.Side));

        gs.CompleteMission("m1");

        var missionCompleted = gs.ActionLog.FirstOrDefault(e => e.Type == "mission_completed");
        Assert.NotNull(missionCompleted);
        Assert.Equal("faction", missionCompleted.Category);
        Assert.Equal("bureau", missionCompleted.Payload["factionId"]);
        Assert.Equal("side", missionCompleted.Payload["type"]);

        var repEntries = gs.ActionLog.Where(e => e.Type == "rep_changed").ToList();
        Assert.Equal(2, repEntries.Count);
        Assert.Equal("bureau", repEntries[0].Payload["factionId"]);
        Assert.Equal("convocation", repEntries[1].Payload["factionId"]);
    }

    [Fact]
    public void CompleteMission_CrossesVendorThreshold_EmitsVendorUnlocked()
    {
        var gs = new GameState(seed: 1);
        gs.Reputation["bureau"] = 23;
        gs.ActionLog.Clear();
        gs.Town.QuestLog.Add(new ActiveMission("m1", "Test", "Desc", 5, "bureau", "active", MissionType.Side));

        gs.CompleteMission("m1");

        var vendorUnlocked = gs.ActionLog.FirstOrDefault(e => e.Type == "vendor_unlocked");
        Assert.NotNull(vendorUnlocked);
        Assert.Equal("faction", vendorUnlocked.Category);
        Assert.Equal("bureau", vendorUnlocked.Payload["factionId"]);
        Assert.Equal("25", vendorUnlocked.Payload["threshold"]);
    }

    [Fact]
    public void CompleteMission_AlreadyUnlocked_DoesNotEmitVendorUnlocked()
    {
        var gs = new GameState(seed: 1);
        gs.Reputation["bureau"] = 30;
        gs.ActionLog.Clear();
        gs.Town.QuestLog.Add(new ActiveMission("m1", "Test", "Desc", 5, "bureau", "active", MissionType.Side));

        gs.CompleteMission("m1");

        Assert.DoesNotContain(gs.ActionLog, e => e.Type == "vendor_unlocked");
    }

    [Fact]
    public void FailMission_EmitsMissionFailedAndRepChanged()
    {
        var gs = new GameState(seed: 1);
        gs.ActionLog.Clear();
        gs.Town.QuestLog.Add(new ActiveMission("m1", "Test", "Desc", 5, "bureau", "active", MissionType.Side));

        gs.FailMission("m1");

        var missionFailed = gs.ActionLog.FirstOrDefault(e => e.Type == "mission_failed");
        Assert.NotNull(missionFailed);
        Assert.Equal("faction", missionFailed.Category);
        Assert.Equal("bureau", missionFailed.Payload["factionId"]);

        var repEntries = gs.ActionLog.Where(e => e.Type == "rep_changed").ToList();
        Assert.Equal(2, repEntries.Count);
    }

    [Fact]
    public void Travel_EmitsTravelStarted()
    {
        var gs = new GameState(seed: 42);
        gs.ActionLog.Clear();

        gs.Travel("broken_engine");

        var travelStarted = gs.ActionLog.FirstOrDefault(e => e.Type == "travel_started");
        Assert.NotNull(travelStarted);
        Assert.Equal("overworld", travelStarted.Category);
        Assert.Equal("the_reach", travelStarted.Payload["from"]);
        Assert.Equal("broken_engine", travelStarted.Payload["to"]);
    }

    [Fact]
    public void Travel_ZeroEncounters_EmitsTownReached()
    {
        for (int seed = 0; seed < 500; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Travel("broken_engine");
            if (gs.RolledTravelEncounterCount != 0) continue;

            gs.ActionLog.Clear();
            gs.Travel("the_reach");
            if (gs.RolledTravelEncounterCount != 0) continue;

            var townReached = gs.ActionLog.FirstOrDefault(e => e.Type == "town_reached");
            Assert.NotNull(townReached);
            Assert.Equal("overworld", townReached.Category);
            Assert.Equal("the_reach", townReached.Payload["townId"]);
            return;
        }
        Assert.Fail("Expected at least one seed to roll 0 encounters on both legs");
    }

    [Fact]
    public void ResolveTravelEncounter_EmitsTravelEncounterResolved()
    {
        for (int seed = 0; seed < 300; seed++)
        {
            var gs = new GameState(seed: seed);
            gs.Party.SetMember(0, new CharacterState(
                new Guid("11111111-1111-1111-1111-111111111111"),
                "Hero", "stillblade", 1, 0,
                new BaseStats(4, 4, 4, 4, 4), 20, Equipment.Empty,
                Array.Empty<string>(), 0));
            gs.Travel("broken_engine");

            if (gs.CurrentTravelEncounter?.ResolutionType == "stat_test")
            {
                gs.ActionLog.Clear();
                gs.ResolveTravelEncounter("roll");

                var resolved = gs.ActionLog.FirstOrDefault(e => e.Type == "travel_encounter_resolved");
                Assert.NotNull(resolved);
                Assert.Equal("overworld", resolved.Category);
                Assert.Equal("stat_test", resolved.Payload["resolutionType"]);
                Assert.Equal("roll", resolved.Payload["choice"]);
                return;
            }
            else if (gs.CurrentTravelEncounter?.ResolutionType == "dialogue")
            {
                gs.ActionLog.Clear();
                gs.ResolveTravelEncounter("Help");

                var resolved = gs.ActionLog.FirstOrDefault(e => e.Type == "travel_encounter_resolved");
                Assert.NotNull(resolved);
                Assert.Equal("overworld", resolved.Category);
                Assert.Equal("dialogue", resolved.Payload["resolutionType"]);
                Assert.Equal("Help", resolved.Payload["choice"]);
                return;
            }
        }
        Assert.Fail("Expected at least one seed to produce a non-combat encounter");
    }

    [Fact]
    public void DiscoverSecret_EmitsDungeonEvent()
    {
        var gs = new GameState(seed: 42);
        gs.ActionLog.Clear();

        gs.DiscoverSecret("breakable_wall", "secret-pump-room-east");

        var entry = gs.ActionLog.FirstOrDefault(e => e.Type == "secret_discovered");
        Assert.NotNull(entry);
        Assert.Equal("dungeon", entry.Category);
        Assert.Equal("breakable_wall", entry.Payload["secretType"]);
        Assert.Equal("secret-pump-room-east", entry.Payload["secretId"]);
    }

    [Fact]
    public void ChooseSettlementFate_EmitsDungeonEvent()
    {
        var gs = new GameState(seed: 42);
        gs.ActionLog.Clear();

        gs.ChooseSettlementFate("the_reach", "saved");

        var entry = gs.ActionLog.FirstOrDefault(e => e.Type == "settlement_fate_chosen");
        Assert.NotNull(entry);
        Assert.Equal("dungeon", entry.Category);
        Assert.Equal("the_reach", entry.Payload["settlementId"]);
        Assert.Equal("saved", entry.Payload["fate"]);
    }

    [Fact]
    public void ActionLog_SizeCapWarning_At1000Events()
    {
        var gs = new GameState(seed: 42);
        var sw = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(sw);
        try
        {
            for (int i = 0; i < 1000; i++)
            {
                gs.DiscoverSecret("breakable_wall", $"secret-{i}");
            }
            var output = sw.ToString();
            Assert.Contains("ActionLog size warning", output);
            Assert.Contains("1000", output);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}
