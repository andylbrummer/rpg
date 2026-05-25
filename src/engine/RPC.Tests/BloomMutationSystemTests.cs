using System.Collections.Generic;
using RPC.Engine.Combat;

namespace RPC.Tests;

public class BloomMutationSystemTests
{
    private static Combatant Bloom(params StatusEffect[] extra)
    {
        var effects = new List<StatusEffect> { new StatusEffect("bloom", 999, null) };
        effects.AddRange(extra);
        return new Combatant(System.Guid.NewGuid(), "Bloomling", false, 20, 20, 5, 0, effects, 4);
    }

    private static Combatant NonBloom()
        => new Combatant(System.Guid.NewGuid(), "Rat", false, 10, 10, 5, 0, new List<StatusEffect>(), 2);

    [Fact]
    public void IsBloom_DetectsMarker()
    {
        Assert.True(BloomMutationSystem.IsBloom(Bloom()));
        Assert.False(BloomMutationSystem.IsBloom(NonBloom()));
    }

    [Fact]
    public void IsSuppressed_ByScorcherOrBloomTouch()
    {
        Assert.True(BloomMutationSystem.IsSuppressed(Bloom(new StatusEffect("burned", 3, null))));
        Assert.True(BloomMutationSystem.IsSuppressed(Bloom(new StatusEffect("bloom_suppressed", 3, null))));
        Assert.False(BloomMutationSystem.IsSuppressed(Bloom()));
    }

    [Fact]
    public void TryMutate_NonBloom_DoesNotMutate()
    {
        var (result, mutation) = BloomMutationSystem.TryMutate(NonBloom(), new GameRandom(1), chancePercent: 100);
        Assert.Null(mutation);
    }

    [Fact]
    public void TryMutate_Suppressed_DoesNotMutate()
    {
        var (_, mutation) = BloomMutationSystem.TryMutate(
            Bloom(new StatusEffect("burned", 3, null)), new GameRandom(1), chancePercent: 100);
        Assert.Null(mutation);
    }

    [Fact]
    public void TryMutate_Dead_DoesNotMutate()
    {
        var dead = Bloom() with { Hp = 0 };
        var (_, mutation) = BloomMutationSystem.TryMutate(dead, new GameRandom(1), chancePercent: 100);
        Assert.Null(mutation);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    public void TryMutate_GuaranteedChance_AppliesAStatShift(int seed)
    {
        var c = Bloom();
        var (result, mutation) = BloomMutationSystem.TryMutate(c, new GameRandom(seed), chancePercent: 100);

        Assert.NotNull(mutation);
        // Every mutation increases exactly one of speed/power/maxHp.
        Assert.True(result.Speed + result.Power + result.MaxHp > c.Speed + c.Power + c.MaxHp);
    }

    [Fact]
    public void TryMutate_ZeroChance_NeverMutates()
    {
        var (_, mutation) = BloomMutationSystem.TryMutate(Bloom(), new GameRandom(1), chancePercent: 0);
        Assert.Null(mutation);
    }
}
