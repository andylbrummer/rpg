using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Party;
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

    /// <summary>
    /// A full cache leaves the loot on the floor. The command handler reads the return only as
    /// "did state change", so refusing silently made an explicit pickup look like a dropped input.
    /// </summary>
    [Fact]
    public void Pickup_refused_by_a_full_cache_says_why()
    {
        var gs = StateOnLootTile("rat_tail");
        gs.Party.ExpeditionCache = Enumerable.Range(0, PartyState.MaxExpeditionCacheSlots)
            .Select(i => new ComponentStack($"filler_{i}", 99, 99))
            .ToArray();

        Assert.False(gs.TryPickupLoot());
        Assert.Contains(gs.ActionLog, e => e.Type == "loot_refused_cache_full");
        Assert.DoesNotContain("1,1", gs.Exploration.CollectedLoot);
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
