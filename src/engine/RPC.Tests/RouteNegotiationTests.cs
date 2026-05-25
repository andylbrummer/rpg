using System.Linq;
using RPC.Engine;
using RPC.Engine.Overworld;

namespace RPC.Tests;

public class RouteNegotiationTests
{
    private const string From = "the_reach";
    private const string To = "broken_engine";

    private static (GameState gs, OverworldRoute route) Setup(RouteStatus status, params string[] controllers)
    {
        var gs = new GameState(seed: 42);
        var route = gs.Overworld.GetRoute(From, To)!;
        route.Status = status;
        foreach (var f in controllers)
            gs.Overworld.Nodes[From].FactionPresence.Add(f);
        return (gs, route);
    }

    [Fact]
    public void Contested_WithFriendlyStanding_OpensRouteAndSpendsReputation()
    {
        var (gs, route) = Setup(RouteStatus.Contested, "bureau");
        gs.Reputation["bureau"] = 30;

        var result = gs.NegotiatePassage(From, To);

        Assert.True(result.Success);
        Assert.Equal("bureau", result.FactionId);
        Assert.Equal(RouteStatus.Open, route.Status);
        Assert.Equal(25, gs.Reputation["bureau"]); // 30 - 5 cost
        Assert.Contains(gs.ActionLog, e => e.Type == "route_negotiated" && e.Payload["factionId"] == "bureau");
    }

    [Fact]
    public void Contested_WithInsufficientStanding_Fails()
    {
        var (gs, route) = Setup(RouteStatus.Contested, "bureau");
        gs.Reputation["bureau"] = 10;

        var result = gs.NegotiatePassage(From, To);

        Assert.False(result.Success);
        Assert.Equal(RouteStatus.Contested, route.Status);
        Assert.Equal(10, gs.Reputation["bureau"]); // unchanged
        Assert.Contains(gs.ActionLog, e => e.Type == "route_negotiation_failed");
    }

    [Fact]
    public void Blocked_WithAlliedStanding_DowngradesToContested()
    {
        var (gs, route) = Setup(RouteStatus.Blocked, "bureau");
        gs.Reputation["bureau"] = 50;

        var result = gs.NegotiatePassage(From, To);

        Assert.True(result.Success);
        Assert.Equal(RouteStatus.Contested, route.Status); // blocked only reopens one tier
    }

    [Fact]
    public void Blocked_WithOnlyFriendlyStanding_Fails()
    {
        var (gs, route) = Setup(RouteStatus.Blocked, "bureau");
        gs.Reputation["bureau"] = 30; // >=25 but <50

        var result = gs.NegotiatePassage(From, To);

        Assert.False(result.Success);
        Assert.Equal(RouteStatus.Blocked, route.Status);
    }

    [Fact]
    public void OpenRoute_CannotBeNegotiated()
    {
        var (gs, _) = Setup(RouteStatus.Open, "bureau");
        gs.Reputation["bureau"] = 80;

        Assert.False(gs.NegotiatePassage(From, To).Success);
    }

    [Fact]
    public void BloomAffectedRoute_CannotBeNegotiated()
    {
        var (gs, route) = Setup(RouteStatus.BloomAffected, "bureau");
        gs.Reputation["bureau"] = 80;

        var result = gs.NegotiatePassage(From, To);

        Assert.False(result.Success);
        Assert.Equal(RouteStatus.BloomAffected, route.Status);
    }

    [Fact]
    public void NoControllingFaction_Fails()
    {
        var (gs, _) = Setup(RouteStatus.Contested); // no presence added
        var result = gs.NegotiatePassage(From, To);
        Assert.False(result.Success);
        Assert.Null(result.FactionId);
    }

    [Fact]
    public void PicksHighestReputationController()
    {
        var (gs, route) = Setup(RouteStatus.Contested, "stillness", "cartography");
        gs.Reputation["stillness"] = 26;
        gs.Reputation["cartography"] = 40;

        var result = gs.NegotiatePassage(From, To);

        Assert.True(result.Success);
        Assert.Equal("cartography", result.FactionId);
        Assert.Equal(35, gs.Reputation["cartography"]); // 40 - 5
        Assert.Equal(RouteStatus.Open, route.Status);
    }
}
