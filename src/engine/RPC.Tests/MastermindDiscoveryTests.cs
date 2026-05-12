using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Character;

namespace RPC.Tests;

public class MastermindDiscoveryTests : IDisposable
{
    private readonly string _testSavePath;

    public MastermindDiscoveryTests()
    {
        _testSavePath = Path.Combine(Path.GetTempPath(), $"test_mastermind_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testSavePath))
            File.Delete(_testSavePath);
    }

    [Fact]
    public void CanClassReveal_InkbloodArchivist_AtThreshold3_ReturnsTrue()
    {
        var evidence = new EvidenceState();
        evidence.AddEvidence("bureau", "test", 3);

        Assert.True(evidence.CanClassReveal(3, "inkblood", "archivist"));
        Assert.False(evidence.CanClassReveal(3, "inkblood", null));
        Assert.False(evidence.CanClassReveal(2, "inkblood", "archivist"));
    }

    [Fact]
    public void CanClassReveal_AshmouthBroker_AtThreshold5_ReturnsTrue()
    {
        var evidence = new EvidenceState();
        evidence.AddEvidence("cartography", "test", 5);

        Assert.True(evidence.CanClassReveal(5, "ashmouth", "broker"));
        Assert.False(evidence.CanClassReveal(5, "ashmouth", null));
        Assert.False(evidence.CanClassReveal(4, "ashmouth", "broker"));
    }

    [Fact]
    public void CanClassReveal_OtherClasses_ReturnsFalse()
    {
        var evidence = new EvidenceState();
        evidence.AddEvidence("bureau", "test", 10);

        Assert.False(evidence.CanClassReveal(10, "bonewarden", "tank"));
        Assert.False(evidence.CanClassReveal(10, "stillblade", "duelist"));
    }

    [Fact]
    public void GetRevelationForParty_InkbloodArchivist_ReturnsArchivistInsight()
    {
        var evidence = new EvidenceState();
        evidence.AddEvidence("bureau", "test", 3);

        var party = new[]
        {
            new CharacterState(Guid.NewGuid(), "Mira", "inkblood", 3, 0,
                new BaseStats(3, 5, 4, 5, 4), 14, Equipment.Empty,
                Array.Empty<string>(), 0, "archivist")
        };

        Assert.Equal("archivist_insight", evidence.GetRevelationForParty(party));
    }

    [Fact]
    public void GetRevelationForParty_AshmouthBroker_ReturnsBrokerInsight()
    {
        var evidence = new EvidenceState();
        evidence.AddEvidence("bureau", "test", 5);

        var party = new[]
        {
            new CharacterState(Guid.NewGuid(), "Rook", "ashmouth", 5, 0,
                new BaseStats(5, 4, 5, 3, 4), 20, Equipment.Empty,
                Array.Empty<string>(), 0, "broker")
        };

        Assert.Equal("broker_insight", evidence.GetRevelationForParty(party));
    }

    [Fact]
    public void GetRevelationForParty_NoRevelation_ReturnsNull()
    {
        var evidence = new EvidenceState();
        evidence.AddEvidence("bureau", "test", 2);

        var party = new[]
        {
            new CharacterState(Guid.NewGuid(), "Mira", "inkblood", 3, 0,
                new BaseStats(3, 5, 4, 5, 4), 14, Equipment.Empty,
                Array.Empty<string>(), 0, "archivist")
        };

        Assert.Null(evidence.GetRevelationForParty(party));
    }

    [Fact]
    public void GetRevelationForParty_EmptySlot_Skipped()
    {
        var evidence = new EvidenceState();
        evidence.AddEvidence("bureau", "test", 3);

        var party = new[]
        {
            default(CharacterState),
            new CharacterState(Guid.NewGuid(), "Mira", "inkblood", 3, 0,
                new BaseStats(3, 5, 4, 5, 4), 14, Equipment.Empty,
                Array.Empty<string>(), 0, "archivist")
        };

        Assert.Equal("archivist_insight", evidence.GetRevelationForParty(party));
    }

    [Fact]
    public void AccuseFaction_WrongAccusation_AppliesRepPenaltyAndSetsAdvantage()
    {
        var gs = new GameState(seed: 42);
        gs.CampaignConfig = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            EvidenceChain = Enumerable.Repeat("x", 10).ToList()
        };
        gs.AddEvidence("bureau", "test", 7);

        var result = gs.AccuseFaction("bureau");

        Assert.True(result);
        Assert.Equal("bureau", gs.AccusedFaction);
        Assert.True(gs.MastermindAdvantage);
        Assert.Equal(-20, gs.Reputation["bureau"]);

        var log = gs.ActionLog.Last(e => e.Type == "accusation_wrong");
        Assert.NotNull(log);
        Assert.Equal("-20", log.Payload["penalty"]);
    }

    [Fact]
    public void AccuseFaction_CorrectAccusation_SetsAccusedAndLogsCorrect()
    {
        var gs = new GameState(seed: 42);
        gs.CampaignConfig = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            EvidenceChain = Enumerable.Repeat("x", 10).ToList()
        };
        gs.AddEvidence("cartography", "test", 7);

        var result = gs.AccuseFaction("cartography");

        Assert.True(result);
        Assert.Equal("cartography", gs.AccusedFaction);
        Assert.False(gs.MastermindAdvantage);

        var log = gs.ActionLog.Last(e => e.Type == "accusation_correct");
        Assert.NotNull(log);
    }

    [Fact]
    public void AccuseFaction_InsufficientEvidence_ReturnsFalse()
    {
        var gs = new GameState(seed: 42);
        gs.CampaignConfig = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            EvidenceChain = Enumerable.Repeat("x", 10).ToList()
        };
        gs.AddEvidence("cartography", "test", 5);

        var result = gs.AccuseFaction("cartography");

        Assert.False(result);
        Assert.Null(gs.AccusedFaction);
    }

    [Fact]
    public void AccuseFaction_AlreadyAccused_ReturnsFalse()
    {
        var gs = new GameState(seed: 42);
        gs.CampaignConfig = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            EvidenceChain = Enumerable.Repeat("x", 10).ToList()
        };
        gs.AddEvidence("cartography", "test", 7);
        gs.AccuseFaction("cartography");

        var result = gs.AccuseFaction("bureau");

        Assert.False(result);
    }

    [Fact]
    public void AccuseFaction_NoCampaignConfig_ReturnsFalse()
    {
        var gs = new GameState(seed: 42);
        gs.AddEvidence("bureau", "test", 7);

        var result = gs.AccuseFaction("bureau");

        Assert.False(result);
    }

    [Fact]
    public void UnlockFinalDungeon_CorrectAccusationAndTenEvidence_ReturnsTrue()
    {
        var gs = new GameState(seed: 42);
        gs.CampaignConfig = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            EvidenceChain = Enumerable.Repeat("x", 10).ToList()
        };
        gs.AddEvidence("cartography", "test", 10);
        gs.AccuseFaction("cartography");

        var result = gs.UnlockFinalDungeon();

        Assert.True(result);
        Assert.True(gs.FinalDungeonUnlocked);

        var log = gs.ActionLog.Last(e => e.Type == "final_dungeon_unlocked");
        Assert.NotNull(log);
        Assert.Equal("cartography", log.Payload["mastermind"]);
    }

    [Fact]
    public void UnlockFinalDungeon_WrongAccusation_ReturnsFalse()
    {
        var gs = new GameState(seed: 42);
        gs.CampaignConfig = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            EvidenceChain = Enumerable.Repeat("x", 10).ToList()
        };
        gs.AddEvidence("bureau", "test", 10);
        gs.AccuseFaction("bureau");

        var result = gs.UnlockFinalDungeon();

        Assert.False(result);
        Assert.False(gs.FinalDungeonUnlocked);
    }

    [Fact]
    public void UnlockFinalDungeon_InsufficientEvidence_ReturnsFalse()
    {
        var gs = new GameState(seed: 42);
        gs.CampaignConfig = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            EvidenceChain = Enumerable.Repeat("x", 10).ToList()
        };
        gs.AddEvidence("cartography", "test", 9);
        gs.AccuseFaction("cartography");

        var result = gs.UnlockFinalDungeon();

        Assert.False(result);
        Assert.False(gs.FinalDungeonUnlocked);
    }

    [Fact]
    public void UnlockFinalDungeon_AlreadyUnlocked_ReturnsFalse()
    {
        var gs = new GameState(seed: 42);
        gs.CampaignConfig = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            EvidenceChain = Enumerable.Repeat("x", 10).ToList()
        };
        gs.AddEvidence("cartography", "test", 10);
        gs.AccuseFaction("cartography");
        gs.UnlockFinalDungeon();

        var result = gs.UnlockFinalDungeon();

        Assert.False(result);
    }

    [Fact]
    public void SaveLoad_PreservesAccusedFactionAndAdvantage()
    {
        var gs = new GameState(seed: 1);
        gs.CampaignConfig = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            EvidenceChain = Enumerable.Repeat("x", 10).ToList()
        };
        gs.AddEvidence("bureau", "test", 7);
        gs.AccuseFaction("bureau");

        gs.SaveGame(_testSavePath);

        var gs2 = new GameState(seed: 2);
        var loaded = gs2.LoadGame(_testSavePath);
        Assert.True(loaded);

        Assert.Equal("bureau", gs2.AccusedFaction);
        Assert.True(gs2.MastermindAdvantage);
        Assert.False(gs2.FinalDungeonUnlocked);
    }

    [Fact]
    public void SaveLoad_PreservesFinalDungeonUnlocked()
    {
        var gs = new GameState(seed: 1);
        gs.CampaignConfig = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            EvidenceChain = Enumerable.Repeat("x", 10).ToList()
        };
        gs.AddEvidence("cartography", "test", 10);
        gs.AccuseFaction("cartography");
        gs.UnlockFinalDungeon();

        gs.SaveGame(_testSavePath);

        var gs2 = new GameState(seed: 2);
        var loaded = gs2.LoadGame(_testSavePath);
        Assert.True(loaded);

        Assert.Equal("cartography", gs2.AccusedFaction);
        Assert.False(gs2.MastermindAdvantage);
        Assert.True(gs2.FinalDungeonUnlocked);
    }

    [Fact]
    public void Reset_ClearsMastermindState()
    {
        var gs = new GameState(seed: 1);
        gs.CampaignConfig = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "cartography",
            EvidenceChain = Enumerable.Repeat("x", 10).ToList()
        };
        gs.AddEvidence("bureau", "test", 7);
        gs.AccuseFaction("bureau");
        gs.Reset();

        Assert.Null(gs.AccusedFaction);
        Assert.False(gs.MastermindAdvantage);
        Assert.False(gs.FinalDungeonUnlocked);
    }

    [Fact]
    public void AddEvidence_WithChannel_TracksChannelCounts()
    {
        var evidence = new EvidenceState();
        evidence.AddEvidence("bureau", EvidenceChannel.DocumentaryContradictions, "doc1");
        evidence.AddEvidence("bureau", EvidenceChannel.NpcBehavioralShifts, "npc1");
        evidence.AddEvidence("bureau", EvidenceChannel.DocumentaryContradictions, "doc2");

        Assert.Equal(3, evidence.Counters["bureau"]);
        Assert.Equal(2, evidence.GetChannelCount("bureau", EvidenceChannel.DocumentaryContradictions));
        Assert.Equal(1, evidence.GetChannelCount("bureau", EvidenceChannel.NpcBehavioralShifts));
        Assert.Equal(0, evidence.GetChannelCount("bureau", EvidenceChannel.DirectConfrontation));
    }
}
