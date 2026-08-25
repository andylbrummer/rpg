using RPC.Engine.Character;

namespace RPC.Tests;

public class LevelingSystemTests
{
    private static ClassDef MakeClass()
        => new("test", "TestClass", "Test", new BaseStats(4, 4, 4, 4, 4),
            Array.Empty<AbilityDef>(),
            new[]
            {
                new LevelTableEntry(1, 0, new BaseStats(0, 0, 0, 0, 0), Array.Empty<string>()),
                new LevelTableEntry(2, 4, new BaseStats(1, 0, 0, 0, 0), new[] { "new_skill" }),
                new LevelTableEntry(3, 3, new BaseStats(0, 1, 0, 0, 0), Array.Empty<string>()),
                new LevelTableEntry(4, 5, new BaseStats(0, 0, 1, 0, 0), new[] { "ult" })
            });

    private static CharacterState MakeChar(int level = 1, int xp = 0)
        => new(new Guid("11111111111111111111111111111111"), "Hero", "test", level, xp,
            new BaseStats(4, 4, 4, 4, 4), 20, Equipment.Empty,
            new[] { "base_skill" }, 0);

    [Fact]
    public void Leveling_XpForNextLevel_ReturnsThreshold()
    {
        Assert.Equal(50, LevelingSystem.XpForNextLevel(1));
        Assert.Equal(120, LevelingSystem.XpForNextLevel(2));
    }

    [Fact]
    public void Leveling_CanLevelUp_WhenXpReachesThreshold()
    {
        var c = MakeChar(xp: 49);
        Assert.False(LevelingSystem.CanLevelUp(c));

        var c2 = MakeChar(xp: 50);
        Assert.True(LevelingSystem.CanLevelUp(c2));
    }

    [Fact]
    public void Leveling_ApplyLevelUp_IncreasesStats()
    {
        var c = MakeChar(level: 1, xp: 50);
        var classDef = MakeClass();

        var result = LevelingSystem.ApplyLevelUp(c, classDef);

        Assert.Equal(2, result.Level);
        Assert.Equal(5, result.BaseStats.Strength);
        Assert.Contains("new_skill", result.KnownAbilities);
    }

    [Fact]
    public void Leveling_CheckAndApply_MultipleLevelUps()
    {
        var c = MakeChar(level: 1, xp: 300);
        var classDef = MakeClass();

        var result = LevelingSystem.CheckAndApplyLevelUps(c, classDef);

        Assert.True(result.Level >= 3);
        Assert.Contains("new_skill", result.KnownAbilities);
    }

    [Fact]
    public void Leveling_ApplyLevelUp_HpGain()
    {
        var c = MakeChar(level: 1, xp: 50);
        var classDef = MakeClass();

        var result = LevelingSystem.ApplyLevelUp(c, classDef);

        // CurrentHp should increase by hpGain (4)
        Assert.Equal(24, result.CurrentHp);
    }
}
