using System.Linq;
using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using RPC.Host.Web.Presenters;

namespace RPC.Tests;

/// <summary>
/// T51b Cartographer detection. The Inkblood Cartographer passive senses positioned secrets within
/// Chebyshev (king-move) distance 2 at the end of each movement, marking them detected ("?" on the
/// automap) — existence only, not type. Explicit search promotes a detection to a full discovery.
/// </summary>
public class SecretDetectionTests
{
    private static GameState DungeonState(out Dungeon dungeon, Position player)
    {
        var gs = new GameState(seed: 1);
        dungeon = new Dungeon(6, 6, "test");
        for (int x = 0; x < 6; x++)
            for (int y = 0; y < 6; y++)
                dungeon.Tiles[x, y] = new Tile(TileType.Floor);
        gs.CurrentDungeon = dungeon;
        gs.CurrentDungeonType = "test";
        gs.Mode = GameMode.Exploration;
        gs.Player.Position = player;
        gs.Player.Facing = Direction.North;
        return gs;
    }

    private static void AddInkblood(GameState gs, int slot = 0) =>
        gs.Party.SetMember(slot, new CharacterState(
            Guid.NewGuid(), "Mapper", "inkblood", 1, 0,
            new BaseStats(4, 4, 4, 4, 4), 20, Equipment.Empty, Array.Empty<string>(), 0));

    [Fact]
    public void ChebyshevDistance_IsKingMove_NotManhattan()
    {
        Assert.Equal(2, new Position(0, 0).ChebyshevDistance(new Position(2, 2)));
        Assert.Equal(2, new Position(1, 1).ChebyshevDistance(new Position(3, 2)));
    }

    [Fact]
    public void Cartographer_AutoDetectsBreakableWall_WithinTwoTiles_AtEndOfMovement()
    {
        // Start 3 tiles away (Chebyshev 3 — out of range), step into range.
        var gs = DungeonState(out _, new Position(3, 4));
        AddInkblood(gs);
        gs.Secrets.Register(new SecretDef("crack", "breakable_wall", X: 3, Y: 1, Wall: "North"));

        Assert.False(gs.Journal.IsDetected("crack")); // not yet — start pos is distance 3

        Assert.True(gs.TryMoveForward()); // North -> (3,3), now Chebyshev 2

        Assert.True(gs.Journal.IsDetected("crack"));
        Assert.False(gs.Journal.IsDiscovered("crack")); // detection reveals existence, not type
        var log = gs.ActionLog.First(e => e.Type == "secret_detected");
        Assert.Equal("crack", log.Payload["secretId"]);
        Assert.Equal("cartographer", log.Payload["trigger"]);
    }

    [Fact]
    public void NonCartographerParty_DoesNotAutoDetect()
    {
        var gs = DungeonState(out _, new Position(3, 4));
        gs.Party.SetMember(0, new CharacterState(
            Guid.NewGuid(), "Grunt", "marcher", 1, 0,
            new BaseStats(4, 4, 4, 4, 4), 20, Equipment.Empty, Array.Empty<string>(), 0));
        gs.Secrets.Register(new SecretDef("crack", "breakable_wall", X: 3, Y: 3, Wall: "North"));

        Assert.True(gs.TryMoveForward());

        Assert.False(gs.Journal.IsDetected("crack"));
    }

    [Fact]
    public void Cartographer_DoesNotDetect_BeyondTwoTiles()
    {
        var gs = DungeonState(out _, new Position(3, 5));
        AddInkblood(gs);
        gs.Secrets.Register(new SecretDef("crack", "breakable_wall", X: 3, Y: 1, Wall: "North"));

        Assert.True(gs.TryMoveForward()); // -> (3,4), Chebyshev to (3,1) is 3

        Assert.False(gs.Journal.IsDetected("crack"));
    }

    [Fact]
    public void Detection_IsIdempotent_DoesNotRelog()
    {
        var gs = DungeonState(out _, new Position(3, 3));
        AddInkblood(gs);
        gs.Secrets.Register(new SecretDef("crack", "breakable_wall", X: 3, Y: 2, Wall: "North"));

        Assert.True(gs.DetectSecret("crack", "cartographer"));
        Assert.False(gs.DetectSecret("crack", "cartographer"));
        Assert.Equal(1, gs.ActionLog.Count(e => e.Type == "secret_detected"));
    }

    [Fact]
    public void AlreadyDiscoveredSecret_IsNotDetected()
    {
        var gs = DungeonState(out _, new Position(3, 3));
        AddInkblood(gs);
        gs.Secrets.Register(new SecretDef("crack", "breakable_wall", X: 3, Y: 2, Wall: "North"));
        Assert.True(gs.DiscoverSecret("breakable_wall", "crack", "search"));

        Assert.False(gs.DetectSecret("crack", "cartographer"));
        Assert.True(gs.Journal.IsDiscovered("crack"));
        Assert.False(gs.Journal.IsDetected("crack"));
    }

    [Fact]
    public void ExplicitSearch_RevealsType_AndCracksWallMaterial()
    {
        var gs = DungeonState(out var dungeon, new Position(3, 3));
        // Secret on the north border of (3,2), hidden as a plain breakable wall.
        dungeon.Tiles[3, 2] = new Tile(TileType.Floor, North: BorderType.BreakableWall);
        dungeon.Tiles[3, 1] = new Tile(TileType.Floor, South: BorderType.BreakableWall);
        gs.Secrets.Register(new SecretDef("crack", "breakable_wall", X: 3, Y: 2, Wall: "North"));

        var found = gs.SearchForSecrets();

        Assert.Contains("crack", found);
        Assert.True(gs.Journal.IsDiscovered("crack"));
        var log = gs.ActionLog.First(e => e.Type == "secret_discovered");
        Assert.Equal("search", log.Payload["trigger"]);
        // Cracked-wall material on reveal, on both adjacent tiles.
        Assert.Equal(BorderType.CrackedWall, dungeon.Tiles[3, 2].North);
        Assert.Equal(BorderType.CrackedWall, dungeon.Tiles[3, 1].South);
    }

    [Fact]
    public void ExplicitSearch_FarSecret_NotRevealed()
    {
        var gs = DungeonState(out _, new Position(0, 0));
        gs.Secrets.Register(new SecretDef("crack", "breakable_wall", X: 5, Y: 5, Wall: "North"));

        Assert.Empty(gs.SearchForSecrets());
        Assert.False(gs.Journal.IsDiscovered("crack"));
    }

    [Fact]
    public void DetectedSecret_ExposedInStatePayload_ForAutomapMarker()
    {
        var gs = DungeonState(out _, new Position(3, 3));
        AddInkblood(gs);
        gs.Secrets.Register(new SecretDef("crack", "breakable_wall", X: 3, Y: 2, Wall: "North"));
        Assert.True(gs.DetectSecret("crack", "cartographer"));

        var detected = JsonSerializer.Serialize(ExplorationPresenter.Present(gs).DetectedSecrets);
        Assert.Contains("\"id\":\"crack\"", detected);
        Assert.Contains("\"x\":3", detected);
        Assert.Contains("\"y\":2", detected);

        // Once discovered, it drops out of the detected list (type now known, no "?").
        gs.DiscoverSecret("breakable_wall", "crack", "search");
        var afterDiscovery = JsonSerializer.Serialize(ExplorationPresenter.Present(gs).DetectedSecrets);
        Assert.DoesNotContain("crack", afterDiscovery);
    }
}
