using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Dungeons;

/// <summary>
/// Classifies every room in an assembled dungeon by its relationship to the critical path
/// (entrance → boss). Pure and RNG-free: output depends only on geometry.
/// </summary>
public class DungeonPathClassifier
{
    private static readonly Direction[] Directions =
        { Direction.North, Direction.East, Direction.South, Direction.West };

    public IReadOnlyDictionary<int, RoomRole> Classify(Dungeon dungeon, string? bossEncounterId = null)
    {
        var result = new Dictionary<int, RoomRole>();
        if (dungeon.Rooms.Count == 0) return result;

        var entrance = dungeon.FindEntrance();
        var boss = FindBoss(dungeon, bossEncounterId);
        var criticalTiles = entrance is null || boss is null
            ? new HashSet<Position>()
            : ShortestPath(dungeon, entrance.Value, boss.Value);

        int entranceRoom = entrance is null ? -1 : dungeon.Tiles[entrance.Value.X, entrance.Value.Y].RoomId;
        int bossRoom = boss is null ? -1 : dungeon.Tiles[boss.Value.X, boss.Value.Y].RoomId;

        foreach (var room in dungeon.Rooms)
        {
            if (room.Id == entranceRoom) { result[room.Id] = RoomRole.Entrance; continue; }
            if (room.Id == bossRoom) { result[room.Id] = RoomRole.Boss; continue; }

            bool onPath = RoomTiles(dungeon, room.Id).Any(criticalTiles.Contains);
            if (onPath) { result[room.Id] = RoomRole.Critical; continue; }

            int connections = CountConnections(dungeon, room.Id);
            result[room.Id] = connections <= 1 ? RoomRole.DeadEnd : RoomRole.SideBranch;
        }

        return result;
    }

    private static IEnumerable<Position> RoomTiles(Dungeon dungeon, int roomId)
    {
        for (int x = 0; x < dungeon.Width; x++)
            for (int y = 0; y < dungeon.Height; y++)
                if (dungeon.Tiles[x, y].RoomId == roomId && dungeon.Tiles[x, y].IsWalkable)
                    yield return new Position(x, y);
    }

    // A "connection" is a walkable step from a room tile into a tile belonging to a different room.
    private static int CountConnections(Dungeon dungeon, int roomId)
    {
        var seen = new HashSet<int>();
        int count = 0;
        foreach (var pos in RoomTiles(dungeon, roomId))
        {
            foreach (var dir in Directions)
            {
                if (!dungeon.CanMoveTo(pos, dir)) continue;
                var next = pos.Move(dir);
                int otherRoom = dungeon.Tiles[next.X, next.Y].RoomId;
                if (otherRoom != roomId && seen.Add(otherRoom)) count++;
            }
        }
        return count;
    }


    // When a specific boss encounter id is provided, match exactly that tile (the procedural path
    // tags many tiles with encounter ids before decoration, so "first encounter tile" would latch
    // onto an arbitrary near-entrance encounter). When null, fall back to the first encounter tile.
    private static Position? FindBoss(Dungeon dungeon, string? bossEncounterId)
    {
        for (int x = 0; x < dungeon.Width; x++)
            for (int y = 0; y < dungeon.Height; y++)
            {
                var t = dungeon.Tiles[x, y];
                if (!t.IsWalkable || t.EncounterId == null) continue;
                if (bossEncounterId == null || t.EncounterId == bossEncounterId)
                    return new Position(x, y);
            }
        return null;
    }

    // BFS shortest path; returns the set of tiles on one shortest entrance→boss path.
    private static HashSet<Position> ShortestPath(Dungeon dungeon, Position start, Position goal)
    {
        var prev = new Dictionary<Position, Position>();
        var visited = new HashSet<Position> { start };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            if (pos == goal) break;
            foreach (var dir in Directions)
            {
                if (!dungeon.CanMoveTo(pos, dir)) continue;
                var next = pos.Move(dir);
                if (visited.Add(next)) { prev[next] = pos; queue.Enqueue(next); }
            }
        }

        var path = new HashSet<Position>();
        if (start != goal && !prev.ContainsKey(goal)) return path; // unreachable
        var cur = goal;
        path.Add(cur);
        while (cur != start && prev.TryGetValue(cur, out var p)) { cur = p; path.Add(cur); }
        return path;
    }
}
