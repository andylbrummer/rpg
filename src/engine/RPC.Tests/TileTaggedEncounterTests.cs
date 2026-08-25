using RPC.Engine;
using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

public class TileTaggedEncounterTests
{
    private const string TestTableJson = """
    {
      "id": "test_table",
      "name": "Test Table",
      "entries": [
        { "id": "tagged-enc", "weight": 1, "enemies": [{ "enemyId": "rat", "count": 3 }] },
        { "id": "empty-enc", "weight": 1, "enemies": [] },
        { "id": "wandering", "weight": 1, "enemies": [{ "enemyId": "rat", "count": 1 }] }
      ]
    }
    """;

    private static EncounterTableRegistry CreateRegistry()
    {
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("test_table", TestTableJson);
        return registry;
    }

    [Fact]
    public void SteppingOnTaggedTile_TriggersExactEncounter()
    {
        var registry = CreateRegistry();
        var gs = new GameState(seed: 42, encounterTables: registry);
        var dungeon = new Dungeon(3, 3, "test");
        dungeon.Tiles[1, 1] = new Tile(TileType.Floor);
        dungeon.Tiles[1, 0] = new Tile(TileType.Floor, EncounterId: "tagged-enc");
        gs.EnterDungeon(dungeon, "test");
        gs.Player.Position = new Position(1, 1);
        gs.Player.Facing = Direction.North;

        gs.TryMoveForward();

        Assert.Equal(GameMode.Combat, gs.Mode);
        Assert.NotNull(gs.Combat);
        var ratCount = gs.Combat.Combatants.Count(c => !c.IsPlayer && c.Name.StartsWith("rat"));
        Assert.Equal(3, ratCount);
    }

    [Fact]
    public void UntaggedTile_FallsBackToWanderingEncounter()
    {
        var registry = CreateRegistry();
        var gs = new GameState(seed: 1, encounterTables: registry);
        var dungeon = new Dungeon(5, 5, "test");
        for (int x = 0; x < 5; x++)
            for (int y = 0; y < 5; y++)
                dungeon.Tiles[x, y] = new Tile(TileType.Floor);
        dungeon.WanderingTableId = "test_table";
        gs.EnterDungeon(dungeon, "test");
        gs.Player.Position = new Position(2, 2);

        var directions = new[] { Direction.North, Direction.East, Direction.South, Direction.West };
        bool enteredCombat = false;
        for (int i = 0; i < 20; i++)
        {
            gs.Player.Facing = directions[i % 4];
            gs.TryMoveForward();
            if (gs.Mode == GameMode.Combat)
            {
                enteredCombat = true;
                break;
            }
        }

        Assert.True(enteredCombat);
    }

    [Fact]
    public void ResolvingTaggedEncounter_ClearsTileTag()
    {
        var registry = CreateRegistry();
        var gs = new GameState(seed: 42, encounterTables: registry);
        var dungeon = new Dungeon(3, 3, "test");
        dungeon.Tiles[1, 1] = new Tile(TileType.Floor);
        dungeon.Tiles[1, 0] = new Tile(TileType.Floor, EncounterId: "empty-enc");
        gs.EnterDungeon(dungeon, "test");
        gs.Player.Position = new Position(1, 1);
        gs.Player.Facing = Direction.North;

        gs.TryMoveForward();

        Assert.Equal(GameMode.Exploration, gs.Mode);
        Assert.Null(dungeon.Tiles[1, 0].EncounterId);
    }

    [Fact]
    public void FleeingTaggedEncounter_LeavesTagPending()
    {
        var registry = CreateRegistry();
        var gs = new GameState(seed: 42, encounterTables: registry);
        var dungeon = new Dungeon(3, 3, "test");
        dungeon.Tiles[1, 1] = new Tile(TileType.Floor);
        dungeon.Tiles[1, 0] = new Tile(TileType.Floor, EncounterId: "tagged-enc");
        gs.EnterDungeon(dungeon, "test");
        gs.Player.Position = new Position(1, 1);
        gs.Player.Facing = Direction.North;

        gs.TryMoveForward();
        Assert.Equal(GameMode.Combat, gs.Mode);

        gs.FleeCombat();
        Assert.Equal(GameMode.Exploration, gs.Mode);
        Assert.Equal("tagged-enc", dungeon.Tiles[1, 0].EncounterId);
    }
}
