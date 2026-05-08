using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

public class DungeonTests
{
    [Fact]
    public void Position_Move_North()
    {
        var pos = new Position(5, 5);
        var newPos = pos.Move(Direction.North);
        Assert.Equal(new Position(5, 4), newPos);
    }

    [Fact]
    public void Position_Move_East()
    {
        var pos = new Position(5, 5);
        var newPos = pos.Move(Direction.East);
        Assert.Equal(new Position(6, 5), newPos);
    }

    [Fact]
    public void Direction_TurnLeft()
    {
        Assert.Equal(Direction.West, Direction.North.TurnLeft());
        Assert.Equal(Direction.North, Direction.East.TurnLeft());
        Assert.Equal(Direction.East, Direction.South.TurnLeft());
        Assert.Equal(Direction.South, Direction.West.TurnLeft());
    }

    [Fact]
    public void Direction_TurnRight()
    {
        Assert.Equal(Direction.East, Direction.North.TurnRight());
        Assert.Equal(Direction.South, Direction.East.TurnRight());
        Assert.Equal(Direction.West, Direction.South.TurnRight());
        Assert.Equal(Direction.North, Direction.West.TurnRight());
    }

    [Fact]
    public void Dungeon_CanMoveTo_WalkableTiles()
    {
        var dungeon = new Dungeon(10, 10, "Test");
        dungeon.Tiles[5, 5] = new Tile(TileType.Floor);
        dungeon.Tiles[5, 6] = new Tile(TileType.Floor, North: BorderType.Wall);

        // Can move from (5,5) to (5,6) going south - no wall on south border of (5,5)
        Assert.True(dungeon.CanMoveTo(new Position(5, 5), Direction.South));

        // Cannot move from (5,6) to (5,5) going north - wall on north border of (5,6)
        Assert.False(dungeon.CanMoveTo(new Position(5, 6), Direction.North));

        // Out of bounds
        Assert.False(dungeon.CanMoveTo(new Position(100, 100), Direction.North));
    }

    [Fact]
    public void Dungeon_CanMoveTo_DoorIsPassable()
    {
        var dungeon = new Dungeon(10, 10, "Test");
        dungeon.Tiles[5, 5] = new Tile(TileType.Floor, South: BorderType.Door);
        dungeon.Tiles[5, 6] = new Tile(TileType.Floor, North: BorderType.Door);

        Assert.True(dungeon.CanMoveTo(new Position(5, 5), Direction.South));
        Assert.True(dungeon.CanMoveTo(new Position(5, 6), Direction.North));
    }

    [Fact]
    public void DungeonBuilder_CreatesDungeon()
    {
        var builder = new DungeonBuilder(seed: 42);
        builder.AddSegment(CreateTestRoom());

        var dungeon = builder.Build("Test Dungeon", 5);

        Assert.NotNull(dungeon);
        Assert.Equal("Test Dungeon", dungeon.Name);
        Assert.Equal(64, dungeon.Width);
        Assert.Equal(64, dungeon.Height);
    }

    [Fact]
    public void DungeonBuilder_DerivesBorders()
    {
        var builder = new DungeonBuilder(seed: 42);
        builder.AddSegment(CreateTestRoom());

        var dungeon = builder.Build("Test Dungeon", 1);

        // The test room has floor tiles at (32,32), (33,32), (32,33), (33,33)
        // Tile (32,32) has north=Door (exit). After DeriveBorders, other edges
        // should have walls where adjacent to empty.
        var tile = dungeon.Tiles[32, 32];
        Assert.Equal(TileType.Floor, tile.Type);
        Assert.Equal(BorderType.Door, tile.North);

        // Tile to the east should be walkable and have west=None (adjacent to floor)
        var east = dungeon.Tiles[33, 32];
        Assert.Equal(TileType.Floor, east.Type);
        Assert.Equal(BorderType.None, east.West);

        // North of the exit tile should be a wall (edge of dungeon / empty)
        // Since (32,31) is Empty, the north border of (32,32) is Door (explicit)
        // The south border of (32,32) should be None (adjacent to floor at 32,33)
        // or Wall (if at dungeon edge) - in this case it's adjacent to floor
        Assert.Equal(BorderType.None, tile.South);
    }

    private static RoomSegment CreateTestRoom()
    {
        return new RoomSegment
        {
            Id = "test",
            Name = "Test Room",
            Tags = new() { "entrance" },
            Tiles = new()
            {
                new() { X = 0, Y = 0, Type = TileType.Floor, North = BorderType.Door, IsExit = true, ExitDirection = Direction.North },
                new() { X = 1, Y = 0, Type = TileType.Floor },
                new() { X = 0, Y = 1, Type = TileType.Floor },
                new() { X = 1, Y = 1, Type = TileType.Floor },
            }
        };
    }
}
