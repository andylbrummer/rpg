using RPC.Engine.Town;
using Xunit;

public class TavernRecruitNameTests
{
    [Fact]
    public void NamePool_HasAtLeast40_DistinctNames()
    {
        var names = TavernRecruitGenerator.NamePool;
        Assert.True(names.Count >= 40, $"expected >=40 names, got {names.Count}");
        Assert.Equal(names.Count, names.Distinct().Count());
    }
}
