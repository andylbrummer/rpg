using RPC.Engine;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

public class StrafeDungeonIntegrationTests
{
    [Fact]
    public void BrokenEngineDungeon_StrafeRight_FromEntrance()
    {
        var state = new GameState(seed: 42);
        var dungeon = CreateBrokenEngineDungeon();
        state.EnterDungeon(dungeon, "broken_engine");

        // Player should be at entrance
        var startPos = state.Player.Position;
        Assert.Equal(Direction.North, state.Player.Facing);

        // Try strafe right (East from North)
        var moved = state.TryStrafeRight();
        Assert.True(moved, $"Strafe right failed from {startPos} facing {state.Player.Facing}. Tile: {dungeon.Tiles[startPos.X, startPos.Y]}");
        Assert.Equal(startPos.Move(Direction.East), state.Player.Position);
    }

    [Fact]
    public void BrokenEngineDungeon_MoveBack_FromEntrance()
    {
        var state = new GameState(seed: 42);
        var dungeon = CreateBrokenEngineDungeon();
        state.EnterDungeon(dungeon, "broken_engine");

        var startPos = state.Player.Position;
        Assert.Equal(Direction.North, state.Player.Facing);

        var moved = state.TryMoveBack();
        Assert.True(moved, $"Move back failed from {startPos}. Tile: {dungeon.Tiles[startPos.X, startPos.Y]}");
        Assert.Equal(startPos.Move(Direction.South), state.Player.Position);
    }

    [Fact]
    public void BrokenEngineDungeon_MoveForwardEast_FromEntrance()
    {
        var state = new GameState(seed: 42);
        var dungeon = CreateBrokenEngineDungeon();
        state.EnterDungeon(dungeon, "broken_engine");

        var startPos = state.Player.Position;
        state.Player.Facing = Direction.East;

        var moved = state.TryMoveForward();
        Assert.True(moved, $"Move forward East failed from {startPos}. Tile: {dungeon.Tiles[startPos.X, startPos.Y]}");
        Assert.Equal(startPos.Move(Direction.East), state.Player.Position);
    }

    private static Dungeon CreateBrokenEngineDungeon()
    {
        var seed = "broken_engine".GetHashCode();
        var builder = new DungeonBuilder(seed: seed);

        builder.AddSegment(CreateEntranceRoom());
        builder.AddSegment(CreateCorridor());
        builder.AddSegment(CreateChamber());
        builder.AddSegment(CreateDeadEnd());

        return builder.Build("Broken Engine", 8);
    }

    private static RoomSegment CreateEntranceRoom()
    {
        return new RoomSegment
        {
            Id = "entrance",
            Name = "Entrance Hall",
            Tags = new() { "entrance" },
            Tiles = new()
            {
                new() { X = 0, Y = 0, Type = TileType.Floor },
                new() { X = 1, Y = 0, Type = TileType.Floor, North = BorderType.Door, IsExit = true, ExitDirection = Direction.North },
                new() { X = 2, Y = 0, Type = TileType.Floor },
                new() { X = 0, Y = 1, Type = TileType.Floor },
                new() { X = 1, Y = 1, Type = TileType.Floor },
                new() { X = 2, Y = 1, Type = TileType.Floor },
            }
        };
    }

    private static RoomSegment CreateCorridor()
    {
        return new RoomSegment
        {
            Id = "corridor",
            Name = "Corridor",
            Tiles = new()
            {
                new() { X = 0, Y = 0, Type = TileType.Floor, South = BorderType.Door, IsExit = true, ExitDirection = Direction.South },
                new() { X = 0, Y = -1, Type = TileType.Floor },
                new() { X = 0, Y = -2, Type = TileType.Floor },
                new() { X = 0, Y = -3, Type = TileType.Floor, North = BorderType.Door, IsExit = true, ExitDirection = Direction.North },
            }
        };
    }

    private static RoomSegment CreateChamber()
    {
        return new RoomSegment
        {
            Id = "chamber",
            Name = "Small Chamber",
            Tiles = new()
            {
                new() { X = 0, Y = 1, Type = TileType.Floor, South = BorderType.Door, IsExit = true, ExitDirection = Direction.South },
                new() { X = -1, Y = 0, Type = TileType.Floor },
                new() { X = 0, Y = 0, Type = TileType.Floor },
                new() { X = 1, Y = 0, Type = TileType.Floor },
                new() { X = -1, Y = -1, Type = TileType.Floor },
                new() { X = 0, Y = -1, Type = TileType.Floor },
                new() { X = 1, Y = -1, Type = TileType.Floor },
            }
        };
    }

    private static RoomSegment CreateDeadEnd()
    {
        return new RoomSegment
        {
            Id = "dead_end",
            Name = "Dead End",
            Tiles = new()
            {
                new() { X = 0, Y = 1, Type = TileType.Floor, South = BorderType.Door, IsExit = true, ExitDirection = Direction.South },
                new() { X = 0, Y = 0, Type = TileType.Floor },
            }
        };
    }
}
