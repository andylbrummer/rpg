using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

public class SegmentStitcherTests
{
    private static SegmentTile T(int x, int y, TileType type = TileType.Floor,
        Direction? exit = null, BorderType? n = null, BorderType? s = null,
        BorderType? e = null, BorderType? w = null) => new()
    {
        X = x,
        Y = y,
        Type = type,
        IsExit = exit is not null,
        ExitDirection = exit,
        North = n,
        South = s,
        East = e,
        West = w,
    };

    private static RoomSegment Seg(string id, IEnumerable<string> tags, params SegmentTile[] tiles) => new()
    {
        Id = id,
        Name = id,
        Tags = tags.ToList(),
        Tiles = tiles.ToList(),
    };

    // 2x2 entrance with a single south-facing exit at its bottom-left tile.
    private static RoomSegment EntranceSouth() => Seg("entrance", new[] { "entrance" },
        T(0, 0), T(1, 0),
        T(0, 1, exit: Direction.South, s: BorderType.Door), T(1, 1));

    // 2x2 chamber whose only exit faces north.
    private static RoomSegment ChamberNorth() => Seg("chamber", new[] { "chamber" },
        T(0, 0, exit: Direction.North, n: BorderType.Door), T(1, 0),
        T(0, 1), T(1, 1));

    // 1x3 corridor with exits on both ends (north + south).
    private static RoomSegment Corridor() => Seg("corridor", new[] { "corridor" },
        T(0, 0, exit: Direction.North, n: BorderType.Door),
        T(0, 1),
        T(0, 2, exit: Direction.South, s: BorderType.Door));

    private static List<RoomSegment> DefaultPool() => new() { EntranceSouth(), ChamberNorth(), Corridor() };

    [Fact]
    public void Stitch_ProducesFullyConnectedDungeon()
    {
        var stitcher = new SegmentStitcher(DefaultPool(), seed: 4242);
        var dungeon = stitcher.Stitch("Procedural Test", targetRooms: 8);

        var report = DungeonConnectivityValidator.Validate(dungeon);
        Assert.True(report.FullyConnected, $"{report.Unreachable.Count} unreachable tiles");
        Assert.True(report.WalkableCount > 4, "expected more than just the entrance room");
    }

    [Fact]
    public void Stitch_IsDeterministicForSameSeed()
    {
        var a = new SegmentStitcher(DefaultPool(), seed: 99).Stitch("A", targetRooms: 10);
        var b = new SegmentStitcher(DefaultPool(), seed: 99).Stitch("B", targetRooms: 10);

        Assert.Equal(a.Width, b.Width);
        Assert.Equal(a.Height, b.Height);
        for (int x = 0; x < a.Width; x++)
            for (int y = 0; y < a.Height; y++)
                Assert.Equal(a.Tiles[x, y], b.Tiles[x, y]);
    }

    [Fact]
    public void Stitch_DifferentSeedsDiffer()
    {
        var a = new SegmentStitcher(DefaultPool(), seed: 1).Stitch("A", targetRooms: 10);
        var b = new SegmentStitcher(DefaultPool(), seed: 2).Stitch("B", targetRooms: 10);

        bool anyDifferent = false;
        for (int x = 0; x < a.Width && !anyDifferent; x++)
            for (int y = 0; y < a.Height && !anyDifferent; y++)
                if (!a.Tiles[x, y].Equals(b.Tiles[x, y]))
                    anyDifferent = true;

        Assert.True(anyDifferent, "different seeds should produce different layouts");
    }

    [Fact]
    public void Stitch_PlacesEntranceAndExitStairs()
    {
        var dungeon = new SegmentStitcher(DefaultPool(), seed: 7).Stitch("Stairs", targetRooms: 6);

        bool hasUp = false, hasDown = false;
        for (int x = 0; x < dungeon.Width; x++)
            for (int y = 0; y < dungeon.Height; y++)
            {
                if (dungeon.Tiles[x, y].Type == TileType.StairsUp) hasUp = true;
                if (dungeon.Tiles[x, y].Type == TileType.StairsDown) hasDown = true;
            }

        Assert.True(hasUp, "missing entrance up-stairs");
        Assert.True(hasDown, "missing exit down-stairs");
    }

    [Fact]
    public void Stitch_RotatesSegmentsToFitMismatchedExits()
    {
        // The entrance exits EAST; the only attachable segment has a SOUTH-facing exit. A second
        // room can therefore only attach if the stitcher rotates the segment to present a
        // west-facing connector.
        var entranceEast = Seg("entrance", new[] { "entrance" },
            T(0, 0), T(1, 0, exit: Direction.East, e: BorderType.Door),
            T(0, 1), T(1, 1));
        var southOnly = Seg("chamber", new[] { "chamber" },
            T(0, 0), T(1, 0),
            T(0, 1, exit: Direction.South, s: BorderType.Door), T(1, 1));

        var pool = new List<RoomSegment> { entranceEast, southOnly };
        var dungeon = new SegmentStitcher(pool, seed: 555).Stitch("Rotated", targetRooms: 3);

        var report = DungeonConnectivityValidator.Validate(dungeon);
        Assert.True(report.FullyConnected);
        Assert.True(report.WalkableCount > 4, "rotation should have allowed a second room to attach");
    }

    [Fact]
    public void Stitch_FillsGapToReachIsolatedSegmentTile()
    {
        // A segment whose two floor tiles are walled off from each other internally. After stitching,
        // the gap-fill pass must carve a corridor so the isolated tile is still reachable.
        var entrance = EntranceSouth();
        var broken = Seg("broken", new[] { "chamber" },
            T(0, 0, exit: Direction.North, n: BorderType.Door, e: BorderType.Wall),
            T(1, 0, w: BorderType.Wall)); // (1,0) is sealed off from (0,0)

        var pool = new List<RoomSegment> { entrance, broken };
        var dungeon = new SegmentStitcher(pool, seed: 31).Stitch("Gap", targetRooms: 4);

        var report = DungeonConnectivityValidator.Validate(dungeon);
        Assert.True(report.FullyConnected, $"gap-fill failed: {report.Unreachable.Count} unreachable");
    }
}
