using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Save;

namespace RPC.Tests;

/// <summary>
/// Reading a Family Archive or a lore document grants its intel exactly once, tracked by the
/// campaign's read-document set. That set was never written to the save, so every archive and
/// document became readable again after a reload — the Ancestral Ledger's +8 reputation and
/// +2 evidence could be collected as many times as the player was willing to load the game.
/// The tracking only ever held for one session.
/// </summary>
public class ReadDocumentPersistenceTests : IDisposable
{
    private readonly string _savePath = Path.Combine(
        Path.GetTempPath(), $"reach-readdocs-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_savePath)) File.Delete(_savePath);
    }

    private GameState StateWithLedger()
    {
        var archives = new ArchiveRegistry();
        archives.Register(new FamilyArchiveDef(
            "ledger", "inkblood", RepReward: 8, EvidenceReward: 2, JournalEntryId: "lore_ledger"));

        var gs = new GameState(seed: 5, archives: archives) { SavePath = _savePath };
        return gs;
    }

    [Fact]
    public void AnArchiveReadBeforeSaving_StaysReadAfterLoading()
    {
        var before = StateWithLedger();
        Assert.NotNull(before.ReadArchive("ledger"));
        Assert.Null(before.ReadArchive("ledger")); // idempotent within the session
        var repAfterFirstRead = before.Reputation["inkblood"];
        before.SaveGame();

        var after = StateWithLedger();
        Assert.True(after.LoadGame());

        Assert.Null(after.ReadArchive("ledger"));
        Assert.Equal(repAfterFirstRead, after.Reputation["inkblood"]);
    }

    /// <summary>
    /// Campaign progress restored from a save written before a campaign was generated. These
    /// fields sat behind an early return meant for the optional campaign-config block, so a
    /// configless save dropped every one of them — the same trap the family name had already been
    /// hoisted out of.
    /// </summary>
    [Fact]
    public void CampaignProgress_SurvivesASaveWithNoCampaignConfig()
    {
        var before = StateWithLedger();
        Assert.Null(before.CampaignConfig);
        before.Campaign.FiredEvents.Add("event_a");
        before.Campaign.UnlockedDungeons.Add("crypt");
        before.Campaign.ReadDocuments.Add("some_doc");
        before.Campaign.AnnouncedFactionStates.Add("inkblood:Preparing");
        before.Campaign.BetrayalPath = true;
        before.SaveGame();

        var after = StateWithLedger();
        Assert.True(after.LoadGame());

        Assert.Contains("event_a", after.Campaign.FiredEvents);
        Assert.Contains("crypt", after.Campaign.UnlockedDungeons);
        Assert.Contains("some_doc", after.Campaign.ReadDocuments);
        Assert.Contains("inkblood:Preparing", after.Campaign.AnnouncedFactionStates);
        Assert.True(after.Campaign.BetrayalPath);
    }

    [Fact]
    public void AnUnreadArchive_IsStillReadableAfterLoading()
    {
        var before = StateWithLedger();
        before.SaveGame();

        var after = StateWithLedger();
        Assert.True(after.LoadGame());

        Assert.NotNull(after.ReadArchive("ledger"));
    }
}
