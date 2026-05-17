using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Overworld;

namespace RPC.Tests;

public class EventSchedulerTests
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
    public void Tick_EventFires_AtTurnThreshold()
    {
        var state = CreateState(1);
        var config = CampaignConfig.Roll(new GameRandom(1));
        state.GenerateOverworld(config);
        state.CurrentComplication = null; // Isolate test from loaded complication events

        var evt = new CampaignEventDef(
            "test_event", "Test", "Desc", 5, "patron",
            "reputation_change", null, null, null, "threat", 5);

        state.CurrentScheme = new SchemeDef("Test", "Test", "Test", "feel", Array.Empty<string>(), new[] { evt });
        state.Overworld.Turns = 5;
        state.Mode = GameMode.Exploration;

        var service = new CampaignService(null);
        var scheduler = new EventScheduler(service);
        scheduler.Tick(state);

        Assert.Contains(state.ActionLog, e => e.Type == "event_fired" && e.Payload["eventId"] == "test_event");
    }

    [Fact]
    public void Tick_EventDoesNotFire_BeforeTurnThreshold()
    {
        var state = CreateState(1);
        var config = CampaignConfig.Roll(new GameRandom(1));
        state.GenerateOverworld(config);
        state.CurrentComplication = null;

        var evt = new CampaignEventDef(
            "test_event", "Test", "Desc", 10, "patron",
            "reputation_change", null, null, null, "threat", 5);

        state.CurrentScheme = new SchemeDef("Test", "Test", "Test", "feel", Array.Empty<string>(), new[] { evt });
        state.Overworld.Turns = 5;
        state.Mode = GameMode.Exploration;

        var service = new CampaignService(null);
        var scheduler = new EventScheduler(service);
        scheduler.Tick(state);

        Assert.DoesNotContain(state.ActionLog, e => e.Type == "event_fired");
    }

    [Fact]
    public void Tick_EventDoesNotFire_Twice()
    {
        var state = CreateState(1);
        var config = CampaignConfig.Roll(new GameRandom(1));
        state.GenerateOverworld(config);
        state.CurrentComplication = null;

        var evt = new CampaignEventDef(
            "test_event", "Test", "Desc", 5, "patron",
            "reputation_change", null, null, null, "threat", 5);

        state.CurrentScheme = new SchemeDef("Test", "Test", "Test", "feel", Array.Empty<string>(), new[] { evt });
        state.Overworld.Turns = 5;
        state.Mode = GameMode.Exploration;

        var service = new CampaignService(null);
        var scheduler = new EventScheduler(service);
        scheduler.Tick(state);
        scheduler.Tick(state);

        var firedCount = state.ActionLog.Count(e => e.Type == "event_fired" && e.Payload["eventId"] == "test_event");
        Assert.Equal(1, firedCount);
    }

    [Fact]
    public void Tick_Suppressed_InMenuMode()
    {
        var state = CreateState(1);
        var config = CampaignConfig.Roll(new GameRandom(1));
        state.GenerateOverworld(config);
        state.CurrentComplication = null;

        var evt = new CampaignEventDef(
            "test_event", "Test", "Desc", 5, "patron",
            "reputation_change", null, null, null, "threat", 5);

        state.CurrentScheme = new SchemeDef("Test", "Test", "Test", "feel", Array.Empty<string>(), new[] { evt });
        state.Overworld.Turns = 5;
        state.Mode = GameMode.Menu;

        var service = new CampaignService(null);
        var scheduler = new EventScheduler(service);
        scheduler.Tick(state);

        Assert.DoesNotContain(state.ActionLog, e => e.Type == "event_fired");
    }

    [Fact]
    public void Tick_ReputationChange_AppliesDelta()
    {
        var state = CreateState(1);
        var config = CampaignConfig.Roll(new GameRandom(1));
        state.GenerateOverworld(config);
        state.CurrentComplication = null;

        var targetFaction = config.Threat;
        state.Reputation[targetFaction] = 10;

        var evt = new CampaignEventDef(
            "test_event", "Test", "Desc", 5, "patron",
            "reputation_change", null, null, null, targetFaction, -3);

        state.CurrentScheme = new SchemeDef("Test", "Test", "Test", "feel", Array.Empty<string>(), new[] { evt });
        state.Overworld.Turns = 5;
        state.Mode = GameMode.Exploration;

        var service = new CampaignService(null);
        var scheduler = new EventScheduler(service);
        scheduler.Tick(state);

        Assert.Equal(7, state.Reputation[targetFaction]);
    }

    [Fact]
    public void Tick_EventFires_Unconditionally_AtTurnThreshold()
    {
        var state = CreateState(1);
        var config = CampaignConfig.Roll(new GameRandom(1));
        state.GenerateOverworld(config);
        state.CurrentComplication = null;

        var evt = new CampaignEventDef(
            "test_event", "Test", "Desc", 5, "patron",
            "reputation_change", null, null, null, "threat", 5);

        state.CurrentScheme = new SchemeDef("Test", "Test", "Test", "feel", Array.Empty<string>(), new[] { evt });
        state.Overworld.Turns = 5;
        state.Mode = GameMode.Exploration;

        // Delay patron's timeline so they are still Investigating at turn 5
        state.Campaign.FactionTimelineModifiers[config.Patron] = 10;

        var service = new CampaignService(null);
        var scheduler = new EventScheduler(service);
        scheduler.Tick(state);

        // Scheme events fire at turn threshold regardless of faction state
        Assert.Contains(state.ActionLog, e => e.Type == "event_fired" && e.Payload["eventId"] == "test_event");
    }

    [Fact]
    public void Tick_WorldStateChange_ModifiesSettlements()
    {
        var state = CreateState(1);
        var config = CampaignConfig.Roll(new GameRandom(1));
        state.GenerateOverworld(config);
        state.CurrentComplication = null;

        var evt = new CampaignEventDef(
            "test_event", "Test", "Desc", 5, "patron",
            "world_state_change", null, null, null, null, 0, "ashford", "evacuated");

        state.CurrentScheme = new SchemeDef("Test", "Test", "Test", "feel", Array.Empty<string>(), new[] { evt });
        state.Overworld.Turns = 5;
        state.Mode = GameMode.Exploration;

        var service = new CampaignService(null);
        var scheduler = new EventScheduler(service);
        scheduler.Tick(state);

        Assert.Equal("evacuated", state.WorldState.Settlements.GetValueOrDefault("ashford"));
    }
}
