using RPC.Engine;
using RPC.Engine.Town;

namespace RPC.Tests;

public class VendorFilterTests
{
    [Fact]
    public void GetAvailableVendors_At24Rep_BureauVendorNotAvailable()
    {
        var town = new TownState();
        town.FactionVendors = FactionVendorGenerator.GenerateStock();
        var rep = new ReputationState();
        rep["bureau"] = 24;

        var available = VendorFilter.GetAvailableVendors(town, rep);

        Assert.DoesNotContain(available, v => v.FactionId == "bureau");
    }

    [Fact]
    public void GetAvailableVendors_At25Rep_BureauVendorAvailable()
    {
        var town = new TownState();
        town.FactionVendors = FactionVendorGenerator.GenerateStock();
        var rep = new ReputationState();
        rep["bureau"] = 25;

        var available = VendorFilter.GetAvailableVendors(town, rep);

        Assert.Contains(available, v => v.FactionId == "bureau");
    }

    [Fact]
    public void GetAvailableVendors_AtMinus25Rep_BureauVendorNotAvailable()
    {
        var town = new TownState();
        town.FactionVendors = FactionVendorGenerator.GenerateStock();
        var rep = new ReputationState();
        rep["bureau"] = -25;

        var available = VendorFilter.GetAvailableVendors(town, rep);

        Assert.DoesNotContain(available, v => v.FactionId == "bureau");
    }

    [Fact]
    public void GetAvailableVendors_AtMinus24Rep_BureauVendorNotAvailable()
    {
        var town = new TownState();
        town.FactionVendors = FactionVendorGenerator.GenerateStock();
        var rep = new ReputationState();
        rep["bureau"] = -24;

        var available = VendorFilter.GetAvailableVendors(town, rep);

        Assert.DoesNotContain(available, v => v.FactionId == "bureau");
    }

    [Fact]
    public void IsVendorVisible_AtMinus25Rep_BureauVendorHidden()
    {
        var rep = new ReputationState();
        rep["bureau"] = -25;

        Assert.False(VendorFilter.IsVendorVisible("bureau", rep));
    }

    [Fact]
    public void IsVendorVisible_AtMinus24Rep_BureauVendorVisible()
    {
        var rep = new ReputationState();
        rep["bureau"] = -24;

        Assert.True(VendorFilter.IsVendorVisible("bureau", rep));
    }

    [Fact]
    public void PurchaseVendorItem_FactionItemAtThreshold_DeductsGoldAndAddsToInventory()
    {
        var gs = new GameState(seed: 1);
        gs.PartyGold = 100;
        gs.Town.FactionVendors = FactionVendorGenerator.GenerateStock();
        gs.Reputation["bureau"] = 25;

        var itemId = gs.Town.FactionVendors[0].Stock[0].ItemId;
        var price = gs.Town.FactionVendors[0].Stock[0].Price;
        var result = gs.PurchaseVendorItem(itemId);

        Assert.True(result);
        Assert.Equal(100 - price, gs.PartyGold);
        Assert.Contains(itemId, gs.PartyInventory);
    }

    [Fact]
    public void PurchaseVendorItem_FactionItemBelowThreshold_ReturnsFalse()
    {
        var gs = new GameState(seed: 1);
        gs.PartyGold = 100;
        gs.Town.FactionVendors = FactionVendorGenerator.GenerateStock();
        gs.Reputation["bureau"] = 24;

        var itemId = gs.Town.FactionVendors[0].Stock[0].ItemId;
        var result = gs.PurchaseVendorItem(itemId);

        Assert.False(result);
        Assert.Equal(100, gs.PartyGold);
        Assert.Empty(gs.PartyInventory);
    }

    [Fact]
    public void PurchaseVendorItem_FactionItemAtHostileLockout_ReturnsFalse()
    {
        var gs = new GameState(seed: 1);
        gs.PartyGold = 100;
        gs.Town.FactionVendors = FactionVendorGenerator.GenerateStock();
        gs.Reputation["bureau"] = -25;

        var itemId = gs.Town.FactionVendors[0].Stock[0].ItemId;
        var result = gs.PurchaseVendorItem(itemId);

        Assert.False(result);
        Assert.Equal(100, gs.PartyGold);
        Assert.Empty(gs.PartyInventory);
    }

    [Fact]
    public void PurchaseVendorItem_InsufficientGold_ReturnsFalse()
    {
        var gs = new GameState(seed: 1);
        gs.PartyGold = 1;
        gs.Town.FactionVendors = FactionVendorGenerator.GenerateStock();
        gs.Reputation["bureau"] = 25;

        var itemId = gs.Town.FactionVendors[0].Stock[0].ItemId;
        var result = gs.PurchaseVendorItem(itemId);

        Assert.False(result);
        Assert.Equal(1, gs.PartyGold);
    }

    [Fact]
    public void PurchaseVendorItem_GenericItem_DeductsGoldAndAddsToInventory()
    {
        var gs = new GameState(seed: 1);
        gs.PartyGold = 50;
        gs.Town.VendorStock.Add(new VendorItem("small_salve", "Small Salve", 15, 3));

        var result = gs.PurchaseVendorItem("small_salve");

        Assert.True(result);
        Assert.Equal(35, gs.PartyGold);
        Assert.Contains("small_salve", gs.PartyInventory);
        Assert.Empty(gs.Town.VendorStock);
    }

    [Fact]
    public void PurchaseVendorItem_GenericItemInsufficientGold_ReturnsFalse()
    {
        var gs = new GameState(seed: 1);
        gs.PartyGold = 5;
        gs.Town.VendorStock.Add(new VendorItem("small_salve", "Small Salve", 15, 3));

        var result = gs.PurchaseVendorItem("small_salve");

        Assert.False(result);
        Assert.Equal(5, gs.PartyGold);
    }
}
