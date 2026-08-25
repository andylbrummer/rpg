using System.Linq;
using RPC.Engine;
using RPC.Engine.Town;

namespace RPC.Tests;

public class FactionRecruitGatingTests
{
    private static ReputationState Rep(params (string faction, int value)[] entries)
    {
        var rep = new ReputationState();
        foreach (var (faction, value) in entries)
            rep[faction] = value;
        return rep;
    }

    [Fact]
    public void NoReputation_NoExclusiveRecruits()
    {
        var exclusives = TavernRecruitGenerator.GetExclusiveRecruits(new ReputationState());
        Assert.Empty(exclusives);
    }

    [Fact]
    public void Bureau20_UnlocksLiarOnly()
    {
        var exclusives = TavernRecruitGenerator.GetExclusiveRecruits(Rep(("bureau", 20)));

        Assert.Single(exclusives);
        Assert.Equal("liar", exclusives[0].ClassId);
    }

    [Fact]
    public void Bureau19_DoesNotUnlockLiar()
    {
        var exclusives = TavernRecruitGenerator.GetExclusiveRecruits(Rep(("bureau", 19)));
        Assert.Empty(exclusives);
    }

    [Fact]
    public void Convocation25_UnlocksHeretic()
    {
        var exclusives = TavernRecruitGenerator.GetExclusiveRecruits(Rep(("convocation", 25)));

        Assert.Single(exclusives);
        Assert.Equal("heretic", exclusives[0].ClassId);
    }

    [Fact]
    public void Convocation24_DoesNotUnlockHeretic()
    {
        Assert.Empty(TavernRecruitGenerator.GetExclusiveRecruits(Rep(("convocation", 24))));
    }

    [Fact]
    public void InkbloodCompact25_UnlocksBeastkeeper()
    {
        var exclusives = TavernRecruitGenerator.GetExclusiveRecruits(Rep(("inkblood", 25)));

        Assert.Single(exclusives);
        Assert.Equal("beastkeeper", exclusives[0].ClassId);
    }

    [Fact]
    public void AllThresholdsMet_UnlocksAllThree()
    {
        var exclusives = TavernRecruitGenerator.GetExclusiveRecruits(
            Rep(("bureau", 20), ("convocation", 25), ("inkblood", 30)));

        var classes = exclusives.Select(e => e.ClassId).ToHashSet();
        Assert.Equal(3, classes.Count);
        Assert.Contains("liar", classes);
        Assert.Contains("heretic", classes);
        Assert.Contains("beastkeeper", classes);
    }

    [Fact]
    public void GenerateRoster_WithRep_AppendsExclusivesAfterBaseSix()
    {
        var roster = TavernRecruitGenerator.GenerateRoster(42, Rep(("bureau", 20)));

        Assert.Equal(7, roster.Count); // 6 base + 1 exclusive
        Assert.Contains(roster, r => r.ClassId == "liar");
    }

    [Fact]
    public void ReturnToTown_UnlocksExclusive_WhenRepGained()
    {
        var gs = new GameState(seed: 42);
        Assert.DoesNotContain(gs.Town.TavernRoster, r => r.ClassId == "liar");

        gs.Reputation["bureau"] = 20;
        gs.ReturnToTown();

        Assert.Contains(gs.Town.TavernRoster, r => r.ClassId == "liar");
    }

    [Fact]
    public void ReturnToTown_RemovesExclusive_WhenRepDropsBelowThreshold()
    {
        var gs = new GameState(seed: 42);
        gs.Reputation["bureau"] = 20;
        gs.ReturnToTown();
        Assert.Contains(gs.Town.TavernRoster, r => r.ClassId == "liar");

        gs.Reputation["bureau"] = 5;
        gs.ReturnToTown();

        Assert.DoesNotContain(gs.Town.TavernRoster, r => r.ClassId == "liar");
    }
}
