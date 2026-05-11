using RPC.Engine;
using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Save;

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
}
