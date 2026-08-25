using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Models.Dungeons;
using RPC.Host.Web.Presenters;
using Xunit;

namespace RPC.Tests;

/// <summary>
/// The explored-tile automap is memoised across snapshots because it dominates both the payload
/// and the presentation cost while changing only rarely. These tests pin the invalidation: every
/// input that can change what the automap renders must be reflected in the very next Present()
/// from the same presenter instance. A miss here is a silently stale map on the client, which is
/// exactly the failure a cache like this invites.
/// </summary>
public class ExplorationPresenterCacheTests
{
    private static GameState StateWithDungeon(out Dungeon dungeon)
    {
        var gs = new GameState(seed: 1);
        dungeon = new Dungeon(8, 8, "t");
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                dungeon.Tiles[x, y] = new Tile(TileType.Floor, RoomId: 0);

        gs.CurrentDungeon = dungeon;
        gs.Player.Position = new Position(4, 4);
        return gs;
    }

    private static string ExploredJson(ExplorationPresenter presenter, GameState gs) =>
        presenter.Present(gs).Explored.ToString();

    /// <summary>Number of tiles in the pre-serialized explored fragment.</summary>
    private static int ExploredCount(ExplorationPresenter presenter, GameState gs)
    {
        using var doc = JsonDocument.Parse(ExploredJson(presenter, gs));
        return doc.RootElement.GetArrayLength();
    }

    /// <summary>
    /// The other tests here prove the cache never goes stale; this one proves it is still a cache.
    /// Without it, "invalidate on everything" would pass the whole file while quietly rebuilding
    /// the automap on every frame and giving back the cost the memoisation exists to save.
    /// </summary>
    [Fact]
    public void Unchanged_state_reuses_the_cached_automap()
    {
        var gs = StateWithDungeon(out _);
        var presenter = new ExplorationPresenter();
        gs.ExploredTiles.Add("3,3");

        presenter.Present(gs);
        Assert.Equal(1, presenter.ExploredRebuildCount);

        for (int i = 0; i < 25; i++) presenter.Present(gs);
        Assert.Equal(1, presenter.ExploredRebuildCount);

        // Moving the party re-presents the nearby tiles but must not disturb the automap.
        gs.Player.Position = new Position(2, 2);
        presenter.Present(gs);
        Assert.Equal(1, presenter.ExploredRebuildCount);

        gs.ExploredTiles.Add("4,4");
        presenter.Present(gs);
        Assert.Equal(2, presenter.ExploredRebuildCount);
    }

    [Fact]
    public void Newly_explored_tile_appears_in_the_next_snapshot()
    {
        var gs = StateWithDungeon(out _);
        var presenter = new ExplorationPresenter();

        gs.ExploredTiles.Add("1,1");
        Assert.Equal(1, ExploredCount(presenter, gs));

        gs.ExploredTiles.Add("2,2");
        Assert.Equal(2, ExploredCount(presenter, gs));
    }

    [Fact]
    public void Border_change_invalidates_the_cached_automap()
    {
        var gs = StateWithDungeon(out var dungeon);
        var presenter = new ExplorationPresenter();
        gs.ExploredTiles.Add("3,3");

        dungeon.Tiles[3, 3] = dungeon.Tiles[3, 3] with { North = BorderType.BreakableWall };
        Assert.Contains("\"north\":\"BreakableWall\"", ExploredJson(presenter, gs));

        // Breaking the wall opens the border; the automap must stop drawing it.
        dungeon.Tiles[3, 3] = dungeon.Tiles[3, 3] with { North = BorderType.None };
        Assert.Contains("\"north\":\"None\"", ExploredJson(presenter, gs));
    }

    [Fact]
    public void Tile_type_change_invalidates_the_cached_automap()
    {
        var gs = StateWithDungeon(out var dungeon);
        var presenter = new ExplorationPresenter();
        gs.ExploredTiles.Add("3,3");

        Assert.Contains("\"type\":\"Floor\"", ExploredJson(presenter, gs));

        dungeon.Tiles[3, 3] = dungeon.Tiles[3, 3] with { Type = TileType.StairsDown };
        Assert.Contains("\"type\":\"StairsDown\"", ExploredJson(presenter, gs));
    }

    [Fact]
    public void Collecting_loot_invalidates_the_cached_automap()
    {
        var gs = StateWithDungeon(out var dungeon);
        var presenter = new ExplorationPresenter();
        dungeon.Tiles[3, 3] = dungeon.Tiles[3, 3] with { LootId = "rat_tail" };
        gs.ExploredTiles.Add("3,3");

        Assert.Contains("\"hasLoot\":true", ExploredJson(presenter, gs));

        gs.Exploration.CollectedLoot.Add("3,3");
        Assert.Contains("\"hasLoot\":false", ExploredJson(presenter, gs));
    }

    [Fact]
    public void Switching_dungeon_invalidates_the_cached_automap()
    {
        var gs = StateWithDungeon(out _);
        var presenter = new ExplorationPresenter();
        gs.ExploredTiles.Add("3,3");
        Assert.Equal(1, ExploredCount(presenter, gs));

        var next = new Dungeon(8, 8, "t2");
        next.Tiles[1, 1] = new Tile(TileType.StairsUp);
        gs.CurrentDungeon = next;
        gs.ExploredTiles.Clear();
        gs.ExploredTiles.Add("1,1");

        var json = ExploredJson(presenter, gs);
        Assert.Contains("\"type\":\"StairsUp\"", json);
        Assert.Equal(1, ExploredCount(presenter, gs));
    }

    /// <summary>
    /// The explored set is bounded and evicts its oldest key as it adds a new one, so once it is
    /// full an add leaves Count unchanged. A cache keyed on Count alone would serve the pre-add
    /// map forever after; the set's version counter is what makes this case correct.
    /// </summary>
    [Fact]
    public void Evicting_add_at_the_cap_still_invalidates_the_cached_automap()
    {
        var gs = new GameState(seed: 1);
        var dungeon = new Dungeon(96, 96, "t");
        for (int x = 0; x < 96; x++)
            for (int y = 0; y < 96; y++)
                dungeon.Tiles[x, y] = new Tile(TileType.Floor, RoomId: 0);
        gs.CurrentDungeon = dungeon;

        var presenter = new ExplorationPresenter();

        // Fill past the 4096-entry cap so the next add must evict.
        for (int i = 0; i < 4096; i++)
            gs.ExploredTiles.Add($"{i % 96},{i / 96}");

        var before = ExploredJson(presenter, gs);
        var countBefore = gs.ExploredTiles.Count;

        gs.ExploredTiles.Add("95,95");

        Assert.Equal(countBefore, gs.ExploredTiles.Count); // eviction kept Count stable
        Assert.NotEqual(before, ExploredJson(presenter, gs));
    }

    /// <summary>
    /// A save restored against a differently sized dungeon carries explored keys outside the
    /// current bounds. Presenting must drop them rather than throw and take the snapshot with it.
    /// </summary>
    [Fact]
    public void Out_of_bounds_and_malformed_explored_keys_are_skipped()
    {
        var gs = StateWithDungeon(out _);
        var presenter = new ExplorationPresenter();

        gs.ExploredTiles.Add("3,3");
        gs.ExploredTiles.Add("99,99");
        gs.ExploredTiles.Add("-1,4");
        gs.ExploredTiles.Add("garbage");
        gs.ExploredTiles.Add("5,");

        Assert.Equal(1, ExploredCount(presenter, gs));
    }
}
