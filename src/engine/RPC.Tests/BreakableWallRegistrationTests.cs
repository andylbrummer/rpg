using RPC.Engine;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

/// <summary>
/// Segments author BreakableWall borders and the stitcher places them wherever that segment lands,
/// but every mechanism that finds or opens such a wall addresses a positioned SecretDef — and
/// nothing ever created one for a stitched wall. The authored walls were indistinguishable from
/// solid stone, and the Cartographer's detection had nothing to detect in a real dungeon.
/// </summary>
public class BreakableWallRegistrationTests
{
    private static Dungeon FloorGrid(int size = 6)
    {
        var dungeon = new Dungeon(size, size, "test");
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                dungeon.Tiles[x, y] = new Tile(TileType.Floor);
        return dungeon;
    }

    [Fact]
    public void EveryBreakableWallInTheLayout_BecomesADiscoverableSecret()
    {
        var dungeon = FloorGrid();
        dungeon.Tiles[3, 2] = new Tile(TileType.Floor, North: BorderType.BreakableWall);
        dungeon.Tiles[3, 1] = new Tile(TileType.Floor, South: BorderType.BreakableWall);

        var secrets = new SecretRegistry();
        BreakableWallSecrets.RegisterFrom(secrets, dungeon);

        var wall = secrets.Get(BreakableWallSecrets.IdFor(3, 2, Direction.North));
        Assert.NotNull(wall);
        Assert.Equal("breakable_wall", wall!.Type);
        Assert.Equal(3, wall.X);
        Assert.Equal(2, wall.Y);
        Assert.Equal("North", wall.Wall);
    }

    /// <summary>
    /// A wall lies between two tiles and is recorded on both sides. Registering from all four
    /// directions would give one wall two ids, so a player could "discover" the same stone twice
    /// and the second break would find nothing left to open.
    /// </summary>
    [Fact]
    public void AWallSharedByTwoTiles_IsRegisteredOnce()
    {
        var dungeon = FloorGrid();
        dungeon.Tiles[3, 2] = new Tile(TileType.Floor, North: BorderType.BreakableWall);
        dungeon.Tiles[3, 1] = new Tile(TileType.Floor, South: BorderType.BreakableWall);

        var secrets = new SecretRegistry();
        BreakableWallSecrets.RegisterFrom(secrets, dungeon);

        Assert.Single(secrets.All);
    }

    [Fact]
    public void AnAuthoredSecretAtTheSameWall_KeepsPrecedence()
    {
        var dungeon = FloorGrid();
        dungeon.Tiles[3, 2] = new Tile(TileType.Floor, North: BorderType.BreakableWall);
        dungeon.Tiles[3, 1] = new Tile(TileType.Floor, South: BorderType.BreakableWall);

        var secrets = new SecretRegistry();
        secrets.Register(new SecretDef("authored", "breakable_wall", Hint: "Hand written.", X: 3, Y: 2, Wall: "North"));
        BreakableWallSecrets.RegisterFrom(secrets, dungeon);

        Assert.Single(secrets.All);
        Assert.Equal("authored", secrets.All.Single().Id);
    }

    /// <summary>
    /// Wall positions belong to one layout. Carrying the previous dungeon's forward would mark
    /// tiles in this one that hold nothing — a "?" on the automap over solid stone.
    /// </summary>
    [Fact]
    public void EnteringASecondDungeon_DropsTheFirstsWalls()
    {
        var first = FloorGrid();
        first.Tiles[3, 2] = new Tile(TileType.Floor, North: BorderType.BreakableWall);
        first.Tiles[3, 1] = new Tile(TileType.Floor, South: BorderType.BreakableWall);

        var second = FloorGrid();
        second.Tiles[1, 4] = new Tile(TileType.Floor, West: BorderType.BreakableWall);

        var gs = new GameState(seed: 1);
        gs.EnterDungeon(first, "test");
        Assert.NotNull(gs.Secrets.Get(BreakableWallSecrets.IdFor(3, 2, Direction.North)));

        gs.EnterDungeon(second, "test");

        Assert.Null(gs.Secrets.Get(BreakableWallSecrets.IdFor(3, 2, Direction.North)));
        Assert.NotNull(gs.Secrets.Get(BreakableWallSecrets.IdFor(1, 4, Direction.West)));
    }

    /// <summary>
    /// The end-to-end point of all of it: a wall the segments authored can now be searched out and
    /// broken through, which is what made it worth authoring.
    /// </summary>
    [Fact]
    public void AStitchedWall_CanBeSearchedOutAndBroken()
    {
        var dungeon = FloorGrid();
        dungeon.Tiles[3, 2] = new Tile(TileType.Floor, North: BorderType.BreakableWall);
        dungeon.Tiles[3, 1] = new Tile(TileType.Floor, South: BorderType.BreakableWall);

        var gs = new GameState(seed: 1);
        gs.EnterDungeon(dungeon, "test");
        gs.Player.Position = new Position(3, 2);

        var found = gs.SearchForSecrets();

        var id = BreakableWallSecrets.IdFor(3, 2, Direction.North);
        Assert.Contains(id, found);
        Assert.False(dungeon.CanMoveTo(new Position(3, 2), Direction.North));

        Assert.True(gs.BreakWall(id));
        Assert.True(dungeon.CanMoveTo(new Position(3, 2), Direction.North));
    }
}
