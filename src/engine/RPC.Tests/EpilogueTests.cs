using RPC.Engine;
using RPC.Engine.Campaign;

namespace RPC.Tests;

public class EpilogueTests
{
    [Fact]
    public void Generate_ContainsMastermindAndPatron()
    {
        var state = new GameState();
        state.Campaign.CampaignConfig = new CampaignConfig
        {
            Mastermind = "inkblood",
            Patron = "bureau",
            Scheme = SchemeType.TheVault,
            FactionTimelines = new Dictionary<string, FactionTimeline>(),
            NpcCasting = new Dictionary<string, string>()
        };

        var text = EpilogueGenerator.Generate(state);
        Assert.Contains("inkblood", text);
        Assert.Contains("bureau", text);
    }

    [Fact]
    public void Generate_ReflectsDeaths()
    {
        var state = new GameState();
        state.Campaign.CampaignConfig = new CampaignConfig
        {
            Mastermind = "inkblood",
            Patron = "bureau",
            Scheme = SchemeType.TheVault,
            FactionTimelines = new Dictionary<string, FactionTimeline>(),
            NpcCasting = new Dictionary<string, string>()
        };
        state.ActionLog.Add(new ActionLogEntry(1, "combat", "character_died", new Dictionary<string, string>
        {
            { "characterName", "Alice" }
        }));

        var text = EpilogueGenerator.Generate(state);
        Assert.Contains("1 companion fell", text);
    }

    [Fact]
    public void Generate_NoDeaths_SurvivedText()
    {
        var state = new GameState();
        state.Campaign.CampaignConfig = new CampaignConfig
        {
            Mastermind = "inkblood",
            Patron = "bureau",
            Scheme = SchemeType.TheVault,
            FactionTimelines = new Dictionary<string, FactionTimeline>(),
            NpcCasting = new Dictionary<string, string>()
        };

        var text = EpilogueGenerator.Generate(state);
        Assert.Contains("survived", text);
    }

    [Fact]
    public void Generate_BetrayalPath_Mentioned()
    {
        var state = new GameState();
        state.Campaign.CampaignConfig = new CampaignConfig
        {
            Mastermind = "inkblood",
            Patron = "bureau",
            Scheme = SchemeType.TheVault,
            FactionTimelines = new Dictionary<string, FactionTimeline>(),
            NpcCasting = new Dictionary<string, string>()
        };
        state.Campaign.BetrayalPath = true;

        var text = EpilogueGenerator.Generate(state);
        Assert.Contains("betrayal", text);
        Assert.Contains("succeeded", text);
        Assert.Contains("knife in the back", text);
    }
}
