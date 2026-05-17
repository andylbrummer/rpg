using RPC.Engine.Campaign;
using RPC.Engine.Combat;
using RPC.Engine.Overworld;

namespace RPC.Tests;

public class RouteStatusTests
{
    [Fact]
    public void CanTravel_BlockedRoute_ReturnsFalse()
    {
        var route = new OverworldRoute("a", "b", 2, 3, "plains") { Status = RouteStatus.Blocked };
        Assert.False(RouteStatusSystem.CanTravel(route));
    }

    [Fact]
    public void CanTravel_OpenRoute_ReturnsTrue()
    {
        var route = new OverworldRoute("a", "b", 2, 3, "plains") { Status = RouteStatus.Open };
        Assert.True(RouteStatusSystem.CanTravel(route));
    }

    [Fact]
    public void CanTravel_ContestedRoute_ReturnsTrue()
    {
        var route = new OverworldRoute("a", "b", 2, 3, "plains") { Status = RouteStatus.Contested };
        Assert.True(RouteStatusSystem.CanTravel(route));
    }

    [Fact]
    public void GetEffectiveDangerRating_Contested_Increases()
    {
        var route = new OverworldRoute("a", "b", 2, 3, "plains") { Status = RouteStatus.Contested };
        Assert.Equal(5, RouteStatusSystem.GetEffectiveDangerRating(route));
    }

    [Fact]
    public void GetEffectiveDangerRating_BloomAffected_SlightlyIncreases()
    {
        var route = new OverworldRoute("a", "b", 2, 3, "plains") { Status = RouteStatus.BloomAffected };
        Assert.Equal(4, RouteStatusSystem.GetEffectiveDangerRating(route));
    }

    [Fact]
    public void GetEffectiveDangerRating_Open_Unchanged()
    {
        var route = new OverworldRoute("a", "b", 2, 3, "plains") { Status = RouteStatus.Open };
        Assert.Equal(3, RouteStatusSystem.GetEffectiveDangerRating(route));
    }

    [Fact]
    public void GetEffectiveDangerRating_CappedAtFive()
    {
        var route = new OverworldRoute("a", "b", 2, 5, "plains") { Status = RouteStatus.Contested };
        Assert.Equal(5, RouteStatusSystem.GetEffectiveDangerRating(route));
    }

    [Fact]
    public void Overworld_Travel_BlockedRoute_ReturnsFalse()
    {
        var ow = new OverworldState();
        ow.Nodes["a"] = new OverworldNode("a", "A", NodeType.Town);
        ow.Nodes["b"] = new OverworldNode("b", "B", NodeType.Town);
        ow.Routes.Add(new OverworldRoute("a", "b", 2, 3, "plains") { Status = RouteStatus.Blocked });
        ow.CurrentNodeId = "a";

        var result = ow.Travel("b");
        Assert.False(result);
    }

    [Fact]
    public void Overworld_Travel_ContestedRoute_Succeeds()
    {
        var ow = new OverworldState();
        ow.Nodes["a"] = new OverworldNode("a", "A", NodeType.Town);
        ow.Nodes["b"] = new OverworldNode("b", "B", NodeType.Town);
        ow.Routes.Add(new OverworldRoute("a", "b", 2, 3, "plains") { Status = RouteStatus.Contested });
        ow.CurrentNodeId = "a";

        var result = ow.Travel("b");
        Assert.True(result);
    }

    [Fact]
    public void ApplyFactionActionTransition_ContestsFactionRoutes()
    {
        var ow = new OverworldState();
        var townNode = new OverworldNode("town", "Town", NodeType.Town);
        townNode.FactionPresence.Add("bureau");
        ow.Nodes["town"] = townNode;
        ow.Nodes["dungeon"] = new OverworldNode("dungeon", "Dungeon", NodeType.Dungeon);
        ow.Routes.Add(new OverworldRoute("town", "dungeon", 2, 3, "plains") { Status = RouteStatus.Open });

        // Note: OverworldState constructor adds a default route at index 0
        var testRouteIndex = ow.Routes.Count - 1;

        RouteStatusSystem.ApplyFactionActionTransition(ow, "bureau", new GameRandom(42));

        Assert.Equal(RouteStatus.Contested, ow.Routes[testRouteIndex].Status);
    }

    [Fact]
    public void ApplyTurnMilestoneTransitions_FactionExecuting_ContestsRoutes()
    {
        var ow = new OverworldState();
        var townNode = new OverworldNode("town", "Town", NodeType.Town);
        townNode.FactionPresence.Add("bureau");
        ow.Nodes["town"] = townNode;
        ow.Nodes["dungeon"] = new OverworldNode("dungeon", "Dungeon", NodeType.Dungeon);
        ow.Routes.Add(new OverworldRoute("town", "dungeon", 2, 3, "plains") { Status = RouteStatus.Open });

        var config = new CampaignConfig
        {
            FactionTimelines = new Dictionary<string, FactionTimeline>
            {
                { "bureau", new FactionTimeline(1, 5) }
            }
        };

        ow.Turns = 5;
        RouteStatusSystem.ApplyTurnMilestoneTransitions(ow, config, new GameRandom(42));

        var testRouteIndex = ow.Routes.Count - 1;
        Assert.Equal(RouteStatus.Contested, ow.Routes[testRouteIndex].Status);
    }

    [Fact]
    public void ApplyComplicationTransition_OpenWar_ContestsRoutes()
    {
        var ow = new OverworldState();
        ow.Nodes["a"] = new OverworldNode("a", "A", NodeType.Town);
        ow.Nodes["b"] = new OverworldNode("b", "B", NodeType.Town);
        ow.Nodes["c"] = new OverworldNode("c", "C", NodeType.Town);
        ow.Routes.Add(new OverworldRoute("a", "b", 2, 3, "plains") { Status = RouteStatus.Open });
        ow.Routes.Add(new OverworldRoute("b", "c", 2, 3, "plains") { Status = RouteStatus.Open });

        RouteStatusSystem.ApplyComplicationTransition(ow, ComplicationType.OpenWar, new GameRandom(42));

        Assert.Contains(ow.Routes, r => r.Status == RouteStatus.Contested);
    }

    [Fact]
    public void ApplyComplicationTransition_BloomSiege_AffectsRoutes()
    {
        var ow = new OverworldState();
        ow.Nodes["a"] = new OverworldNode("a", "A", NodeType.Town);
        ow.Nodes["b"] = new OverworldNode("b", "B", NodeType.Town);
        ow.Routes.Add(new OverworldRoute("a", "b", 2, 3, "plains") { Status = RouteStatus.Open });

        RouteStatusSystem.ApplyComplicationTransition(ow, ComplicationType.BloomSiege, new GameRandom(42));

        Assert.Equal(RouteStatus.BloomAffected, ow.Routes[0].Status);
    }

    [Fact]
    public void GetTravelEffectDescription_Blocked_IsImpassable()
    {
        var route = new OverworldRoute("a", "b", 2, 3, "plains") { Status = RouteStatus.Blocked };
        Assert.Contains("impassable", RouteStatusSystem.GetTravelEffectDescription(route));
    }

    [Fact]
    public void GetTravelEffectDescription_Contested_IsDangerous()
    {
        var route = new OverworldRoute("a", "b", 2, 3, "plains") { Status = RouteStatus.Contested };
        Assert.Contains("contested", RouteStatusSystem.GetTravelEffectDescription(route).ToLowerInvariant());
    }
}
