using RPC.Engine;
using RPC.Engine.Models.Dungeons;
using Xunit;

namespace RPC.Tests;

public class PickupLootTests
{
    private static GameState StateOnLootTile(string itemId)
    {
        var gs = new GameState(seed: 1);
        var d = new Dungeon(3, 3, "t");
        d.Tiles[1, 1] = new Tile(TileType.Floor, RoomId: 0, LootId: itemId);
        gs.CurrentDungeon = d;
        gs.CurrentDungeonType = "t";
        gs.Player.Position = new Position(1, 1);
        return gs;
    }

    [Fact]
    public void Pickup_adds_item_to_cache_and_marks_collected()
    {
        var gs = StateOnLootTile("rat_tail");
        bool changed = gs.TryPickupLoot();

        Assert.True(changed);
        Assert.Contains(gs.Party.ExpeditionCache, s => s.ItemId == "rat_tail" && s.Count >= 1);
        Assert.Contains("1,1", gs.Exploration.CollectedLoot);
    }

    [Fact]
    public void Second_pickup_on_same_tile_is_noop()
    {
        var gs = StateOnLootTile("rat_tail");
        Assert.True(gs.TryPickupLoot());
        Assert.False(gs.TryPickupLoot());
    }

    [Fact]
    public void Pickup_on_empty_tile_is_noop()
    {
        var gs = new GameState(seed: 1);
        var d = new Dungeon(3, 3, "t");
        d.Tiles[1, 1] = new Tile(TileType.Floor, RoomId: 0);
        gs.CurrentDungeon = d;
        gs.Player.Position = new Position(1, 1);
        Assert.False(gs.TryPickupLoot());
    }
}
