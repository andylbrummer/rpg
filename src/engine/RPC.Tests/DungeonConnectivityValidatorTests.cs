using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

public class DungeonConnectivityValidatorTests
{
    private static Dungeon Row(int n)
    {
        var d = new Dungeon(n, 1, "test");
        for (int x = 0; x < n; x++)
            d.Tiles[x, 0] = new Tile(TileType.Floor);
        return d;
    }

    [Fact]
    public void FullyConnectedRow_AllReachable()
    {
        var report = DungeonConnectivityValidator.Validate(Row(3));

        Assert.True(report.FullyConnected);
        Assert.Equal(3, report.WalkableCount);
        Assert.Equal(3, report.ReachableCount);
        Assert.Empty(report.Unreachable);
        Assert.Equal(2, report.MaxDepth);
    }

    [Fact]
    public void WalledOffTile_IsReportedUnreachable()
    {
        var d = Row(3);
        // Wall between [1,0] and [2,0] in both directions isolates [2,0].
        d.Tiles[1, 0] = new Tile(TileType.Floor, East: BorderType.Wall);
        d.Tiles[2, 0] = new Tile(TileType.Floor, West: BorderType.Wall);

        var report = DungeonConnectivityValidator.Validate(d);

        Assert.False(report.FullyConnected);
        Assert.Equal(2, report.ReachableCount);
        Assert.Contains(new Position(2, 0), report.Unreachable);
    }

    [Fact]
    public void StairsUp_IsUsedAsEntrance()
    {
        var d = Row(3);
        d.Tiles[0, 0] = new Tile(TileType.StairsUp);

        var report = DungeonConnectivityValidator.Validate(d);
        Assert.True(report.FullyConnected);
        Assert.Equal(3, report.ReachableCount);
    }

    [Fact]
    public void EmptyDungeon_IsTriviallyConnected()
    {
        var report = DungeonConnectivityValidator.Validate(new Dungeon(3, 3, "test"));
        Assert.True(report.FullyConnected);
        Assert.Equal(0, report.WalkableCount);
    }

    [Fact]
    public void IsFullyConnected_MatchesValidate()
    {
        Assert.True(DungeonConnectivityValidator.IsFullyConnected(Row(4)));
    }
}
