namespace RPC.Engine.Models.Dungeons;

public readonly record struct Position(int X, int Y)
{
    public Position Move(Direction direction) => direction switch
    {
        Direction.North => new Position(X, Y - 1),
        Direction.East => new Position(X + 1, Y),
        Direction.South => new Position(X, Y + 1),
        Direction.West => new Position(X - 1, Y),
        _ => this
    };

    /// <summary>Chebyshev (king-move) distance: <c>max(|dx|, |dy|)</c>. The natural radius for a
    /// square sensing box on a grid — used by the Cartographer 2-tile detection passive, matching
    /// the square reveal box of <c>ExplorationService.ExploreAroundPlayer</c>.</summary>
    public int ChebyshevDistance(Position other)
        => Math.Max(Math.Abs(other.X - X), Math.Abs(other.Y - Y));

    public Direction DirectionTo(Position other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;

        if (Math.Abs(dx) > Math.Abs(dy))
            return dx > 0 ? Direction.East : Direction.West;
        else
            return dy > 0 ? Direction.South : Direction.North;
    }
}

public enum Direction
{
    North,
    East,
    South,
    West
}

public static class DirectionExtensions
{
    public static Direction TurnLeft(this Direction dir) => dir switch
    {
        Direction.North => Direction.West,
        Direction.East => Direction.North,
        Direction.South => Direction.East,
        Direction.West => Direction.South,
        _ => dir
    };

    public static Direction TurnRight(this Direction dir) => dir switch
    {
        Direction.North => Direction.East,
        Direction.East => Direction.South,
        Direction.South => Direction.West,
        Direction.West => Direction.North,
        _ => dir
    };

    public static float ToRadians(this Direction dir) => dir switch
    {
        Direction.North => 0,
        Direction.East => (float)(Math.PI / 2),
        Direction.South => (float)Math.PI,
        Direction.West => (float)(-Math.PI / 2),
        _ => 0
    };

    public static Direction Opposite(this Direction dir) => dir switch
    {
        Direction.North => Direction.South,
        Direction.East => Direction.West,
        Direction.South => Direction.North,
        Direction.West => Direction.East,
        _ => dir
    };

    public static Direction StrafeLeft(this Direction dir) => dir switch
    {
        Direction.North => Direction.West,
        Direction.East => Direction.North,
        Direction.South => Direction.East,
        Direction.West => Direction.South,
        _ => dir
    };

    public static Direction StrafeRight(this Direction dir) => dir switch
    {
        Direction.North => Direction.East,
        Direction.East => Direction.South,
        Direction.South => Direction.West,
        Direction.West => Direction.North,
        _ => dir
    };
}
