using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Dungeons;

/// <summary>
/// Connectivity + pacing validation for an assembled dungeon (hand-authored or procedurally
/// stitched). Walks the tile graph from the entrance using the dungeon's own movement rules and
/// reports whether every walkable tile is reachable, plus a basic pacing depth metric.
/// </summary>
public static class DungeonConnectivityValidator
{
    public record ConnectivityReport(
        bool FullyConnected,
        int WalkableCount,
        int ReachableCount,
        IReadOnlyList<Position> Unreachable,
        int MaxDepth);

    private static readonly Direction[] Directions =
        { Direction.North, Direction.East, Direction.South, Direction.West };

    public static ConnectivityReport Validate(Dungeon dungeon)
    {
        var walkable = new List<Position>();
        Position? entrance = null;

        for (int x = 0; x < dungeon.Width; x++)
        {
            for (int y = 0; y < dungeon.Height; y++)
            {
                var tile = dungeon.Tiles[x, y];
                if (!tile.IsWalkable) continue;
                var pos = new Position(x, y);
                walkable.Add(pos);
                if (tile.Type == TileType.StairsUp && entrance is null)
                    entrance = pos; // prefer the up-stairs as the entrance
            }
        }

        if (walkable.Count == 0)
            return new ConnectivityReport(true, 0, 0, Array.Empty<Position>(), 0);

        entrance ??= walkable[0];

        // BFS over the tile graph using the dungeon's movement rules (respects walls/secret doors).
        var visited = new HashSet<Position> { entrance.Value };
        var queue = new Queue<(Position Pos, int Depth)>();
        queue.Enqueue((entrance.Value, 0));
        var maxDepth = 0;

        while (queue.Count > 0)
        {
            var (pos, depth) = queue.Dequeue();
            if (depth > maxDepth) maxDepth = depth;

            foreach (var dir in Directions)
            {
                if (!dungeon.CanMoveTo(pos, dir)) continue;
                var next = pos.Move(dir);
                if (visited.Add(next))
                    queue.Enqueue((next, depth + 1));
            }
        }

        var unreachable = walkable.Where(p => !visited.Contains(p)).ToList();
        return new ConnectivityReport(
            unreachable.Count == 0,
            walkable.Count,
            visited.Count,
            unreachable,
            maxDepth);
    }

    public static bool IsFullyConnected(Dungeon dungeon) => Validate(dungeon).FullyConnected;
}
