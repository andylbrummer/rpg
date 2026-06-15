using RPC.Engine;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Save;

namespace RPC.Tests;

/// <summary>
/// Verifies the core task contract: a dungeon can be reproduced from its persisted
/// <see cref="DungeonGenerationIdentity"/> alone, both at the generator level and across a full
/// save/load round-trip through the <see cref="IDungeonGenerator"/> seam.
/// </summary>
public class DungeonIdentityPersistenceTests : IDisposable
{
    private readonly string _testSavePath;

    public DungeonIdentityPersistenceTests()
    {
        _testSavePath = Path.Combine(Path.GetTempPath(), $"rpc_identity_test_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testSavePath)) File.Delete(_testSavePath);
    }

    private static SegmentTile T(int x, int y, Direction? exit = null,
        BorderType? n = null, BorderType? s = null) => new()
    {
        X = x,
        Y = y,
        Type = TileType.Floor,
        IsExit = exit is not null,
        ExitDirection = exit,
        North = n,
        South = s,
    };

    private static RoomSegment Seg(string id, params SegmentTile[] tiles) => new()
    {
        Id = id,
        Name = id,
        Tags = new List<string> { id },
        Tiles = tiles.ToList(),
    };

    private static List<RoomSegment> Pool() => new()
    {
        Seg("entrance",
            T(0, 0), T(1, 0),
            T(0, 1, exit: Direction.South, s: BorderType.Door), T(1, 1)),
        Seg("chamber",
            T(0, 0, exit: Direction.North, n: BorderType.Door), T(1, 0),
            T(0, 1), T(1, 1)),
        Seg("corridor",
            T(0, 0, exit: Direction.North, n: BorderType.Door),
            T(0, 1),
            T(0, 2, exit: Direction.South, s: BorderType.Door)),
    };

    private static bool TilesEqual(Dungeon a, Dungeon b)
    {
        if (a.Width != b.Width || a.Height != b.Height) return false;
        for (int x = 0; x < a.Width; x++)
            for (int y = 0; y < a.Height; y++)
                if (!Equals(a.Tiles[x, y], b.Tiles[x, y])) return false;
        return true;
    }

    [Fact]
    public void Generate_FromPersistedIdentity_ReproducesStructurallyEqualDungeon()
    {
        var generator = new DungeonGenerator(Pool(), dungeonTemplates: null, encounterTables: null);

        // Generate, then keep ONLY the resolved identity (as save/load would persist).
        var first = generator.Generate(new DungeonGenerationRequest("vault", Seed: 314, ContentHash: "hash-a"));
        var identity = first.Identity;

        // Regenerate purely from the persisted identity.
        var replay = generator.Generate(new DungeonGenerationRequest(identity.DungeonType, identity.Seed, identity.ContentHash));

        Assert.Equal("vault", identity.DungeonType);
        Assert.Equal(314, identity.Seed);
        Assert.Equal("hash-a", identity.ContentHash);
        Assert.True(TilesEqual(first.Dungeon, replay.Dungeon),
            "regenerating from the persisted identity must yield a structurally-equal dungeon");
    }

    [Fact]
    public void Generate_NullSeedRequest_ResolvesStableSeedIntoIdentity()
    {
        var generator = new DungeonGenerator(Pool(), dungeonTemplates: null, encounterTables: null);

        // No seed supplied: the identity must still carry a concrete effective seed that
        // reproduces the dungeon.
        var result = generator.Generate(new DungeonGenerationRequest("vault"));

        var replay = generator.Generate(
            new DungeonGenerationRequest(result.Identity.DungeonType, result.Identity.Seed, result.Identity.ContentHash));

        Assert.True(TilesEqual(result.Dungeon, replay.Dungeon));
    }

    [Fact]
    public void SaveLoadSeam_RegeneratesDungeonFromPersistedIdentity()
    {
        var generator = new DungeonGenerator(Pool(), dungeonTemplates: null, encounterTables: null);

        var original = new GameState(seed: 1);
        var generated = generator.Generate(new DungeonGenerationRequest("vault", Seed: 999));
        original.EnterDungeon(generated.Dungeon, "vault");
        original.SaveGame(_testSavePath);

        // Fresh state, no dungeon in memory: the loader must rebuild it via the generator seam.
        var loaded = new GameState(seed: 2);
        Assert.True(loaded.LoadGame(_testSavePath, dungeonGenerator: generator));

        Assert.NotNull(loaded.CurrentDungeon);
        Assert.Equal("vault", loaded.CurrentDungeonType);
        Assert.True(TilesEqual(generated.Dungeon, loaded.CurrentDungeon!),
            "save/load must reproduce the same dungeon from persisted identity (type + seed)");
    }
}
