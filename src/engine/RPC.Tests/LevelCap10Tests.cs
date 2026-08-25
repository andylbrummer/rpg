using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Content;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Town;

namespace RPC.Tests;

public class LevelCap10Tests : IDisposable
{
    private readonly string _testSavePath;

    public LevelCap10Tests()
    {
        _testSavePath = Path.Combine(Path.GetTempPath(), $"test_levelcap_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testSavePath))
            File.Delete(_testSavePath);
    }

    [Theory]
    [InlineData("bonewarden")]
    [InlineData("cauterist")]
    [InlineData("fieldwright")]
    [InlineData("hollow")]
    [InlineData("inkblood")]
    [InlineData("stillblade")]
    [InlineData("ashmouth")]
    [InlineData("marcher")]
    public void ClassDef_HasTenLevelEntries(string classId)
    {
        var registry = new ClassRegistry();
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "content", "classes"));
        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
            {
                var json = File.ReadAllText(file);
                var id = Path.GetFileNameWithoutExtension(file);
                registry.LoadFromJson(id, json);
            }
        }

        var classDef = registry.Get(classId);
        Assert.NotNull(classDef);
        Assert.Equal(10, classDef.LevelTable.Length);
        Assert.Contains(classDef.LevelTable, l => l.Level == 10);
    }

    [Fact]
    public void LevelingSystem_XpForNextLevel_Level10_IsMaxValue()
    {
        Assert.Equal(int.MaxValue, LevelingSystem.XpForNextLevel(10));
    }

    [Fact]
    public void LevelingSystem_CanReachLevel10()
    {
        var classDef = new ClassDef(
            "test", "Test", "Test class",
            new BaseStats(4, 4, 4, 4, 4),
            Array.Empty<AbilityDef>(),
            Enumerable.Range(1, 10).Select(l => new LevelTableEntry(l, 3, new BaseStats(0, 0, 1, 0, 0), Array.Empty<string>())).ToArray());

        var character = new CharacterState(
            Guid.NewGuid(), "Hero", "test", 1, 0,
            new BaseStats(4, 4, 4, 4, 4), 20, Equipment.Empty,
            Array.Empty<string>(), 0);

        var updated = LevelingSystem.CheckAndApplyLevelUps(
            character with { Xp = 1600 }, classDef);

        Assert.Equal(10, updated.Level);
    }

    [Fact]
    public void GameState_ExploreAroundPlayer_GrantsXp()
    {
        var gs = new GameState(seed: 42);
        var member = gs.Party.Members[0];
        var initialXp = member.Xp;

        // Enter a dungeon to enable exploration
        var dungeon = new RPC.Engine.Models.Dungeons.Dungeon(20, 20, "test");
        // Set some floor tiles around the player
        for (int x = 8; x <= 12; x++)
        {
            for (int y = 8; y <= 12; y++)
            {
                dungeon.Tiles[x, y] = new Tile(TileType.Floor);
            }
        }
        gs.CurrentDungeon = dungeon;
        gs.Mode = GameMode.Exploration;
        gs.Player = new Player(new Position(10, 10), Direction.North);

        // Move to explore
        gs.ExploreAroundPlayer();

        var updatedXp = gs.Party.Members[0].Xp;
        Assert.True(updatedXp > initialXp, "Exploration should grant XP");
    }

    [Fact]
    public void LevelingSystem_XpThresholds_Level3_Is120()
    {
        Assert.Equal(120, LevelingSystem.XpForNextLevel(2));
    }

    [Fact]
    public void LevelingSystem_XpThresholds_Level6_Is500()
    {
        Assert.Equal(500, LevelingSystem.XpForNextLevel(5));
    }

    [Fact]
    public void LevelingSystem_XpThresholds_Level10_Is1600()
    {
        Assert.Equal(1600, LevelingSystem.XpForNextLevel(9));
    }

    [Fact]
    public void GameState_CompleteMission_GrantsXp()
    {
        var gs = new GameState(seed: 42);
        var mission = new MissionOffer("m1", "Test Mission", "Test", 1, new[] { "50g" }, 5, "bureau", MissionType.Side);
        gs.Town.AvailableMissions.Add(mission);
        gs.AcceptMission("m1");

        var member = gs.Party.Members[0];
        var initialXp = member.Xp;

        gs.CompleteMission("m1");

        var updatedXp = gs.Party.Members[0].Xp;
        Assert.Equal(initialXp + 50, updatedXp);
    }

    [Fact]
    public void GameState_CompleteMainMission_GrantsMoreXp()
    {
        var gs = new GameState(seed: 42);
        var mission = new MissionOffer("m2", "Main Mission", "Test", 1, new[] { "100g" }, 10, "bureau", MissionType.Main);
        gs.Town.AvailableMissions.Add(mission);
        gs.AcceptMission("m2");

        var member = gs.Party.Members[0];
        var initialXp = member.Xp;

        gs.CompleteMission("m2");

        var updatedXp = gs.Party.Members[0].Xp;
        Assert.Equal(initialXp + 100, updatedXp);
    }
}
