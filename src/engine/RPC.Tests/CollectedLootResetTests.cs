using RPC.Engine;
using RPC.Engine.Models.Dungeons;
using Xunit;

namespace RPC.Tests;

public class CollectedLootResetTests
{
    [Fact]
    public void EnterDungeon_clears_collected_loot_from_prior_expedition()
    {
        var gs = new GameState(seed: 1);

        var first = new Dungeon(64, 64, "t");
        first.Tiles[32, 32] = new Tile(TileType.Floor, RoomId: 0);
        gs.EnterDungeon(first, "t");

        // Simulate a prior expedition having collected loot at a shared coordinate.
        gs.Exploration.CollectedLoot.Add("5,7");

        var second = new Dungeon(64, 64, "t");
        second.Tiles[32, 32] = new Tile(TileType.Floor, RoomId: 0);
        gs.EnterDungeon(second, "t");

        Assert.DoesNotContain("5,7", gs.Exploration.CollectedLoot);
        Assert.Empty(gs.Exploration.CollectedLoot);
    }
}
