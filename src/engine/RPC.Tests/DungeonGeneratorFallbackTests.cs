using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

public class DungeonGeneratorFallbackTests
{
    private static SegmentTile T(int x, int y, TileType type = TileType.Floor,
        Direction? exit = null, BorderType? n = null, BorderType? s = null) => new()
    {
        X = x,
        Y = y,
        Type = type,
        IsExit = exit is not null,
        ExitDirection = exit,
        North = n,
        South = s,
    };

    private static RoomSegment Seg(string id, IEnumerable<string> tags, params SegmentTile[] tiles) => new()
    {
        Id = id,
        Name = id,
        Tags = tags.ToList(),
        Tiles = tiles.ToList(),
    };

    private static List<RoomSegment> Pool() => new()
    {
        Seg("entrance", new[] { "entrance" },
            T(0, 0), T(1, 0),
            T(0, 1, exit: Direction.South, s: BorderType.Door), T(1, 1)),
        Seg("chamber", new[] { "chamber" },
            T(0, 0, exit: Direction.North, n: BorderType.Door), T(1, 0),
            T(0, 1), T(1, 1)),
        Seg("corridor", new[] { "corridor" },
            T(0, 0, exit: Direction.North, n: BorderType.Door),
            T(0, 1),
            T(0, 2, exit: Direction.South, s: BorderType.Door)),
    };

    [Fact]
    public void Generate_UnknownDungeonType_FallsBackToProceduralAndIsConnected()
    {
        // No templates registered → the LLM-referenced dungeon has no hand-authored config.
        var generator = new DungeonGenerator(Pool(), dungeonTemplates: null, encounterTables: null);

        var dungeon = generator.Generate("llm-invented-vault", seed: 123);

        var report = DungeonConnectivityValidator.Validate(dungeon);
        Assert.True(report.FullyConnected, $"{report.Unreachable.Count} unreachable tiles");
        Assert.True(report.WalkableCount > 4, "expected a stitched multi-room dungeon");

        bool hasUp = false, hasDown = false;
        for (int x = 0; x < dungeon.Width; x++)
            for (int y = 0; y < dungeon.Height; y++)
            {
                if (dungeon.Tiles[x, y].Type == TileType.StairsUp) hasUp = true;
                if (dungeon.Tiles[x, y].Type == TileType.StairsDown) hasDown = true;
            }
        Assert.True(hasUp && hasDown);
    }

    [Fact]
    public void Generate_UnknownDungeonType_IsDeterministic()
    {
        var a = new DungeonGenerator(Pool(), null, null).Generate("vault", seed: 77);
        var b = new DungeonGenerator(Pool(), null, null).Generate("vault", seed: 77);

        Assert.Equal(a.Width, b.Width);
        for (int x = 0; x < a.Width; x++)
            for (int y = 0; y < a.Height; y++)
                Assert.Equal(a.Tiles[x, y], b.Tiles[x, y]);
    }

    [Fact]
    public void Generate_ProceduralFallback_PacesEncountersWhenTableAvailable()
    {
        var tables = new EncounterTableRegistry();
        tables.LoadFromJson("haunted", """
        {
          "id": "haunted",
          "name": "Haunted",
          "entries": [
            { "id": "wretch", "weight": 1, "enemies": [], "dangerRating": 1 },
            { "id": "horror", "weight": 1, "enemies": [], "dangerRating": 9 }
          ]
        }
        """);

        var generator = new DungeonGenerator(Pool(), dungeonTemplates: null, encounterTables: tables);
        var dungeon = generator.Generate("haunted", seed: 5);

        Assert.Equal("haunted", dungeon.EncounterTableId);

        int encounters = 0;
        for (int x = 0; x < dungeon.Width; x++)
            for (int y = 0; y < dungeon.Height; y++)
                if (dungeon.Tiles[x, y].EncounterId is not null)
                    encounters++;

        Assert.True(encounters > 0, "procedural fallback should pace in encounters");
    }

    [Fact]
    public void Generate_KnownTemplate_ProducesConnectedDungeon()
    {
        var template = new DungeonTemplate(
            Id: "authored",
            Name: "Authored Hall",
            SegmentPool: new[] { "entrance", "chamber", "corridor" },
            SegmentPriority: new[] { "entrance" },
            TargetRooms: 4,
            BossEncounterId: "boss-1",
            EncounterTableId: "authored",
            WanderingTableId: "authored");

        var templates = new Dictionary<string, DungeonTemplate> { ["authored"] = template };
        var generator = new DungeonGenerator(Pool(), templates, null);

        var dungeon = generator.Generate("authored", seed: 9);

        Assert.True(DungeonConnectivityValidator.IsFullyConnected(dungeon));
    }
}
