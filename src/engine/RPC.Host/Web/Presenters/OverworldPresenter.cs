using RPC.Engine;

namespace RPC.Host.Web.Presenters;

public static class OverworldPresenter
{
    public static object Present(GameState state)
    {
        return new
        {
            currentNodeId = state.Overworld.CurrentNodeId,
            nodes = state.Overworld.Nodes.Values.Select(n => new
            {
                id = n.Id,
                name = n.Name,
                type = n.Type.ToString().ToLowerInvariant(),
                factionPresence = n.FactionPresence,
                dungeonTemplateId = n.DungeonTemplateId
            }).ToArray(),
            routes = state.Overworld.Routes.Select(r => new
            {
                from = r.From,
                to = r.To,
                distance = r.Distance,
                dangerRating = r.DangerRating,
                terrain = r.Terrain,
                status = r.Status.ToString()
            }).ToArray(),
            turns = state.Overworld.Turns,
            currentAct = state.CurrentAct
        };
    }

    public static object? PresentTravelEncounter(GameState state)
    {
        if (state.CurrentTravelEncounter == null)
            return null;

        var te = state.CurrentTravelEncounter;
        return new
        {
            id = te.Id,
            name = te.Name,
            resolutionType = te.ResolutionType,
            statName = te.StatName,
            factionId = te.FactionId,
            reputationValue = te.ReputationValue,
            hasSurpriseRound = te.HasSurpriseRound,
            priceTier = te.PriceTier,
            options = te.Options
        };
    }
}
