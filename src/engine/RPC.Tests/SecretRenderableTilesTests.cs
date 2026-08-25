using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

public class SecretRenderableTilesTests
{
    [Fact]
    public void IllusoryFloor_IsWalkable()
    {
        Assert.True(new Tile(TileType.IllusoryFloor).IsWalkable);
    }

    [Fact]
    public void ConcealedCompartment_BlocksMovement()
    {
        var d = new Dungeon(3, 3, "test");
        d.Tiles[1, 1] = new Tile(TileType.Floor, North: BorderType.ConcealedCompartment);
        d.Tiles[1, 0] = new Tile(TileType.Floor);

        Assert.False(d.CanMoveTo(new Position(1, 1), Direction.North));
    }

    [Fact]
    public void IllusoryFloor_IsTraversable()
    {
        var d = new Dungeon(3, 3, "test");
        d.Tiles[1, 1] = new Tile(TileType.Floor);
        d.Tiles[1, 0] = new Tile(TileType.IllusoryFloor); // pit disguised as floor — steppable

        Assert.True(d.CanMoveTo(new Position(1, 1), Direction.North));
    }

    [Fact]
    public void NewSecretTypes_SerializeByName()
    {
        // The presenter ships these to the client via ToString(); the names must match the
        // client BorderType/Tile.type unions exactly.
        Assert.Equal("IllusoryFloor", TileType.IllusoryFloor.ToString());
        Assert.Equal("ConcealedCompartment", BorderType.ConcealedCompartment.ToString());
    }
}
