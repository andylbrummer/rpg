using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Overworld;

namespace RPC.Tests;

public class FactionInteractionTests
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
    public void CheckAndResolveInteractions_NoOp_WhenSingleFactionExecuting()
    {
        var state = CreateState(1);
        var config = CampaignConfig.Roll(new GameRandom(1));
        state.GenerateOverworld(config);
        state.Mode = GameMode.Exploration;

        // Delay all but one faction so only mastermind is Executing at turn 15
        foreach (var faction in CampaignConfig.FactionPool)
        {
            if (faction != config.Mastermind)
                state.Campaign.FactionTimelineModifiers[faction] = 10;
        }
        state.Overworld.Turns = 15;

        var service = new CampaignService(null);
        var interaction = new FactionInteractionService(service);
        interaction.CheckAndResolveInteractions(state);

        Assert.DoesNotContain(state.ActionLog, e => e.Type == "resolution");
    }

    [Fact]
    public void CheckAndResolveInteractions_Resolves_WhenTwoFactionsExecuting()
    {
        var state = CreateState(1);
        var config = CampaignConfig.Roll(new GameRandom(1));
        state.GenerateOverworld(config);
        state.Mode = GameMode.Exploration;

        // Push two factions to Executing, delay others
        state.Campaign.FactionTimelineModifiers[config.Mastermind] = -10;
        state.Campaign.FactionTimelineModifiers[config.Threat] = -10;
        foreach (var faction in CampaignConfig.FactionPool)
        {
            if (faction != config.Mastermind && faction != config.Threat)
                state.Campaign.FactionTimelineModifiers[faction] = 10;
        }
        state.Overworld.Turns = 15;

        var service = new CampaignService(null);
        var interaction = new FactionInteractionService(service);
        interaction.CheckAndResolveInteractions(state);

        Assert.Contains(state.ActionLog, e => e.Type == "resolution");
    }

    [Fact]
    public void CheckAndResolveInteractions_Pairwise_WhenThreeFactionsExecuting()
    {
        var state = CreateState(1);
        var config = CampaignConfig.Roll(new GameRandom(1));
        state.GenerateOverworld(config);
        state.Mode = GameMode.Exploration;

        // Push three roles to Executing; note patron and mastermind may be same faction
        var targetFactions = new HashSet<string> { config.Mastermind, config.Threat, config.Patron };
        foreach (var faction in targetFactions)
            state.Campaign.FactionTimelineModifiers[faction] = -10;
        foreach (var faction in CampaignConfig.FactionPool)
        {
            if (!targetFactions.Contains(faction))
                state.Campaign.FactionTimelineModifiers[faction] = 10;
        }
        state.Overworld.Turns = 15;

        var service = new CampaignService(null);

        var executing = CampaignConfig.FactionPool
            .Where(f => service.GetFactionState(state, f) == FactionState.Executing)
            .ToList();
        Assert.Equal(targetFactions.Count, executing.Count);

        var interaction = new FactionInteractionService(service);
        interaction.CheckAndResolveInteractions(state);

        var expectedPairs = targetFactions.Count * (targetFactions.Count - 1) / 2;
        var resolutions = state.ActionLog.Where(e => e.Type == "resolution").ToList();
        Assert.Equal(expectedPairs, resolutions.Count);
    }

    [Fact]
    public void CheckAndResolveInteractions_Emits_CollisionLog()
    {
        var state = CreateState(1);
        var config = CampaignConfig.Roll(new GameRandom(1));
        state.GenerateOverworld(config);
        state.Mode = GameMode.Exploration;

        state.Campaign.FactionTimelineModifiers[config.Mastermind] = -10;
        state.Campaign.FactionTimelineModifiers[config.Threat] = -10;
        foreach (var faction in CampaignConfig.FactionPool)
        {
            if (faction != config.Mastermind && faction != config.Threat)
                state.Campaign.FactionTimelineModifiers[faction] = 10;
        }
        state.Overworld.Turns = 15;

        var service = new CampaignService(null);
        var interaction = new FactionInteractionService(service);
        interaction.CheckAndResolveInteractions(state);

        Assert.Contains(state.ActionLog, e => e.Type == "executing_collision");
    }

    [Fact]
    public void CheckAndResolveInteractions_NoOp_WhenNoCampaignConfig()
    {
        var state = CreateState(1);
        state.Mode = GameMode.Exploration;

        var service = new CampaignService(null);
        var interaction = new FactionInteractionService(service);
        interaction.CheckAndResolveInteractions(state);

        Assert.Empty(state.ActionLog);
    }
}
