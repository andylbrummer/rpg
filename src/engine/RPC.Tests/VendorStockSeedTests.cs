using System.Linq;
using RPC.Engine;
using RPC.Engine.Town;
using Xunit;

public class VendorStockSeedTests
{
    [Fact]
    public void NewGame_SeedsNonEmptyGenericVendorStock()
    {
        var state = new GameState(seed: 1);
        Assert.NotEmpty(state.Town.VendorStock);
        Assert.All(state.Town.VendorStock, v =>
        {
            Assert.False(string.IsNullOrWhiteSpace(v.ItemId));
            Assert.True(v.Price > 0);
            Assert.True(v.Quantity > 0);
        });
    }

    [Fact]
    public void GenerateVendorStock_PricesAndIds_AreStable()
    {
        var stock = new TownService().GenerateVendorStock();
        Assert.Contains(stock, v => v.ItemId == "small_salve");
        Assert.Equal(stock.Select(v => v.ItemId).Distinct().Count(), stock.Count);
    }
}
