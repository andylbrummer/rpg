using RPC.Engine.Character;

namespace RPC.Tests;

public class CharacterTests
{
    [Fact]
    public void CharacterState_IsValueType()
    {
        var cs = new CharacterState(
            Guid.NewGuid(), "Test", "bonewarden", 1, 0,
            new BaseStats(4, 4, 5, 4, 4),
            17, Equipment.Empty,
            Array.Empty<string>(), 0);

        Assert.True(typeof(CharacterState).IsValueType);
    }

    [Fact]
    public void Bonewarden_Level1_HpApprox18()
    {
        var bonewarden = new CharacterState(
            Guid.NewGuid(), "Kael", "bonewarden", 1, 0,
            new BaseStats(4, 3, 5, 4, 4),
            17, Equipment.Empty,
            new[] { "bone_spear", "tithe_touch" }, 0);

        Assert.Equal(17, bonewarden.EffectiveStats.MaxHp); // 5*3 + 1*2 = 17
        Assert.InRange(bonewarden.EffectiveStats.MaxHp, 15, 20);
    }

    [Fact]
    public void EffectiveStats_AccuracyAndEvade_FromDexterity()
    {
        var character = new CharacterState(
            Guid.NewGuid(), "Test", "stillblade", 1, 0,
            new BaseStats(5, 6, 4, 3, 4),
            14, Equipment.Empty,
            Array.Empty<string>(), 0);

        Assert.Equal(7, character.EffectiveStats.Accuracy); // 6 + 1
        Assert.Equal(3, character.EffectiveStats.Evade);   // 6/2 + 1/2 = 3 + 0
        Assert.Equal(6, character.EffectiveStats.Speed);   // 6 + 0
        Assert.Equal(5, character.EffectiveStats.Power);   // 5
    }

    [Fact]
    public void CharacterState_IsAlive_WhenHpPositive()
    {
        var alive = new CharacterState(
            Guid.NewGuid(), "Alive", "hollow", 1, 0,
            new BaseStats(3, 5, 3, 3, 3),
            1, Equipment.Empty,
            Array.Empty<string>(), 0);

        var dead = alive with { CurrentHp = 0 };

        Assert.True(alive.IsAlive);
        Assert.False(dead.IsAlive);
    }
}
