using System.Linq;
using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using RPC.Host.Web.Presenters;
using Xunit;

namespace RPC.Tests;

/// <summary>
/// Regression guard for the presenter→client break-wall wiring gap: the Break control must be fed
/// from a list of <em>discovered, still-intact</em> breakable walls — disjoint from the detected-but-
/// unrevealed <c>DetectedSecrets</c> "?" set that drives the search affordance. A wall the engine will
/// actually let the party break (<see cref="GameState.BreakWall"/> requires <c>IsDiscovered</c>) must
/// appear here; one that has already been broken (border cleared to <c>None</c>) must drop out.
/// </summary>
public class ExplorationPresenterBreakableWallTests
{
    private static GameState WallState(string wallDir = "North")
    {
        var gs = new GameState(seed: 1);
        var d = new Dungeon(6, 6, "test");
        for (int x = 0; x < 6; x++)
            for (int y = 0; y < 6; y++)
                d.Tiles[x, y] = new Tile(TileType.Floor);
        // Breakable wall on the north border of (3,2) <-> south border of (3,1).
        d.Tiles[3, 2] = new Tile(TileType.Floor, North: BorderType.BreakableWall);
        d.Tiles[3, 1] = new Tile(TileType.Floor, South: BorderType.BreakableWall);
        gs.CurrentDungeon = d;
        gs.CurrentDungeonType = "test";
        gs.Mode = GameMode.Exploration;
        gs.Player.Position = new Position(3, 2);
        gs.Player.Facing = Direction.North;
        gs.Secrets.Register(new SecretDef("crack", "breakable_wall", X: 3, Y: 2, Wall: wallDir));
        return gs;
    }

    private static JsonElement BreakableWalls(GameState gs)
    {
        var json = JsonSerializer.Serialize(new ExplorationPresenter().Present(gs));
        return JsonDocument.Parse(json).RootElement.GetProperty("BreakableWalls");
    }

    [Fact]
    public void DiscoveredIntactBreakableWall_AppearsInBreakableWallsList()
    {
        var gs = WallState();
        Assert.True(gs.DiscoverSecret("breakable_wall", "crack", "search"));

        var walls = BreakableWalls(gs);
        Assert.Equal(1, walls.GetArrayLength());
        var w = walls[0];
        Assert.Equal("crack", w.GetProperty("id").GetString());
        Assert.Equal(3, w.GetProperty("x").GetInt32());
        Assert.Equal(2, w.GetProperty("y").GetInt32());
        Assert.Equal("North", w.GetProperty("wall").GetString());
    }

    [Fact]
    public void BrokenWall_DropsOutOfBreakableWallsList()
    {
        var gs = WallState();
        Assert.True(gs.DiscoverSecret("breakable_wall", "crack", "search"));
        Assert.True(gs.BreakWall("crack")); // clears the border to None on both sides

        Assert.Equal(0, BreakableWalls(gs).GetArrayLength());
    }

    [Fact]
    public void DetectedButUndiscoveredWall_StaysOutOfBreakableWallsList()
    {
        var gs = WallState();
        gs.DetectSecret("crack", "cartographer"); // detected "?" only — NOT discovered

        // It belongs to the detected set (search affordance) but must NOT be offered as breakable.
        Assert.Equal(0, BreakableWalls(gs).GetArrayLength());

        var json = JsonSerializer.Serialize(new ExplorationPresenter().Present(gs));
        var detected = JsonDocument.Parse(json).RootElement.GetProperty("DetectedSecrets");
        Assert.Equal(1, detected.GetArrayLength());
    }
}
