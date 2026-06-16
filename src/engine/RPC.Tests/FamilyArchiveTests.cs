using RPC.Engine;
using RPC.Engine.Campaign;

namespace RPC.Tests;

/// <summary>
/// Family Archives — a Compact (inkblood) signature mechanic. Interactable archive objects that,
/// when read, grant faction intel: Compact reputation, evidence toward the faction's case, and a
/// journal entry. Reuses the document-read tracking set for idempotency and the existing
/// reputation/evidence/journal systems for effects.
/// </summary>
public class FamilyArchiveTests
{
    private static GameState MakeStateWithArchive(FamilyArchiveDef archive)
    {
        var gs = new GameState(seed: 1);
        gs.Archives.Register(archive);
        return gs;
    }

    [Fact]
    public void Reading_Archive_Grants_Reputation_Evidence_And_Journal_Entry()
    {
        var archive = new FamilyArchiveDef(
            "compact_ancestral_ledger", "inkblood",
            RepReward: 8, EvidenceReward: 2, JournalEntryId: "lore_compact_ledger",
            Name: "Ancestral Ledger");
        var gs = MakeStateWithArchive(archive);
        var startingRep = gs.Reputation["inkblood"];

        var result = gs.ReadArchive("compact_ancestral_ledger");

        Assert.NotNull(result);
        Assert.Equal(startingRep + 8, gs.Reputation["inkblood"]);
        Assert.Equal(2, gs.Evidence.Counters.GetValueOrDefault("inkblood"));
        Assert.True(gs.Journal.IsDiscovered("lore_compact_ledger"));
        Assert.Contains(gs.ActionLog, e => e.Type == "archive_read"
            && e.Payload.GetValueOrDefault("archiveId") == "compact_ancestral_ledger"
            && e.Payload.GetValueOrDefault("factionId") == "inkblood");
    }

    [Fact]
    public void Reading_Archive_Is_Idempotent()
    {
        var archive = new FamilyArchiveDef("a1", "inkblood", RepReward: 5);
        var gs = MakeStateWithArchive(archive);

        var first = gs.ReadArchive("a1");
        var repAfterFirst = gs.Reputation["inkblood"];
        var second = gs.ReadArchive("a1");

        Assert.NotNull(first);
        Assert.Null(second); // already read — no second grant
        Assert.Equal(repAfterFirst, gs.Reputation["inkblood"]);
    }

    [Fact]
    public void Reading_Unknown_Archive_Returns_Null()
    {
        var gs = new GameState(seed: 1);
        Assert.Null(gs.ReadArchive("does_not_exist"));
    }

    [Fact]
    public void Archive_Without_Evidence_Or_Journal_Only_Grants_Reputation()
    {
        var archive = new FamilyArchiveDef("rep_only", "inkblood", RepReward: 3);
        var gs = MakeStateWithArchive(archive);
        var startingRep = gs.Reputation["inkblood"];

        var result = gs.ReadArchive("rep_only");

        Assert.NotNull(result);
        Assert.Equal(startingRep + 3, gs.Reputation["inkblood"]);
        Assert.Equal(0, gs.Evidence.Counters.GetValueOrDefault("inkblood"));
    }
}
