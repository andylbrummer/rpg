using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Dungeons;

/// <summary>
/// Procedural segment-stitching assembler. Unlike <see cref="DungeonBuilder"/> (which places
/// segments at their authored orientation against matching exits), the stitcher will <b>rotate</b>
/// segments to fit any open exit, <b>joins</b> the shared border so the rooms are actually passable,
/// then <b>fills gaps</b> by carving corridors until <see cref="DungeonConnectivityValidator"/>
/// reports the result fully connected. This is the Phase 3 procedural fallback used when no
/// hand-authored layout is available.
/// </summary>
public class SegmentStitcher
{
    private readonly List<RoomSegment> _segments;
    private readonly GameRandom _random;
    private readonly int _seed;
    private readonly int _width;
    private readonly int _height;

    private const int MaxCarvePasses = 64;

    public SegmentStitcher(IEnumerable<RoomSegment> segments, int seed, int width = 64, int height = 64)
    {
        _segments = segments.ToList();
        _seed = seed;
        _random = new GameRandom(seed);
        _width = width;
        _height = height;
    }

    /// <summary>A segment's tiles normalized to 0-based coordinates at a chosen rotation.</summary>
    private sealed record Oriented(IReadOnlyList<SegmentTile> Tiles, int Width, int Height);

    private readonly record struct OpenExit(Position Position, Direction Direction);

    public Dungeon Stitch(string name, int targetRooms = 10)
    {
        var dungeon = new Dungeon(_width, _height, name);
        dungeon.Seed = _seed;

        if (_segments.Count == 0)
        {
            // Degenerate fallback: a single walkable entrance tile so the dungeon is still valid.
            dungeon.Tiles[_width / 2, _height / 2] = new Tile(TileType.StairsUp);
            return dungeon;
        }

        var entranceSeg = _segments.FirstOrDefault(s => s.Tags.Contains("entrance")) ?? _segments[0];
        var entrance = Normalize(entranceSeg, 0);

        // Centre the entrance roughly in the middle of the board.
        var entranceOffset = new Position(
            _width / 2 - entrance.Width / 2,
            _height / 2 - entrance.Height / 2);

        var openExits = new List<OpenExit>();
        int roomId = 0;
        PlaceOriented(dungeon, entrance, entranceOffset, roomId++, openExits);

        // Promote one non-exit entrance floor tile to the up-stairs entrance.
        PlaceEntranceStairs(dungeon, entrance, entranceOffset);

        int placed = 1;
        int attempts = 0;
        int maxAttempts = targetRooms * 20;

        while (placed < targetRooms && openExits.Count > 0 && attempts < maxAttempts)
        {
            attempts++;

            var exitIdx = _random.Next(openExits.Count);
            var parentExit = openExits[exitIdx];

            var segment = _segments[_random.Next(_segments.Count)];

            if (TryAttach(dungeon, segment, parentExit, roomId, openExits, exitIdx))
            {
                roomId++;
                placed++;
            }
            else
            {
                // This exit could not host the chosen segment; if we've thrashed on it, retire it
                // so the loop keeps making progress instead of repeatedly retrying a dead exit.
                if (attempts % targetRooms == 0)
                    openExits.RemoveAt(exitIdx);
            }
        }

        DeriveBorders(dungeon);

        // Pacing: put the down-stairs on the tile furthest (by walk distance) from the entrance.
        PlaceExitStairs(dungeon);

        // Fill gaps: guarantee every walkable tile is reachable from the entrance.
        EnsureConnected(dungeon);

        return dungeon;
    }

    // ---- Placement -------------------------------------------------------

    private bool TryAttach(Dungeon dungeon, RoomSegment segment, OpenExit parentExit, int roomId,
        List<OpenExit> openExits, int parentExitIdx)
    {
        // The new segment must expose an exit facing back toward the parent.
        var need = parentExit.Direction.Opposite();
        var target = parentExit.Position.Move(parentExit.Direction);

        // Try the four rotations in a randomized order so the layout varies with the seed.
        foreach (var turns in ShuffledRotations())
        {
            var oriented = Normalize(segment, turns);
            var connectorCandidates = oriented.Tiles
                .Where(t => t.IsExit && t.ExitDirection == need)
                .ToList();
            if (connectorCandidates.Count == 0) continue;

            var connector = connectorCandidates[_random.Next(connectorCandidates.Count)];
            var offset = new Position(target.X - connector.X, target.Y - connector.Y);

            if (!FitsAt(dungeon, oriented, offset)) continue;

            PlaceOriented(dungeon, oriented, offset, roomId, openExits, exclude: connector);

            // Join the two rooms: open the shared border on both sides.
            OpenBorder(dungeon, parentExit.Position, parentExit.Direction);
            OpenBorder(dungeon, target, need);

            // The parent exit is now consumed.
            openExits.RemoveAt(parentExitIdx);
            return true;
        }

        return false;
    }

    private bool FitsAt(Dungeon dungeon, Oriented oriented, Position offset)
    {
        foreach (var tile in oriented.Tiles)
        {
            var world = new Position(offset.X + tile.X, offset.Y + tile.Y);
            if (!dungeon.IsValidPosition(world)) return false;
            if (dungeon.Tiles[world.X, world.Y].Type != TileType.Empty) return false;
        }
        return true;
    }

    private static void PlaceOriented(Dungeon dungeon, Oriented oriented, Position offset, int roomId,
        List<OpenExit> openExits, SegmentTile? exclude = null)
    {
        foreach (var tile in oriented.Tiles)
        {
            var world = new Position(offset.X + tile.X, offset.Y + tile.Y);
            if (!dungeon.IsValidPosition(world)) continue;

            dungeon.Tiles[world.X, world.Y] = new Tile(
                tile.Type,
                tile.North ?? BorderType.None,
                tile.South ?? BorderType.None,
                tile.East ?? BorderType.None,
                tile.West ?? BorderType.None,
                roomId);

            if (tile.IsExit && tile.ExitDirection is { } dir && !ReferenceEquals(tile, exclude))
                openExits.Add(new OpenExit(world, dir));
        }
    }

    private static void PlaceEntranceStairs(Dungeon dungeon, Oriented entrance, Position offset)
    {
        // Prefer an interior, non-exit floor tile so the stairs aren't sitting on a doorway.
        var pick = entrance.Tiles.FirstOrDefault(t => t.Type == TileType.Floor && !t.IsExit)
            ?? entrance.Tiles.FirstOrDefault(t => t.Type == TileType.Floor);
        if (pick is null) return;

        var world = new Position(offset.X + pick.X, offset.Y + pick.Y);
        if (!dungeon.IsValidPosition(world)) return;
        dungeon.Tiles[world.X, world.Y] = dungeon.Tiles[world.X, world.Y] with { Type = TileType.StairsUp };
    }

    private static void OpenBorder(Dungeon dungeon, Position pos, Direction dir)
    {
        if (!dungeon.IsValidPosition(pos)) return;
        dungeon.Tiles[pos.X, pos.Y] = dungeon.Tiles[pos.X, pos.Y].WithBorder(dir, BorderType.Door);
    }

    // ---- Down-stairs placement (pacing) ----------------------------------

    private static void PlaceExitStairs(Dungeon dungeon)
    {
        var entrance = dungeon.FindEntrance();
        if (entrance is null) return;

        // BFS for the deepest reachable tile.
        var visited = new HashSet<Position> { entrance.Value };
        var queue = new Queue<(Position Pos, int Depth)>();
        queue.Enqueue((entrance.Value, 0));
        Position deepest = entrance.Value;
        int bestDepth = -1;

        while (queue.Count > 0)
        {
            var (pos, depth) = queue.Dequeue();
            if (depth > bestDepth && dungeon.Tiles[pos.X, pos.Y].Type == TileType.Floor)
            {
                bestDepth = depth;
                deepest = pos;
            }
            foreach (var dir in AllDirections)
            {
                if (!dungeon.CanMoveTo(pos, dir)) continue;
                var next = pos.Move(dir);
                if (visited.Add(next))
                    queue.Enqueue((next, depth + 1));
            }
        }

        if (bestDepth >= 0)
            dungeon.Tiles[deepest.X, deepest.Y] = dungeon.Tiles[deepest.X, deepest.Y] with { Type = TileType.StairsDown };
    }

    // ---- Gap-filling -----------------------------------------------------

    private void EnsureConnected(Dungeon dungeon)
    {
        for (int pass = 0; pass < MaxCarvePasses; pass++)
        {
            var report = DungeonConnectivityValidator.Validate(dungeon);
            if (report.FullyConnected) return;

            var orphan = report.Unreachable[0];
            var anchor = NearestReachable(dungeon, orphan);
            if (anchor is null) return; // nothing to connect to

            CarveCorridor(dungeon, anchor.Value, orphan);
            DeriveBorders(dungeon);
        }
    }

    /// <summary>Find the reachable walkable tile closest (Manhattan) to <paramref name="orphan"/>.</summary>
    private static Position? NearestReachable(Dungeon dungeon, Position orphan)
    {
        var entrance = dungeon.FindEntrance();
        if (entrance is null) return null;

        var reachable = ReachableSet(dungeon, entrance.Value);
        Position? best = null;
        int bestDist = int.MaxValue;
        foreach (var pos in reachable)
        {
            int dist = Math.Abs(pos.X - orphan.X) + Math.Abs(pos.Y - orphan.Y);
            // Tie-break by coordinate so the choice does not depend on HashSet iteration order,
            // keeping the carved corridor (and thus the whole layout) fully deterministic.
            if (best is null || dist < bestDist ||
                (dist == bestDist && (pos.X < best.Value.X || (pos.X == best.Value.X && pos.Y < best.Value.Y))))
            {
                bestDist = dist;
                best = pos;
            }
        }
        return best;
    }

    /// <summary>Carve an L-shaped floor corridor from <paramref name="from"/> to <paramref name="to"/>, opening borders along the way.</summary>
    private static void CarveCorridor(Dungeon dungeon, Position from, Position to)
    {
        var cur = from;
        while (cur.X != to.X)
        {
            var dir = to.X > cur.X ? Direction.East : Direction.West;
            cur = Step(dungeon, cur, dir);
        }
        while (cur.Y != to.Y)
        {
            var dir = to.Y > cur.Y ? Direction.South : Direction.North;
            cur = Step(dungeon, cur, dir);
        }
    }

    private static Position Step(Dungeon dungeon, Position cur, Direction dir)
    {
        var next = cur.Move(dir);
        if (!dungeon.IsValidPosition(next)) return cur;

        // Lay floor where the corridor passes through empty space; keep existing walkable tiles.
        if (dungeon.Tiles[next.X, next.Y].Type == TileType.Empty)
            dungeon.Tiles[next.X, next.Y] = new Tile(TileType.Floor);

        // Open the shared border in both directions so the step is passable.
        dungeon.Tiles[cur.X, cur.Y] = dungeon.Tiles[cur.X, cur.Y].WithBorder(dir, BorderType.None);
        dungeon.Tiles[next.X, next.Y] = dungeon.Tiles[next.X, next.Y].WithBorder(dir.Opposite(), BorderType.None);
        return next;
    }

    // ---- Shared graph helpers --------------------------------------------

    private static readonly Direction[] AllDirections =
        { Direction.North, Direction.East, Direction.South, Direction.West };


    private static HashSet<Position> ReachableSet(Dungeon dungeon, Position entrance)
    {
        var visited = new HashSet<Position> { entrance };
        var queue = new Queue<Position>();
        queue.Enqueue(entrance);
        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            foreach (var dir in AllDirections)
            {
                if (!dungeon.CanMoveTo(pos, dir)) continue;
                var next = pos.Move(dir);
                if (visited.Add(next))
                    queue.Enqueue(next);
            }
        }
        return visited;
    }

    // ---- Border derivation (walls around the assembled geometry) ----------

    private static void DeriveBorders(Dungeon dungeon)
    {
        for (int x = 0; x < dungeon.Width; x++)
        {
            for (int y = 0; y < dungeon.Height; y++)
            {
                var tile = dungeon.Tiles[x, y];
                if (!tile.IsWalkable) continue;

                tile = WallIfEdge(dungeon, tile, x, y, Direction.North);
                tile = WallIfEdge(dungeon, tile, x, y, Direction.South);
                tile = WallIfEdge(dungeon, tile, x, y, Direction.East);
                tile = WallIfEdge(dungeon, tile, x, y, Direction.West);

                dungeon.Tiles[x, y] = tile;
            }
        }
    }

    private static Tile WallIfEdge(Dungeon dungeon, Tile tile, int x, int y, Direction dir)
    {
        if (tile.GetBorder(dir) != BorderType.None) return tile; // respect authored/joined borders
        var n = new Position(x, y).Move(dir);
        bool neighborWalkable = dungeon.IsValidPosition(n) && dungeon.Tiles[n.X, n.Y].IsWalkable;
        return neighborWalkable ? tile : tile.WithBorder(dir, BorderType.Wall);
    }

    // ---- Rotation --------------------------------------------------------

    private int[] ShuffledRotations()
    {
        var turns = new[] { 0, 1, 2, 3 };
        for (int i = turns.Length - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (turns[i], turns[j]) = (turns[j], turns[i]);
        }
        return turns;
    }

    /// <summary>
    /// Normalize a segment to 0-based coordinates rotated by <paramref name="turns"/> right-turns
    /// (90° clockwise each). Tile coordinates, borders, and exit directions all rotate with the
    /// geometry.
    /// </summary>
    private static Oriented Normalize(RoomSegment segment, int turns)
    {
        turns = ((turns % 4) + 4) % 4;

        if (segment.Tiles.Count == 0)
            return new Oriented(Array.Empty<SegmentTile>(), 0, 0);

        // Shift authored coordinates (which may be 1-based) down to a 0-based box first.
        int minX = segment.Tiles.Min(t => t.X);
        int minY = segment.Tiles.Min(t => t.Y);
        int w = segment.Tiles.Max(t => t.X) - minX + 1;
        int h = segment.Tiles.Max(t => t.Y) - minY + 1;

        var result = new List<SegmentTile>(segment.Tiles.Count);
        foreach (var t in segment.Tiles)
        {
            int x = t.X - minX;
            int y = t.Y - minY;
            int cw = w, ch = h;
            for (int k = 0; k < turns; k++)
            {
                // 90° clockwise in screen coords (y-down): (x, y) -> (ch-1 - y, x),
                // using the box height *before* this turn; the box dims swap afterwards.
                (x, y) = (ch - 1 - y, x);
                (cw, ch) = (ch, cw);
            }

            var st = new SegmentTile
            {
                X = x,
                Y = y,
                Type = t.Type,
                IsExit = t.IsExit,
                ExitDirection = t.ExitDirection is { } ed ? Rotate(ed, turns) : null,
            };

            // Move each authored border into the direction it rotates into.
            SetBorder(st, Rotate(Direction.North, turns), t.North);
            SetBorder(st, Rotate(Direction.South, turns), t.South);
            SetBorder(st, Rotate(Direction.East, turns), t.East);
            SetBorder(st, Rotate(Direction.West, turns), t.West);

            result.Add(st);
        }

        int outW = (turns % 2 == 0) ? w : h;
        int outH = (turns % 2 == 0) ? h : w;
        return new Oriented(result, outW, outH);
    }

    private static void SetBorder(SegmentTile t, Direction dir, BorderType? value)
    {
        if (value is null) return;
        switch (dir)
        {
            case Direction.North: t.North = value; break;
            case Direction.South: t.South = value; break;
            case Direction.East: t.East = value; break;
            case Direction.West: t.West = value; break;
        }
    }

    private static Direction Rotate(Direction dir, int turns)
    {
        for (int k = 0; k < turns; k++)
            dir = dir.TurnRight();
        return dir;
    }
}
