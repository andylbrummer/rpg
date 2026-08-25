using RPC.Engine.Combat;
using RPC.Engine.Overworld;

namespace RPC.Engine.Campaign;

public record FactionResolution(
    string PrimaryFactionId,
    string SecondaryFactionId,
    string ResolutionType,
    string Description);

public class FactionInteractionService
{
    private readonly CampaignService _campaignService;

    public FactionInteractionService(CampaignService campaignService)
    {
        _campaignService = campaignService;
    }

    public void CheckAndResolveInteractions(GameState state)
    {
        var config = state.CampaignConfig;
        if (config == null) return;

        var executingFactions = CampaignConfig.FactionPool
            .Where(f => _campaignService.GetFactionState(state, f) == FactionState.Executing)
            .ToList();

        if (executingFactions.Count < 2) return;

        // Priority: Mastermind > Threat > Wild Card > others
        var ordered = executingFactions
            .OrderByDescending(f => GetFactionPriority(f, config))
            .ToList();

        var resolvedPairs = new HashSet<(string, string)>();

        for (int i = 0; i < ordered.Count; i++)
        {
            for (int j = i + 1; j < ordered.Count; j++)
            {
                var a = ordered[i];
                var b = ordered[j];
                var pairKey = (a, b);
                if (resolvedPairs.Contains(pairKey)) continue;
                resolvedPairs.Add(pairKey);

                ResolvePair(state, a, b, config);
            }
        }

        if (executingFactions.Count >= 2)
        {
            state.EmitActionLog("faction", "executing_collision", new Dictionary<string, string>
            {
                { "factions", string.Join(",", executingFactions) },
                { "count", executingFactions.Count.ToString() }
            });
        }
    }

    private static int GetFactionPriority(string factionId, CampaignConfig config)
    {
        if (factionId == config.Mastermind) return 4;
        if (factionId == config.Threat) return 3;
        if (factionId == config.WildCard) return 2;
        if (factionId == config.Patron) return 1;
        return 0;
    }

    private static void ResolvePair(GameState state, string primary, string secondary, CampaignConfig config)
    {
        var rng = new GameRandom(state.Overworld.Turns + primary.GetHashCode() + secondary.GetHashCode());
        var roll = rng.Roll(1, 100);

        string resolutionType;
        string description;

        if (roll <= 30)
        {
            resolutionType = "route_battle";
            description = $"{primary} and {secondary} clash on the roads.";
            RouteStatusSystem.ApplyFactionActionTransition(state.Overworld, primary, rng);
            RouteStatusSystem.ApplyFactionActionTransition(state.Overworld, secondary, rng);
        }
        else if (roll <= 50)
        {
            resolutionType = "town_destruction";
            description = $"{primary} assaults a settlement held by {secondary}.";
            DestroyRandomSettlement(state, rng);
        }
        else if (roll <= 70)
        {
            resolutionType = "scheme_acceleration";
            description = $"{primary} accelerates their scheme, pressured by {secondary}.";
            state.Campaign.FactionTimelineModifiers[primary] = Math.Max(-3,
                state.Campaign.FactionTimelineModifiers.GetValueOrDefault(primary, 0) - 1);
        }
        else if (roll <= 85)
        {
            resolutionType = "unexpected_alliance";
            description = $"{primary} and {secondary} form a temporary alliance.";
            state.Reputation[secondary] = Math.Min(100, state.Reputation[secondary] + 5);
        }
        else
        {
            resolutionType = "stalemate";
            description = $"{primary} and {secondary} reach a bloody stalemate.";
        }

        state.EmitActionLog("faction", "resolution", new Dictionary<string, string>
        {
            { "primary", primary },
            { "secondary", secondary },
            { "type", resolutionType },
            { "description", description }
        });
    }

    private static void DestroyRandomSettlement(GameState state, GameRandom rng)
    {
        var settlements = state.WorldState.Settlements.Keys.ToList();
        if (settlements.Count == 0) return;

        var target = settlements[rng.Next(settlements.Count)];
        state.WorldState.Settlements[target] = "destroyed";

        // Also block routes connected to the settlement
        foreach (var route in state.Overworld.Routes)
        {
            if (route.From == target || route.To == target)
            {
                route.Status = RouteStatus.Blocked;
            }
        }
    }
}
