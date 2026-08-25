using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

public class DungeonPacerTests
{
    // A straight east-west corridor: tile x has BFS depth x. Entrance up-stairs at x=0,
    // boss down-stairs at the far end unless overridden.
    private static Dungeon Corridor(int n, int? stairsDownAt = null)
    {
        var d = new Dungeon(n, 1, "pacing-test");
        for (int x = 0; x < n; x++)
            d.Tiles[x, 0] = new Tile(TileType.Floor);
        d.Tiles[0, 0] = new Tile(TileType.StairsUp);
        var downAt = stairsDownAt ?? n - 1;
        d.Tiles[downAt, 0] = new Tile(TileType.StairsDown);
        return d;
    }

    [Fact]
    public void Plan_SpacesEncountersByConfiguredDepthGap()
    {
        var plan = new DungeonPacer().Plan(Corridor(12), new DungeonPacer.PacingConfig(EncounterSpacing: 3));

        Assert.NotEmpty(plan.Encounters);
        var depths = plan.Encounters.Select(e => e.Depth).ToList();
        for (int i = 1; i < depths.Count; i++)
            Assert.True(depths[i] - depths[i - 1] >= 3, $"gap {depths[i] - depths[i - 1]} < 3");
    }

    [Fact]
    public void Plan_DangerRatingRampsWithDepth()
    {
        var plan = new DungeonPacer().Plan(Corridor(16), new DungeonPacer.PacingConfig(MinDanger: 1, MaxDanger: 10));

        var ordered = plan.Encounters.OrderBy(e => e.Depth).ToList();
        Assert.True(ordered.Count >= 2);
        for (int i = 1; i < ordered.Count; i++)
            Assert.True(ordered[i].DangerRating >= ordered[i - 1].DangerRating, "danger must not decrease with depth");

        Assert.True(ordered.First().DangerRating <= ordered.Last().DangerRating);
        // Deepest encounter should approach the configured ceiling.
        Assert.True(ordered.Last().DangerRating >= 7, $"deepest danger {ordered.Last().DangerRating} too low");
    }

    [Fact]
    public void Plan_BossOnDeepestTile_DistanceMet()
    {
        var plan = new DungeonPacer().Plan(Corridor(13));

        Assert.NotNull(plan.Boss);
        Assert.Equal(new Position(12, 0), plan.Boss);
        Assert.Equal(12, plan.MaxDepth);
        Assert.Equal(12, plan.BossDepth);
        Assert.True(plan.BossDistanceMet);
    }

    [Fact]
    public void Plan_ShallowBoss_DistanceNotMet()
    {
        // Down-stairs placed near the entrance while the corridor runs much deeper.
        var plan = new DungeonPacer().Plan(Corridor(13, stairsDownAt: 2));

        Assert.Equal(new Position(2, 0), plan.Boss);
        Assert.Equal(2, plan.BossDepth);
        Assert.Equal(12, plan.MaxDepth);
        Assert.False(plan.BossDistanceMet);
    }

    [Fact]
    public void Plan_PlacesBreatherRoomsBetweenEncounters()
    {
        var plan = new DungeonPacer().Plan(Corridor(16), new DungeonPacer.PacingConfig(EncounterSpacing: 3));

        Assert.NotEmpty(plan.BreatherRooms);
        var encounterTiles = plan.Encounters.Select(e => e.Position).ToHashSet();
        foreach (var b in plan.BreatherRooms)
            Assert.DoesNotContain(b, encounterTiles);
    }

    [Fact]
    public void Apply_TagsEncounterTilesAndLeavesEntranceAndBossClear()
    {
        var dungeon = Corridor(14);
        var pacer = new DungeonPacer();
        var plan = pacer.Plan(dungeon, new DungeonPacer.PacingConfig(EncounterSpacing: 2));

        var tables = new EncounterTableRegistry();
        tables.LoadFromJson("dungeon", """
        {
          "id": "dungeon",
          "name": "Test Table",
          "entries": [
            { "id": "easy", "weight": 1, "enemies": [], "dangerRating": 1 },
            { "id": "hard", "weight": 1, "enemies": [], "dangerRating": 9 }
          ]
        }
        """);

        pacer.Apply(dungeon, plan, tables, "dungeon", new GameRandom(7));

        int tagged = 0;
        for (int x = 0; x < dungeon.Width; x++)
            if (dungeon.Tiles[x, 0].EncounterId is not null)
                tagged++;

        Assert.Equal(plan.Encounters.Count, tagged);
        Assert.Null(dungeon.Tiles[0, 0].EncounterId);   // entrance
        Assert.Null(dungeon.Tiles[13, 0].EncounterId);  // boss
    }
}
