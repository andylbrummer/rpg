using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using Xunit;

namespace RPC.Tests;

public class DungeonLootPlacerTests
{
    private static Dungeon BuildRooms()
    {
        // room0 entrance, room1 critical, room2 boss, room3 dead-end (4 separate floor tiles).
        var d = new Dungeon(4, 1, "t");
        d.Tiles[0, 0] = new Tile(TileType.StairsUp, East: BorderType.Door, RoomId: 0);
        d.Tiles[1, 0] = new Tile(TileType.Floor, West: BorderType.Door, East: BorderType.Door, RoomId: 1);
        d.Tiles[2, 0] = new Tile(TileType.Floor, West: BorderType.Door, East: BorderType.Door, RoomId: 2, EncounterId: "boss-1");
        d.Tiles[3, 0] = new Tile(TileType.Floor, West: BorderType.Door, RoomId: 3);
        for (int i = 0; i < 4; i++) d.Rooms.Add(new RoomInfo { Id = i, Min = new Position(i, 0), Max = new Position(i, 0) });
        return d;
    }

    private static DungeonLootTableRegistry Reg()
    {
        var r = new DungeonLootTableRegistry();
        r.LoadFromJson("t", """{ "id":"t", "entries":[ {"itemId":"gold","weight":1} ] }""");
        return r;
    }

    [Fact]
    public void Place_is_deterministic_for_same_seed()
    {
        var roles = new DungeonPathClassifier().Classify(BuildRooms());
        var d1 = BuildRooms(); new DungeonLootPlacer().Place(d1, roles, Reg().Get("t")!, 7);
        var d2 = BuildRooms(); new DungeonLootPlacer().Place(d2, roles, Reg().Get("t")!, 7);

        for (int x = 0; x < 4; x++)
            Assert.Equal(d1.Tiles[x, 0].LootId, d2.Tiles[x, 0].LootId);
    }

    [Fact]
    public void Place_never_puts_loot_on_entrance_or_boss()
    {
        var roles = new DungeonPathClassifier().Classify(BuildRooms());
        // try many seeds; entrance (x0) and boss (x2) must always stay null
        for (int seed = 0; seed < 50; seed++)
        {
            var d = BuildRooms();
            new DungeonLootPlacer().Place(d, roles, Reg().Get("t")!, seed);
            Assert.Null(d.Tiles[0, 0].LootId);
            Assert.Null(d.Tiles[2, 0].LootId);
        }
    }

    [Fact]
    public void DeadEnds_receive_loot_more_often_than_critical_rooms()
    {
        var roles = new DungeonPathClassifier().Classify(BuildRooms());
        int deadEndHits = 0, criticalHits = 0;
        for (int seed = 0; seed < 200; seed++)
        {
            var d = BuildRooms();
            new DungeonLootPlacer().Place(d, roles, Reg().Get("t")!, seed);
            if (d.Tiles[3, 0].LootId != null) deadEndHits++;   // dead-end
            if (d.Tiles[1, 0].LootId != null) criticalHits++;  // critical
        }
        Assert.True(deadEndHits > criticalHits,
            $"dead-end {deadEndHits} should exceed critical {criticalHits}");
    }
}
