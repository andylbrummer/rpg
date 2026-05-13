using RPC.Engine.Campaign;
using RPC.Engine.Combat;

namespace RPC.Engine.Overworld;

public static class RouteStatusSystem
{
    public static bool CanTravel(OverworldRoute route)
    {
        return route.Status != RouteStatus.Blocked;
    }

    public static int GetEffectiveDangerRating(OverworldRoute route)
    {
        return route.Status switch
        {
            RouteStatus.Contested => Math.Min(5, route.DangerRating + 2),
            RouteStatus.BloomAffected => Math.Min(5, route.DangerRating + 1),
            _ => route.DangerRating
        };
    }

    public static string GetTravelEffectDescription(OverworldRoute route)
    {
        return route.Status switch
        {
            RouteStatus.Contested => "Route is contested. Increased danger and encounter chance.",
            RouteStatus.BloomAffected => "Bloom has spread to this route. Unusual encounters possible.",
            RouteStatus.Blocked => "Route is impassable.",
            _ => "Route is clear."
        };
    }

    public static void ApplyTurnMilestoneTransitions(OverworldState overworld, CampaignConfig? config, GameRandom rng)
    {
        var turn = overworld.Turns;
        if (config == null) return;

        // Faction timeline transitions: when factions reach Executing, contest their routes
        foreach (var (factionId, timeline) in config.FactionTimelines)
        {
            if (turn == timeline.Executing)
            {
                ContestFactionRoutes(overworld, factionId, rng);
            }
        }

        // Global milestone transitions
        if (turn == 8)
        {
            EscalateRandomRoute(overworld, rng);
        }
        if (turn == 15)
        {
            BlockWeakestRoute(overworld, rng);
        }
        if (turn == 22)
        {
            BloomRandomRoute(overworld, rng);
        }
    }

    public static void ApplyFactionActionTransition(OverworldState overworld, string factionId, GameRandom rng)
    {
        ContestFactionRoutes(overworld, factionId, rng);
    }

    public static void ApplyComplicationTransition(OverworldState overworld, ComplicationType complication, GameRandom rng)
    {
        switch (complication)
        {
            case ComplicationType.OpenWar:
                ContestRandomRoutes(overworld, rng, count: 2);
                break;
            case ComplicationType.BloomSiege:
                BloomRandomRoute(overworld, rng);
                BloomRandomRoute(overworld, rng);
                break;
            case ComplicationType.ClosingPasses:
                BlockRandomRoute(overworld, rng);
                break;
            case ComplicationType.TitheCollapse:
                // No direct route effect
                break;
            case ComplicationType.ErraticEngine:
                EscalateRandomRoute(overworld, rng);
                break;
            case ComplicationType.MissingTeam:
                // No direct route effect
                break;
        }
    }

    private static void ContestFactionRoutes(OverworldState overworld, string factionId, GameRandom rng)
    {
        var affected = overworld.Routes
            .Where(r =>
            {
                var fromNode = overworld.Nodes.GetValueOrDefault(r.From);
                var toNode = overworld.Nodes.GetValueOrDefault(r.To);
                return (fromNode?.FactionPresence.Contains(factionId) == true ||
                        toNode?.FactionPresence.Contains(factionId) == true) &&
                       r.Status == RouteStatus.Open;
            })
            .ToList();

        foreach (var route in affected)
        {
            route.Status = RouteStatus.Contested;
        }
    }

    private static void ContestRandomRoutes(OverworldState overworld, GameRandom rng, int count = 1)
    {
        var openRoutes = overworld.Routes.Where(r => r.Status == RouteStatus.Open).ToList();
        for (int i = 0; i < count && openRoutes.Count > 0; i++)
        {
            var idx = rng.Next(openRoutes.Count);
            openRoutes[idx].Status = RouteStatus.Contested;
            openRoutes.RemoveAt(idx);
        }
    }

    private static void BlockRandomRoute(OverworldState overworld, GameRandom rng)
    {
        var blockable = overworld.Routes.Where(r => r.Status != RouteStatus.Blocked).ToList();
        if (blockable.Count > 0)
        {
            var idx = rng.Next(blockable.Count);
            blockable[idx].Status = RouteStatus.Blocked;
        }
    }

    private static void BloomRandomRoute(OverworldState overworld, GameRandom rng)
    {
        var affectable = overworld.Routes.Where(r => r.Status != RouteStatus.Blocked).ToList();
        if (affectable.Count > 0)
        {
            var idx = rng.Next(affectable.Count);
            affectable[idx].Status = RouteStatus.BloomAffected;
        }
    }

    private static void EscalateRandomRoute(OverworldState overworld, GameRandom rng)
    {
        var openRoutes = overworld.Routes.Where(r => r.Status == RouteStatus.Open).ToList();
        if (openRoutes.Count > 0)
        {
            var idx = rng.Next(openRoutes.Count);
            openRoutes[idx].Status = RouteStatus.Contested;
            return;
        }

        var contestedRoutes = overworld.Routes.Where(r => r.Status == RouteStatus.Contested).ToList();
        if (contestedRoutes.Count > 0)
        {
            var idx = rng.Next(contestedRoutes.Count);
            contestedRoutes[idx].Status = RouteStatus.Blocked;
        }
    }

    private static void BlockWeakestRoute(OverworldState overworld, GameRandom rng)
    {
        var weakest = overworld.Routes
            .Where(r => r.Status != RouteStatus.Blocked)
            .OrderBy(r => r.DangerRating)
            .FirstOrDefault();
        if (weakest != null)
        {
            weakest.Status = RouteStatus.Blocked;
        }
    }
}
