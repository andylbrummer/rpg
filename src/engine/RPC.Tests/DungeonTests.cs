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
        dungeon.Tiles[5, 6] = new Tile(TileType.Wall);
        
        Assert.True(dungeon.CanMoveTo(new Position(5, 5)));
        Assert.False(dungeon.CanMoveTo(new Position(5, 6)));
        Assert.False(dungeon.CanMoveTo(new Position(100, 100))); // Out of bounds
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

    private static RoomSegment CreateTestRoom()
    {
        return new RoomSegment
        {
            Id = "test",
            Name = "Test Room",
            Tags = new() { "entrance" },
            Tiles = new()
            {
                new() { X = 0, Y = 0, Type = TileType.Floor },
                new() { X = 1, Y = 0, Type = TileType.Floor },
                new() { X = 0, Y = 1, Type = TileType.Floor },
                new() { X = 1, Y = 1, Type = TileType.Floor },
                new() { X = 1, Y = -1, Type = TileType.Floor, IsExit = true, ExitDirection = Direction.North },
            }
        };
    }
}
