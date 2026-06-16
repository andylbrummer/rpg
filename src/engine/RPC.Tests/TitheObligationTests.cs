using RPC.Engine;
using RPC.Engine.Commands;
using RPC.Engine.Town;

namespace RPC.Tests;

/// <summary>
/// Tithe obligation system (T84b). Covers due-on-milestone detection, the ceil(activePartySize/3)
/// cost formula, debt accumulation, each non-payment penalty, the late-payment gold surcharge, and
/// payment clearing debt + lifting penalties. Design: docs/design/05-characters-and-classes.md.
/// </summary>
public class TitheObligationTests
{
    private static GameState NewState(int seed = 1)
    {
        var gs = new GameState(seed: seed);
        // Default party seeds 6 living members; tithe defaults to no debt.
        return gs;
    }

    private static void SetActivePartySize(GameState gs, int size)
    {
        for (int i = 0; i < gs.Party.Members.Length; i++)
            gs.Party.SetMember(i, i < size ? gs.Party.Members[i] : default);
        Assert.Equal(size, gs.Party.Active.Count());
    }

    // --- Cost formula -------------------------------------------------------------------------

    [Theory]
    [InlineData(6, 2)]
    [InlineData(5, 2)]
    [InlineData(4, 2)]
    [InlineData(3, 1)]
    [InlineData(1, 1)]
    public void TitheCost_IsCeilOfPartySizeOverThree(int partySize, int expectedTokens)
    {
        Assert.Equal(expectedTokens, TownService.TitheCostForPartySize(partySize));
    }

    // --- Due detection ------------------------------------------------------------------------

    [Fact]
    public void TownEntry_BeforeFirstMilestone_RaisesNoTithe()
    {
        var gs = NewState();
        gs.Overworld.Turns = 0;

        gs.CheckTitheOnTownEntry();

        Assert.False(gs.Tithe.HasDebt);
        Assert.Empty(gs.Tithe.BilledMilestones);
    }

    [Fact]
    public void TownEntry_AtMilestone_RaisesTitheWithCeilCost()
    {
        var gs = NewState();
        SetActivePartySize(gs, 6);
        gs.Overworld.Turns = 1;

        gs.CheckTitheOnTownEntry();

        Assert.True(gs.Tithe.HasDebt);
        Assert.Equal(2, gs.Tithe.Debt); // ceil(6/3)
        Assert.Equal(1, gs.Tithe.OutstandingSinceTurn);
        Assert.Contains(1, gs.Tithe.BilledMilestones);
    }

    [Fact]
    public void TownEntry_ReEntrySameMilestone_DoesNotDoubleBill()
    {
        var gs = NewState();
        SetActivePartySize(gs, 6);
        gs.Overworld.Turns = 1;

        gs.CheckTitheOnTownEntry();
        gs.CheckTitheOnTownEntry();

        Assert.Equal(2, gs.Tithe.Debt); // billed once, not twice
    }

    // --- Debt accumulation --------------------------------------------------------------------

    [Fact]
    public void UnpaidTithe_AccumulatesAcrossMilestones()
    {
        var gs = NewState();
        SetActivePartySize(gs, 6);

        gs.Overworld.Turns = 1;
        gs.CheckTitheOnTownEntry(); // +2

        gs.Overworld.Turns = 15;
        gs.CheckTitheOnTownEntry(); // +2

        gs.Overworld.Turns = 25;
        gs.CheckTitheOnTownEntry(); // +2

        Assert.Equal(6, gs.Tithe.Debt);
        Assert.Equal(new[] { 1, 15, 25 }, gs.Tithe.BilledMilestones.ToArray());
        Assert.Equal(1, gs.Tithe.OutstandingSinceTurn); // earliest unpaid milestone
    }

    [Fact]
    public void TownEntry_PastUnbilledMilestone_BillsIt()
    {
        var gs = NewState();
        SetActivePartySize(gs, 6);
        gs.Overworld.Turns = 20; // first town entry happens after turns 1 and 15

        gs.CheckTitheOnTownEntry();

        Assert.Equal(new[] { 1, 15 }, gs.Tithe.BilledMilestones.ToArray());
        Assert.Equal(4, gs.Tithe.Debt);
    }

    // --- Penalty: reputation ------------------------------------------------------------------

    [Fact]
    public void UnpaidTithe_DropsCompactReputation_TenPerToken()
    {
        var gs = NewState();
        SetActivePartySize(gs, 6);
        gs.SetReputation(TownService.TitheFactionId, 50);
        gs.Overworld.Turns = 1;

        gs.CheckTitheOnTownEntry();

        // -10 per unpaid token (2 tokens) => -20.
        Assert.Equal(30, gs.Reputation[TownService.TitheFactionId]);
    }

    // --- Penalty: component / vendor cost +50% ------------------------------------------------

    [Fact]
    public void InDebt_ExposesFiftyPercentComponentCostMultiplier()
    {
        var gs = NewState();
        gs.Tithe.Debt = 1;
        Assert.Equal(1.5, gs.Tithe.ComponentCostMultiplier);

        gs.Tithe.Debt = 0;
        Assert.Equal(1.0, gs.Tithe.ComponentCostMultiplier);
    }

    [Fact]
    public void InDebt_VendorPurchaseCostsFiftyPercentMore()
    {
        var gs = NewState();
        gs.Town.VendorStock.Add(new VendorItem("bone_shard", "Bone Shard", 100, 1));
        gs.PartyGold = 1000;

        // Baseline price with no debt.
        gs.Tithe.Debt = 0;
        var goldBefore = gs.PartyGold;
        Assert.True(gs.PurchaseVendorItem("bone_shard"));
        var baselineCost = goldBefore - gs.PartyGold;
        Assert.Equal(100, baselineCost);

        // Re-stock and purchase while in debt: +50%.
        gs.Town.VendorStock.Add(new VendorItem("bone_shard", "Bone Shard", 100, 1));
        gs.Tithe.Debt = 1;
        goldBefore = gs.PartyGold;
        Assert.True(gs.PurchaseVendorItem("bone_shard"));
        var debtCost = goldBefore - gs.PartyGold;
        Assert.Equal(150, debtCost);
    }

    // --- Penalty: contacts refuse -------------------------------------------------------------

    [Fact]
    public void InDebt_ContactsRefuse()
    {
        var gs = NewState();
        Assert.False(gs.Tithe.ContactsRefuse);
        gs.Tithe.Debt = 1;
        Assert.True(gs.Tithe.ContactsRefuse);
    }

    // --- Payment ------------------------------------------------------------------------------

    [Fact]
    public void PayTithe_OnTimeSameTurn_ClearsDebtWithoutSurcharge()
    {
        var gs = NewState();
        SetActivePartySize(gs, 6);
        gs.Overworld.Turns = 1;
        gs.CheckTitheOnTownEntry(); // debt 2, since turn 1
        gs.TitheTokens = 5;
        gs.PartyGold = 1000;

        // Pay on the same turn the milestone fell due — not late.
        Assert.True(gs.PayTithe());

        Assert.Equal(0, gs.Tithe.Debt);
        Assert.False(gs.Tithe.HasDebt);
        Assert.Null(gs.Tithe.OutstandingSinceTurn);
        Assert.Equal(3, gs.TitheTokens);   // 5 - 2
        Assert.Equal(1000, gs.PartyGold);  // no surcharge
    }

    [Fact]
    public void PayTithe_AfterMilestoneTurn_AppliesGoldSurcharge()
    {
        var gs = NewState();
        SetActivePartySize(gs, 6);
        gs.Overworld.Turns = 1;
        gs.CheckTitheOnTownEntry(); // debt 2, since turn 1
        gs.TitheTokens = 5;
        gs.PartyGold = 1000;

        gs.Overworld.Turns = 10; // pay later — late
        Assert.True(gs.PayTithe());

        var expectedSurcharge = (int)System.Math.Ceiling(
            2 * TownService.TitheGoldValuePerToken * TownService.TitheLateGoldSurchargeRate);
        Assert.Equal(0, gs.Tithe.Debt);
        Assert.Equal(3, gs.TitheTokens);
        Assert.Equal(1000 - expectedSurcharge, gs.PartyGold);
    }

    [Fact]
    public void PayTithe_LiftsOngoingPenalties()
    {
        var gs = NewState();
        SetActivePartySize(gs, 6);
        gs.Overworld.Turns = 1;
        gs.CheckTitheOnTownEntry();
        gs.TitheTokens = 5;

        Assert.True(gs.Tithe.ContactsRefuse);
        Assert.Equal(1.5, gs.Tithe.ComponentCostMultiplier);

        Assert.True(gs.PayTithe());

        Assert.False(gs.Tithe.ContactsRefuse);
        Assert.Equal(1.0, gs.Tithe.ComponentCostMultiplier);
    }

    [Fact]
    public void PayTithe_NoDebt_ReturnsFalse()
    {
        var gs = NewState();
        Assert.False(gs.PayTithe());
    }

    [Fact]
    public void PayTithe_InsufficientTokens_ReturnsFalseAndKeepsDebt()
    {
        var gs = NewState();
        SetActivePartySize(gs, 6);
        gs.Overworld.Turns = 1;
        gs.CheckTitheOnTownEntry(); // debt 2
        gs.TitheTokens = 1;         // not enough

        Assert.False(gs.PayTithe());
        Assert.Equal(2, gs.Tithe.Debt);
        Assert.Equal(1, gs.TitheTokens);
    }

    [Fact]
    public void PayTithe_LateButInsufficientGoldForSurcharge_ReturnsFalse()
    {
        var gs = NewState();
        SetActivePartySize(gs, 6);
        gs.Overworld.Turns = 1;
        gs.CheckTitheOnTownEntry();
        gs.TitheTokens = 5;
        gs.PartyGold = 0;        // cannot cover surcharge
        gs.Overworld.Turns = 10; // late

        Assert.False(gs.PayTithe());
        Assert.Equal(2, gs.Tithe.Debt);
    }

    // --- Command pipeline ---------------------------------------------------------------------

    [Fact]
    public void PayTitheCommand_ClearsDebtThroughHandler()
    {
        var gs = NewState();
        SetActivePartySize(gs, 6);
        gs.Overworld.Turns = 1;
        gs.CheckTitheOnTownEntry();
        gs.TitheTokens = 5;
        gs.PartyGold = 1000;

        var handler = new GameCommandHandler(gs, new StubDungeonGenerator());
        var result = handler.Execute(new PayTitheCommand());

        Assert.True(result.StateChanged);
        Assert.False(gs.Tithe.HasDebt);
    }

    // --- Town entry hook ----------------------------------------------------------------------

    [Fact]
    public void ReturnToTown_TriggersTitheCheck()
    {
        var gs = NewState();
        SetActivePartySize(gs, 6);
        gs.Mode = GameMode.Exploration;
        gs.Overworld.Turns = 0; // ReturnToTown increments to 1 (first milestone)

        gs.ReturnToTown();

        Assert.Equal(1, gs.Overworld.Turns);
        Assert.True(gs.Tithe.HasDebt);
    }
}
