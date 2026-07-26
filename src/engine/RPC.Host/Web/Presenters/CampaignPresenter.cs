using RPC.Engine;
using RPC.Engine.Campaign;

namespace RPC.Host.Web.Presenters;

public static class CampaignPresenter
{
    public static object PresentReputation(GameState state)
    {
        return state.Reputation.ToDictionary(r => r.Key, r => r.Value);
    }

    public static object PresentHeat(GameState state)
    {
        return new
        {
            value = state.Heat.Value,
            tier = state.Heat.Tier.ToString().ToLowerInvariant()
        };
    }

    public static object PresentEvidence(GameState state)
    {
        return new
        {
            suspectedFaction = state.Evidence.SuspectedFaction,
            canConfront = state.Evidence.Counters.Values.Any(v => v >= 5),
            canAccuse = state.Evidence.Counters.Values.Any(v => v >= 7),
            hasIrrefutableProof = state.Evidence.Counters.Values.Any(v => v >= 10),
            accusedFaction = state.AccusedFaction,
            // Whether the party could throw in with whoever is really behind the scheme, and
            // whether they already have. Both are plain booleans on purpose: the mastermind's
            // identity is the campaign's hidden information, and the client only needs to know
            // that the option exists, not who it points at. Mirrors CampaignService.ChooseBetrayal,
            // which requires a mastermind, at least one piece of evidence against them, and a run
            // not already committed.
            canBetray = !state.Campaign.BetrayalPath
                && !string.IsNullOrEmpty(state.CampaignConfig?.Mastermind)
                && state.Evidence.Counters.TryGetValue(state.CampaignConfig!.Mastermind, out var mastermindEvidence)
                && mastermindEvidence >= 1,
            onBetrayalPath = state.Campaign.BetrayalPath,
            mastermindRevealed = state.CampaignConfig != null && state.AccusedFaction == state.CampaignConfig.Mastermind,
            mastermindAdvantage = state.MastermindAdvantage,
            finalDungeonUnlocked = state.FinalDungeonUnlocked
        };
    }

    public static object PresentWildCardAlliance(GameState state)
    {
        return new
        {
            status = state.WildCardAllianceStatus.ToString().ToLowerInvariant(),
            factionId = state.WildCardFactionId,
            turn = state.WildCardAllianceTurn
        };
    }

    public static object PresentWorldState(GameState state)
    {
        return new
        {
            settlements = state.WorldState.Settlements,
            accessibleDungeons = state.WorldState.AccessibleDungeons,
            factionTerritory = state.WorldState.FactionTerritory
        };
    }

    public static object PresentFactionStates(GameState state)
    {
        return CampaignConfig.FactionPool.ToDictionary(
            f => f,
            f => state.GetFactionState(f).ToString().ToLowerInvariant());
    }
}
