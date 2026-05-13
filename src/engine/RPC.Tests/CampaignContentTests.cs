using System.Text.Json;
using RPC.Engine.Campaign;

namespace RPC.Tests;

public class CampaignContentTests
{
    private static string GetContentDir(string subDir)
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        return Path.Combine(projectRoot, "content", subDir);
    }

    [Fact]
    public void LoadSchemes_LoadsAllThree()
    {
        var schemes = CampaignContentLoader.LoadSchemes(GetContentDir("schemes"));
        Assert.Equal(3, schemes.Count);
        Assert.Contains(schemes, s => s.Id == "BloomHarvest");
        Assert.Contains(schemes, s => s.Id == "EngineSeizure");
        Assert.Contains(schemes, s => s.Id == "CascadeFailure");
    }

    [Fact]
    public void LoadComplications_LoadsAllThree()
    {
        var complications = CampaignContentLoader.LoadComplications(GetContentDir("complications"));
        Assert.Equal(3, complications.Count);
        Assert.Contains(complications, c => c.Id == "BloomSiege");
        Assert.Contains(complications, c => c.Id == "OpenWar");
        Assert.Contains(complications, c => c.Id == "TitheCollapse");
    }

    [Theory]
    [InlineData("BloomHarvest")]
    [InlineData("EngineSeizure")]
    [InlineData("CascadeFailure")]
    public void Scheme_HasFiveToSevenEvents(string schemeId)
    {
        var scheme = CampaignContentLoader.GetSchemeById(schemeId, GetContentDir("schemes"));
        Assert.NotNull(scheme);
        Assert.InRange(scheme.Events.Length, 5, 7);
    }

    [Theory]
    [InlineData("BloomHarvest")]
    [InlineData("EngineSeizure")]
    [InlineData("CascadeFailure")]
    public void Scheme_HasEvidenceChain(string schemeId)
    {
        var scheme = CampaignContentLoader.GetSchemeById(schemeId, GetContentDir("schemes"));
        Assert.NotNull(scheme);
        Assert.True(scheme.EvidenceChain.Length >= 5, $"Scheme {schemeId} should have at least 5 evidence items");
    }

    [Theory]
    [InlineData("BloomHarvest")]
    [InlineData("EngineSeizure")]
    [InlineData("CascadeFailure")]
    public void Scheme_HasFinaleDungeonFeel(string schemeId)
    {
        var scheme = CampaignContentLoader.GetSchemeById(schemeId, GetContentDir("schemes"));
        Assert.NotNull(scheme);
        Assert.False(string.IsNullOrWhiteSpace(scheme.FinaleDungeonFeel));
        Assert.True(scheme.FinaleDungeonFeel.Length > 50, "Finale dungeon feel should be descriptive");
    }

    [Theory]
    [InlineData("BloomSiege")]
    [InlineData("OpenWar")]
    [InlineData("TitheCollapse")]
    public void Complication_HasWorldStateModifiers(string complicationId)
    {
        var complication = CampaignContentLoader.GetComplicationById(complicationId, GetContentDir("complications"));
        Assert.NotNull(complication);
        Assert.NotNull(complication.WorldStateModifiers);
    }

    [Theory]
    [InlineData("BloomSiege")]
    [InlineData("OpenWar")]
    [InlineData("TitheCollapse")]
    public void Complication_HasEvents(string complicationId)
    {
        var complication = CampaignContentLoader.GetComplicationById(complicationId, GetContentDir("complications"));
        Assert.NotNull(complication);
        Assert.True(complication.Events.Length >= 2, $"Complication {complicationId} should have at least 2 events");
    }

    [Fact]
    public void BloomHarvest_FirstEvent_IsEarlyTurn()
    {
        var scheme = CampaignContentLoader.GetSchemeById("BloomHarvest", GetContentDir("schemes"));
        Assert.NotNull(scheme);
        var firstEvent = scheme.Events.OrderBy(e => e.TurnThreshold).First();
        Assert.True(firstEvent.TurnThreshold <= 3, "First scheme event should happen early");
    }

    [Fact]
    public void BloomHarvest_LastEvent_IsConfrontation()
    {
        var scheme = CampaignContentLoader.GetSchemeById("BloomHarvest", GetContentDir("schemes"));
        Assert.NotNull(scheme);
        var lastEvent = scheme.Events.OrderBy(e => e.TurnThreshold).Last();
        Assert.Equal("confrontation", lastEvent.Id);
        Assert.Equal("final_dungeon_unlocked", lastEvent.Effect);
    }

    [Fact]
    public void BloomSiege_HasRouteModifiers()
    {
        var complication = CampaignContentLoader.GetComplicationById("BloomSiege", GetContentDir("complications"));
        Assert.NotNull(complication);
        Assert.NotNull(complication.WorldStateModifiers.RouteStatusChance);
        Assert.Contains("bloomAffected", complication.WorldStateModifiers.RouteStatusChance.Keys);
    }

    [Fact]
    public void TitheCollapse_HasResurrectionModifier()
    {
        var complication = CampaignContentLoader.GetComplicationById("TitheCollapse", GetContentDir("complications"));
        Assert.NotNull(complication);
        Assert.NotNull(complication.WorldStateModifiers.ResurrectionCostMultiplier);
        Assert.True(complication.WorldStateModifiers.ResurrectionCostMultiplier > 1.0);
    }

    [Fact]
    public void AllSchemes_HaveDistinctIds()
    {
        var schemes = CampaignContentLoader.LoadSchemes(GetContentDir("schemes"));
        var ids = schemes.Select(s => s.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void AllComplications_HaveDistinctIds()
    {
        var complications = CampaignContentLoader.LoadComplications(GetContentDir("complications"));
        var ids = complications.Select(c => c.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
