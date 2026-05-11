using RPC.Engine;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

public class GridMovementTests
{
    [Theory]
    [InlineData(Direction.North, Direction.West)]
    [InlineData(Direction.East, Direction.North)]
    [InlineData(Direction.South, Direction.East)]
    [InlineData(Direction.West, Direction.South)]
    public void Direction_StrafeLeft_Perpendicular(Direction facing, Direction expected)
    {
        Assert.Equal(expected, facing.StrafeLeft());
    }

    [Theory]
    [InlineData(Direction.North, Direction.East)]
    [InlineData(Direction.East, Direction.South)]
    [InlineData(Direction.South, Direction.West)]
    [InlineData(Direction.West, Direction.North)]
    public void Direction_StrafeRight_Perpendicular(Direction facing, Direction expected)
    {
        Assert.Equal(expected, facing.StrafeRight());
    }

    [Theory]
    [InlineData(Direction.North, Direction.South)]
    [InlineData(Direction.East, Direction.West)]
    [InlineData(Direction.South, Direction.North)]
    [InlineData(Direction.West, Direction.East)]
    public void Direction_Opposite_Reverses(Direction facing, Direction expected)
    {
        Assert.Equal(expected, facing.Opposite());
    }

    [Fact]
    public void GameState_TryMoveForward_MovesInFacingDirection()
    {
        var state = CreateTestState();
        state.Player.Facing = Direction.North;
        var before = state.Player.Position;

        var moved = state.TryMoveForward();

        Assert.True(moved);
        Assert.Equal(before.Move(Direction.North), state.Player.Position);
    }

    [Fact]
    public void GameState_TryMoveBack_MovesOppositeFacing()
    {
        var state = CreateTestState();
        state.Player.Facing = Direction.North;
        var before = state.Player.Position;

        var moved = state.TryMoveBack();

        Assert.True(moved);
        Assert.Equal(before.Move(Direction.South), state.Player.Position);
    }

    [Fact]
    public void GameState_TryStrafeLeft_MovesPerpendicular()
    {
        var state = CreateTestState();
        state.Player.Facing = Direction.North;
        var before = state.Player.Position;

        var moved = state.TryStrafeLeft();

        Assert.True(moved);
        Assert.Equal(before.Move(Direction.West), state.Player.Position);
        Assert.Equal(Direction.North, state.Player.Facing);
    }

    [Fact]
    public void GameState_TryStrafeRight_MovesPerpendicular()
    {
        var state = CreateTestState();
        state.Player.Facing = Direction.North;
        var before = state.Player.Position;

        var moved = state.TryStrafeRight();

        Assert.True(moved);
        Assert.Equal(before.Move(Direction.East), state.Player.Position);
        Assert.Equal(Direction.North, state.Player.Facing);
    }

    [Theory]
    [InlineData(Direction.North, 5, 4)]
    [InlineData(Direction.East, 6, 5)]
    [InlineData(Direction.South, 5, 6)]
    [InlineData(Direction.West, 4, 5)]
    public void GameState_TryMoveForward_6DirectionCoverage(Direction facing, int expectedX, int expectedY)
    {
        var state = CreateTestState();
        state.Player.Facing = facing;

        state.TryMoveForward();

        Assert.Equal(new Position(expectedX, expectedY), state.Player.Position);
    }

    [Theory]
    [InlineData(Direction.North, 5, 6)]
    [InlineData(Direction.East, 4, 5)]
    [InlineData(Direction.South, 5, 4)]
    [InlineData(Direction.West, 6, 5)]
    public void GameState_TryMoveBack_6DirectionCoverage(Direction facing, int expectedX, int expectedY)
    {
        var state = CreateTestState();
        state.Player.Facing = facing;

        state.TryMoveBack();

        Assert.Equal(new Position(expectedX, expectedY), state.Player.Position);
    }

    [Theory]
    [InlineData(Direction.North, 4, 5)]
    [InlineData(Direction.East, 5, 4)]
    [InlineData(Direction.South, 6, 5)]
    [InlineData(Direction.West, 5, 6)]
    public void GameState_TryStrafeLeft_6DirectionCoverage(Direction facing, int expectedX, int expectedY)
    {
        var state = CreateTestState();
        state.Player.Facing = facing;

        state.TryStrafeLeft();

        Assert.Equal(new Position(expectedX, expectedY), state.Player.Position);
    }

    [Theory]
    [InlineData(Direction.North, 6, 5)]
    [InlineData(Direction.East, 5, 6)]
    [InlineData(Direction.South, 4, 5)]
    [InlineData(Direction.West, 5, 4)]
    public void GameState_TryStrafeRight_6DirectionCoverage(Direction facing, int expectedX, int expectedY)
    {
        var state = CreateTestState();
        state.Player.Facing = facing;

        state.TryStrafeRight();

        Assert.Equal(new Position(expectedX, expectedY), state.Player.Position);
    }

    [Fact]
    public void GameState_TryStrafe_DoesNotChangeFacing()
    {
        var state = CreateTestState();
        state.Player.Facing = Direction.East;

        state.TryStrafeLeft();
        Assert.Equal(Direction.East, state.Player.Facing);

        state.TryStrafeRight();
        Assert.Equal(Direction.East, state.Player.Facing);
    }

    [Fact]
    public void GameState_TryMoveForward_BlockedByWall_ReturnsFalse()
    {
        var state = CreateTestState(wallFacing: Direction.North);
        state.Player.Facing = Direction.North;

        var moved = state.TryMoveForward();

        Assert.False(moved);
        Assert.Equal(new Position(5, 5), state.Player.Position);
    }

    [Fact]
    public void GameState_TryMoveBack_BlockedByWall_ReturnsFalse()
    {
        var state = CreateTestState(wallFacing: Direction.South);
        state.Player.Facing = Direction.North;

        var moved = state.TryMoveBack();

        Assert.False(moved);
        Assert.Equal(new Position(5, 5), state.Player.Position);
    }

    [Fact]
    public void GameState_TryStrafeLeft_BlockedByWall_ReturnsFalse()
    {
        var state = CreateTestState(wallFacing: Direction.West);
        state.Player.Facing = Direction.North;

        var moved = state.TryStrafeLeft();

        Assert.False(moved);
        Assert.Equal(new Position(5, 5), state.Player.Position);
    }

    [Fact]
    public void GameState_TryStrafeRight_BlockedByWall_ReturnsFalse()
    {
        var state = CreateTestState(wallFacing: Direction.East);
        state.Player.Facing = Direction.North;

        var moved = state.TryStrafeRight();

        Assert.False(moved);
        Assert.Equal(new Position(5, 5), state.Player.Position);
    }

    [Fact]
    public void GameState_Movement_InCombat_ReturnsFalse()
    {
        var state = CreateTestState();
        state.EnterDungeon(CreateTestDungeon(), "test");
        state.TriggerEncounter();
        Assert.Equal(GameMode.Combat, state.Mode);

        var before = state.Player.Position;

        Assert.False(state.TryMoveForward());
        Assert.False(state.TryMoveBack());
        Assert.False(state.TryStrafeLeft());
        Assert.False(state.TryStrafeRight());
        Assert.Equal(before, state.Player.Position);
    }

    private static GameState CreateTestState(Direction? wallFacing = null)
    {
        var state = new GameState(seed: 42);
        var dungeon = CreateTestDungeon(wallFacing);
        state.EnterDungeon(dungeon, "test");
        state.Player.Position = new Position(5, 5);
        state.Player.Facing = Direction.North;
        return state;
    }

    private static Dungeon CreateTestDungeon(Direction? wallFacing = null)
    {
        var dungeon = new Dungeon(11, 11, "Test");
        for (int x = 0; x < 11; x++)
            for (int y = 0; y < 11; y++)
                dungeon.Tiles[x, y] = new Tile(TileType.Floor);

        if (wallFacing.HasValue)
        {
            var pos = new Position(5, 5);
            dungeon.Tiles[5, 5] = dungeon.Tiles[5, 5].WithBorder(wallFacing.Value, BorderType.Wall);
        }

        return dungeon;
    }
}
