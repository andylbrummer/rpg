using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Combat;

namespace RPC.Tests;

public class CampaignConfigTests : IDisposable
{
    private readonly string _testSavePath;

    public CampaignConfigTests()
    {
        _testSavePath = Path.Combine(Path.GetTempPath(), $"test_campaigncfg_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testSavePath))
            File.Delete(_testSavePath);
        if (File.Exists(_testSavePath + ".tmp"))
            File.Delete(_testSavePath + ".tmp");
    }

    [Fact]
    public void Roll_PatronNotEqualToThreat()
    {
        var rng = new GameRandom(42);
        var config = CampaignConfig.Roll(rng);

        Assert.NotEqual(config.Patron, config.Threat);
    }

    [Fact]
    public void Roll_ThreatNotEqualToMastermind()
    {
        var rng = new GameRandom(42);
        var config = CampaignConfig.Roll(rng);

        Assert.NotEqual(config.Threat, config.Mastermind);
    }

    [Fact]
    public void Roll_WildCardIsUninvolved()
    {
        var rng = new GameRandom(42);
        var config = CampaignConfig.Roll(rng);

        var involved = new[] { config.Patron, config.Threat, config.Mastermind };
        Assert.DoesNotContain(config.WildCard, involved);
    }

    [Fact]
    public void Roll_SchemeIsFromPool()
    {
        var rng = new GameRandom(42);
        var config = CampaignConfig.Roll(rng);

        Assert.Contains(config.Scheme, CampaignConfig.SchemePool);
    }

    [Fact]
    public void Roll_ComplicationIsFromPool()
    {
        var rng = new GameRandom(42);
        var config = CampaignConfig.Roll(rng);

        Assert.Contains(config.Complication, CampaignConfig.ComplicationPool);
    }

    [Fact]
    public void Validate_MissingEvidenceChain_Fails()
    {
        var config = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            Scheme = SchemeType.BloomHarvest,
            WildCard = "stillness",
            Complication = ComplicationType.BloomSiege,
            EvidenceChain = new List<string>(),
            FactionTimelines = new Dictionary<string, FactionTimeline>
            {
                { "bureau", new FactionTimeline(1, 5) }
            },
            NpcCasting = new Dictionary<string, string>
            {
                { "patron_contact", "npc_voss" }
            },
            WildcardTrigger = new WildcardTrigger("inkblood", 10)
        };

        var valid = config.Validate(out var error);

        Assert.False(valid);
        Assert.Contains("Evidence chain", error);
    }

    [Fact]
    public void Validate_DuplicateNpcCasting_Fails()
    {
        var config = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            Scheme = SchemeType.BloomHarvest,
            WildCard = "stillness",
            Complication = ComplicationType.BloomSiege,
            EvidenceChain = Enumerable.Range(0, 10).Select(i => $"evidence_{i}").ToList(),
            FactionTimelines = new Dictionary<string, FactionTimeline>
            {
                { "bureau", new FactionTimeline(1, 5) }
            },
            NpcCasting = new Dictionary<string, string>
            {
                { "patron_contact", "npc_voss" },
                { "threat_contact", "npc_voss" }
            },
            WildcardTrigger = new WildcardTrigger("inkblood", 10)
        };

        var valid = config.Validate(out var error);

        Assert.False(valid);
        Assert.Contains("NPC cannot fill two roles", error);
    }

    [Fact]
    public void Validate_TimelinePreparingNotLessThanExecuting_Fails()
    {
        var config = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            Scheme = SchemeType.BloomHarvest,
            WildCard = "stillness",
            Complication = ComplicationType.BloomSiege,
            EvidenceChain = Enumerable.Range(0, 10).Select(i => $"evidence_{i}").ToList(),
            FactionTimelines = new Dictionary<string, FactionTimeline>
            {
                { "bureau", new FactionTimeline(5, 5) }
            },
            NpcCasting = new Dictionary<string, string>
            {
                { "patron_contact", "npc_voss" }
            },
            WildcardTrigger = new WildcardTrigger("inkblood", 10)
        };

        var valid = config.Validate(out var error);

        Assert.False(valid);
        Assert.Contains("preparing must be less than executing", error);
    }

    [Fact]
    public void Validate_WildcardTriggerIsThreat_Fails()
    {
        var config = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            Scheme = SchemeType.BloomHarvest,
            WildCard = "stillness",
            Complication = ComplicationType.BloomSiege,
            EvidenceChain = Enumerable.Range(0, 10).Select(i => $"evidence_{i}").ToList(),
            FactionTimelines = new Dictionary<string, FactionTimeline>
            {
                { "bureau", new FactionTimeline(1, 5) }
            },
            NpcCasting = new Dictionary<string, string>
            {
                { "patron_contact", "npc_voss" }
            },
            WildcardTrigger = new WildcardTrigger("convocation", 10)
        };

        var valid = config.Validate(out var error);

        Assert.False(valid);
        Assert.Contains("Wildcard trigger faction cannot be threat or mastermind", error);
    }

    [Fact]
    public void Validate_WildcardTriggerIsMastermind_Fails()
    {
        var config = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            Scheme = SchemeType.BloomHarvest,
            WildCard = "stillness",
            Complication = ComplicationType.BloomSiege,
            EvidenceChain = Enumerable.Range(0, 10).Select(i => $"evidence_{i}").ToList(),
            FactionTimelines = new Dictionary<string, FactionTimeline>
            {
                { "bureau", new FactionTimeline(1, 5) }
            },
            NpcCasting = new Dictionary<string, string>
            {
                { "patron_contact", "npc_voss" }
            },
            WildcardTrigger = new WildcardTrigger("cartography", 10)
        };

        var valid = config.Validate(out var error);

        Assert.False(valid);
        Assert.Contains("Wildcard trigger faction cannot be threat or mastermind", error);
    }

    [Fact]
    public void Validate_ValidConfig_Passes()
    {
        var config = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            Scheme = SchemeType.BloomHarvest,
            WildCard = "stillness",
            Complication = ComplicationType.BloomSiege,
            EvidenceChain = Enumerable.Range(0, 10).Select(i => $"evidence_{i}").ToList(),
            FactionTimelines = new Dictionary<string, FactionTimeline>
            {
                { "bureau", new FactionTimeline(1, 5) },
                { "convocation", new FactionTimeline(2, 6) }
            },
            NpcCasting = new Dictionary<string, string>
            {
                { "patron_contact", "npc_voss" },
                { "threat_contact", "npc_lyra" }
            },
            WildcardTrigger = new WildcardTrigger("inkblood", 10)
        };

        var valid = config.Validate(out var error);

        Assert.True(valid);
        Assert.Equal("", error);
    }

    [Fact]
    public void Roll_ThousandTimes_ZeroInvalid()
    {
        int invalidCount = 0;
        for (int seed = 1; seed <= 1000; seed++)
        {
            var rng = new GameRandom(seed);
            var config = CampaignConfig.Roll(rng);

            if (config.Patron == config.Threat ||
                config.Threat == config.Mastermind ||
                new[] { config.Patron, config.Threat, config.Mastermind }.Contains(config.WildCard) ||
                !CampaignConfig.SchemePool.Contains(config.Scheme) ||
                !CampaignConfig.ComplicationPool.Contains(config.Complication))
            {
                invalidCount++;
            }
        }

        Assert.Equal(0, invalidCount);
    }

    [Fact]
    public void SaveLoad_RoundTrip_PreservesCampaignConfig()
    {
        var gs = new GameState(seed: 42);
        gs.CampaignConfig = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            Scheme = SchemeType.EngineSeizure,
            WildCard = "stillness",
            Complication = ComplicationType.ErraticEngine,
            EvidenceChain = new List<string> { "e1", "e2", "e3", "e4", "e5", "e6", "e7", "e8", "e9", "e10" },
            FactionTimelines = new Dictionary<string, FactionTimeline>
            {
                { "bureau", new FactionTimeline(1, 5) }
            },
            NpcCasting = new Dictionary<string, string>
            {
                { "patron_contact", "npc_voss" }
            },
            WildcardTrigger = new WildcardTrigger("inkblood", 12)
        };

        gs.SaveGame(_testSavePath);

        var gs2 = new GameState(seed: 99);
        var loaded = gs2.LoadGame(_testSavePath);

        Assert.True(loaded);
        Assert.NotNull(gs2.CampaignConfig);
        Assert.Equal("bureau", gs2.CampaignConfig.Patron);
        Assert.Equal("convocation", gs2.CampaignConfig.Threat);
        Assert.Equal("cartography", gs2.CampaignConfig.Mastermind);
        Assert.Equal(SchemeType.EngineSeizure, gs2.CampaignConfig.Scheme);
        Assert.Equal("stillness", gs2.CampaignConfig.WildCard);
        Assert.Equal(ComplicationType.ErraticEngine, gs2.CampaignConfig.Complication);
        Assert.Equal(10, gs2.CampaignConfig.EvidenceChain.Count);
        Assert.Equal("npc_voss", gs2.CampaignConfig.NpcCasting["patron_contact"]);
        Assert.Equal(1, gs2.CampaignConfig.FactionTimelines["bureau"].Preparing);
        Assert.Equal(5, gs2.CampaignConfig.FactionTimelines["bureau"].Executing);
        Assert.NotNull(gs2.CampaignConfig.WildcardTrigger);
        Assert.Equal("inkblood", gs2.CampaignConfig.WildcardTrigger.FactionId);
        Assert.Equal(12, gs2.CampaignConfig.WildcardTrigger.TurnThreshold);
    }

    [Fact]
    public void SaveLoad_NullCampaignConfig_RoundTripsAsNull()
    {
        var gs = new GameState(seed: 42);
        gs.CampaignConfig = null;

        gs.SaveGame(_testSavePath);

        var gs2 = new GameState(seed: 99);
        var loaded = gs2.LoadGame(_testSavePath);

        Assert.True(loaded);
        Assert.Null(gs2.CampaignConfig);
    }

    [Fact]
    public void Deserialize_HandAuthoredJson_PassesValidation()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        var json = File.ReadAllText(Path.Combine(projectRoot, "content", "campaigns", "test-campaign.json"));
        var config = System.Text.Json.JsonSerializer.Deserialize<CampaignConfig>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(config);
        Assert.True(config.Validate(out var error), error);
        Assert.Equal("bureau", config.Patron);
        Assert.Equal("convocation", config.Threat);
        Assert.Equal("bureau", config.Mastermind);
        Assert.Equal(SchemeType.ManufacturedCrisis, config.Scheme);
        Assert.Equal("stillness", config.WildCard);
        Assert.Equal(ComplicationType.MissingTeam, config.Complication);
        Assert.Equal(11, config.EvidenceChain.Count);
        Assert.Equal(5, config.FactionTimelines.Count);
        Assert.Equal(8, config.NpcCasting.Count);
        Assert.NotNull(config.WildcardTrigger);
        Assert.Equal("inkblood", config.WildcardTrigger.FactionId);
        Assert.Equal(12, config.WildcardTrigger.TurnThreshold);
    }
}
