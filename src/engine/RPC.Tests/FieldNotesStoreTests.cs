using RPC.Engine;

namespace RPC.Tests;

public class FieldNotesStoreTests
{
    [Fact]
    public void JournalState_Discover_AddsToOrder()
    {
        var journal = new JournalState();
        journal.Discover("synergy_a");
        journal.Discover("synergy_b");
        journal.Discover("synergy_a");

        Assert.Equal(2, journal.DiscoveryOrder.Count);
        Assert.Equal("synergy_a", journal.DiscoveryOrder[0]);
        Assert.Equal("synergy_b", journal.DiscoveryOrder[1]);
    }

    [Fact]
    public void JournalState_IsDiscovered_ReturnsCorrectFlag()
    {
        var journal = new JournalState();
        Assert.False(journal.IsDiscovered("unknown"));

        journal.Discover("known");
        Assert.True(journal.IsDiscovered("known"));
    }

    [Fact]
    public void JournalState_SortOrder_PreservesDiscoverySequence()
    {
        var journal = new JournalState();
        journal.Discover("third");
        journal.Discover("first");
        journal.Discover("second");

        Assert.Equal(new[] { "third", "first", "second" }, journal.DiscoveryOrder);
    }

    [Fact]
    public void JournalState_EntryCount_TracksDiscoveredOnly()
    {
        var journal = new JournalState();
        Assert.Empty(journal.Discovered);

        journal.Discover("a");
        journal.Discover("b");
        Assert.Equal(2, journal.Discovered.Count);
    }
}
