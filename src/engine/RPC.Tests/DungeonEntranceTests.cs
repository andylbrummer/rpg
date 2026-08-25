using RPC.Engine;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

/// <summary>
/// The entrance is the tile the stitcher marks <see cref="TileType.StairsUp"/>, and it is the one
/// place a party may be dropped into a dungeon: on first entry and when a rescue party goes in
/// after a TPK. Three separate scans used to answer "where is the entrance?" independently, and
/// two of them looked for <see cref="TileType.Floor"/> — which the stairs tile is not — so the
/// party spawned on whichever walkable tile happened to sort first, typically the far west edge
/// of the map, in a different room than the one the path classifier calls the entrance.
/// </summary>
public class DungeonEntranceTests
{
    /// <summary>A dungeon whose stairs sit deliberately far from the lowest-sorting floor tile.</summary>
    private static Dungeon TwoRoomDungeonWithStairsAtEast()
    {
        var dungeon = new Dungeon(20, 20, "Test");
        // West room — sorts first in a column-major scan, but is not the entrance.
        for (int y = 4; y <= 6; y++)
            dungeon.Tiles[2, y] = new Tile(TileType.Floor, RoomId: 1);
        // East room — holds the entrance stairs.
        for (int y = 10; y <= 12; y++)
            dungeon.Tiles[15, y] = new Tile(TileType.Floor, RoomId: 2);
        dungeon.Tiles[15, 11] = new Tile(TileType.StairsUp, RoomId: 2);
        return dungeon;
    }

    [Fact]
    public void FindEntrance_PrefersTheStairsOverTheFirstWalkableTile()
    {
        var dungeon = TwoRoomDungeonWithStairsAtEast();

        Assert.Equal(new Position(15, 11), dungeon.FindEntrance());
    }

    [Fact]
    public void FindEntrance_FallsBackToTheFirstWalkableTileWhenThereAreNoStairs()
    {
        var dungeon = new Dungeon(20, 20, "Test");
        dungeon.Tiles[7, 3] = new Tile(TileType.Floor);
        dungeon.Tiles[9, 1] = new Tile(TileType.Floor);

        Assert.Equal(new Position(7, 3), dungeon.FindEntrance());
    }

    [Fact]
    public void FindEntrance_IsNullWhenNothingIsWalkable()
    {
        Assert.Null(new Dungeon(8, 8, "Empty").FindEntrance());
    }

    [Fact]
    public void EnterDungeon_PlacesThePartyOnTheEntranceStairs()
    {
        var state = new GameState();
        var dungeon = TwoRoomDungeonWithStairsAtEast();

        state.EnterDungeon(dungeon, "test");

        Assert.Equal(new Position(15, 11), state.Player.Position);
        Assert.Equal(Direction.North, state.Player.Facing);
    }

    [Fact]
    public void EnterDungeon_ExploresAroundTheEntranceItStoodOn()
    {
        var state = new GameState();
        var dungeon = TwoRoomDungeonWithStairsAtEast();

        state.EnterDungeon(dungeon, "test");

        Assert.True(state.ExploredTiles.Contains("15,11"));
        Assert.False(state.ExploredTiles.Contains("2,5"));
    }
}
