namespace RPC.Engine.Combat;

/// <summary>
/// Bloom creatures shift their stats mid-combat. Each round a bloom enemy has a chance to mutate,
/// unless suppressed by a player counter — Cauterist's Scorcher (the "burned" status) or the
/// Heretic's Bloom Touch (the "bloom_suppressed" status). Mutation is surfaced via a combat log
/// entry (visual cue).
/// </summary>
public static class BloomMutationSystem
{
    public const int DefaultMutationChancePercent = 10;

    public record Mutation(string Name, Func<Combatant, Combatant> Apply);

    private static readonly Mutation[] Mutations =
    {
        new("engorged", c => c with { Speed = c.Speed + 2 }),
        new("spined", c => c with { Power = c.Power + 3 }),
        new("volatile", c => c with { MaxHp = c.MaxHp + 5, Hp = c.Hp + 5 }),
    };

    public static bool IsBloom(Combatant c) => c.StatusEffects.Any(s => s.Type == "bloom");

    /// <summary>True when a player counter is active on the creature: Scorcher (burned) or Bloom Touch.</summary>
    public static bool IsSuppressed(Combatant c) =>
        c.StatusEffects.Any(s => s.Type is "burned" or "bloom_suppressed");

    /// <summary>
    /// Roll for a mid-combat mutation. Returns the (possibly mutated) combatant and the mutation
    /// name, or null when nothing changed (not bloom, suppressed, dead, or roll missed).
    /// </summary>
    public static (Combatant Result, string? Mutation) TryMutate(
        Combatant c, GameRandom rng, int chancePercent = DefaultMutationChancePercent)
    {
        if (!c.IsAlive || !IsBloom(c) || IsSuppressed(c)) return (c, null);
        if (rng.Roll(1, 100) > chancePercent) return (c, null);

        var mutation = Mutations[rng.Next(Mutations.Length)];
        return (mutation.Apply(c), mutation.Name);
    }
}
