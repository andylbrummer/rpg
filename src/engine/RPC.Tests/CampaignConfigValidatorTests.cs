using System.Linq;
using RPC.Engine.Campaign;

namespace RPC.Tests;

public class CampaignConfigValidatorTests
{
    private static CampaignConfig Valid() => new()
    {
        Patron = "bureau",
        Threat = "convocation",
        Mastermind = "inkblood",
        WildCard = "stillness",
        EvidenceChain = new List<string> { "clue_a", "clue_b", "clue_c" },
        FactionTimelines = new Dictionary<string, FactionTimeline> { ["bureau"] = new FactionTimeline(2, 5) },
    };

    [Fact]
    public void ValidConfig_HasNoIssues()
    {
        Assert.True(CampaignConfigValidator.IsValid(Valid()));
    }

    [Fact]
    public void Completeness_MissingFieldsReported()
    {
        var c = Valid();
        c.Patron = "";
        c.FactionTimelines = new Dictionary<string, FactionTimeline>();

        var issues = CampaignConfigValidator.Validate(c);
        Assert.Contains(issues, i => i.Category == "completeness" && i.Detail.Contains("patron"));
        Assert.Contains(issues, i => i.Category == "completeness" && i.Detail.Contains("timelines"));
    }

    [Fact]
    public void FactionConsistency_WildCardInvolved_Reported()
    {
        var c = Valid();
        c.WildCard = c.Patron; // involved

        var issues = CampaignConfigValidator.Validate(c);
        Assert.Contains(issues, i => i.Category == "faction_consistency" && i.Detail.Contains("uninvolved"));
    }

    [Fact]
    public void FactionConsistency_UnknownFaction_Reported()
    {
        var c = Valid();
        c.Mastermind = "not_a_faction";

        var issues = CampaignConfigValidator.Validate(c);
        Assert.Contains(issues, i => i.Category == "faction_consistency" && i.Detail.Contains("mastermind"));
    }

    [Fact]
    public void Coherence_PatronEqualsThreat_Reported()
    {
        var c = Valid();
        c.Threat = c.Patron;

        var issues = CampaignConfigValidator.Validate(c);
        Assert.Contains(issues, i => i.Category == "coherence" && i.Detail.Contains("threat"));
    }

    [Fact]
    public void Completability_ShortEvidenceChain_Reported()
    {
        var c = Valid();
        c.EvidenceChain = new List<string> { "only_one" };

        var issues = CampaignConfigValidator.Validate(c);
        Assert.Contains(issues, i => i.Category == "completability" && i.Detail.Contains("Evidence chain"));
    }

    [Fact]
    public void Completability_TimelineOutOfOrder_Reported()
    {
        var c = Valid();
        c.FactionTimelines = new Dictionary<string, FactionTimeline> { ["bureau"] = new FactionTimeline(7, 3) };

        var issues = CampaignConfigValidator.Validate(c);
        Assert.Contains(issues, i => i.Category == "completability" && i.Detail.Contains("precede"));
    }

    [Fact]
    public void Summarize_JoinsCategorizedIssues()
    {
        var c = Valid();
        c.Patron = "";
        var summary = CampaignConfigValidator.Summarize(CampaignConfigValidator.Validate(c));
        Assert.Contains("[completeness]", summary);
    }
}
