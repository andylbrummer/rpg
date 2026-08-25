using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using Xunit;

namespace RPC.Tests;

public class DungeonPathClassifierTests
{
    // Layout (x →, y ↓), all Floor, doors between adjacent room tiles:
    //   (0,0)=StairsUp room0   (1,0)=room1   (2,0)=boss room2
    //                                  (1,1)=room3 dead-end (branch off room1)
    private static Dungeon BuildLine()
    {
        var d = new Dungeon(3, 2, "test");
        void Floor(int x, int y, int roomId) =>
            d.Tiles[x, y] = new Tile(x == 0 && y == 0 ? TileType.StairsUp : TileType.Floor, RoomId: roomId);

        Floor(0, 0, 0);
        Floor(1, 0, 1);
        Floor(2, 0, 2);
        Floor(1, 1, 3);

        // Open doors both directions between connected tiles.
        d.Tiles[0, 0] = d.Tiles[0, 0] with { East = BorderType.Door };
        d.Tiles[1, 0] = d.Tiles[1, 0] with { West = BorderType.Door, East = BorderType.Door, South = BorderType.Door };
        d.Tiles[2, 0] = d.Tiles[2, 0] with { West = BorderType.Door };
        d.Tiles[1, 1] = d.Tiles[1, 1] with { North = BorderType.Door };

        // Boss tile tagged like TagBossTile does.
        d.Tiles[2, 0] = d.Tiles[2, 0] with { EncounterId = "boss-1" };

        d.Rooms.Add(new RoomInfo { Id = 0, Min = new Position(0, 0), Max = new Position(0, 0) });
        d.Rooms.Add(new RoomInfo { Id = 1, Min = new Position(1, 0), Max = new Position(1, 0) });
        d.Rooms.Add(new RoomInfo { Id = 2, Min = new Position(2, 0), Max = new Position(2, 0) });
        d.Rooms.Add(new RoomInfo { Id = 3, Min = new Position(1, 1), Max = new Position(1, 1) });
        return d;
    }

    [Fact]
    public void Classify_assigns_entrance_boss_critical_and_deadend()
    {
        var roles = new DungeonPathClassifier().Classify(BuildLine());

        Assert.Equal(RoomRole.Entrance, roles[0]);
        Assert.Equal(RoomRole.Critical, roles[1]); // on path entrance→boss
        Assert.Equal(RoomRole.Boss, roles[2]);
        Assert.Equal(RoomRole.DeadEnd, roles[3]); // single connection, off path
    }

    [Fact]
    public void Classify_is_deterministic()
    {
        var a = new DungeonPathClassifier().Classify(BuildLine());
        var b = new DungeonPathClassifier().Classify(BuildLine());
        Assert.Equal(a, b);
    }
}
