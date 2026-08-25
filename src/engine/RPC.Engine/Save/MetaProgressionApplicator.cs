namespace RPC.Engine.Save;

/// <summary>
/// Applies accumulated <see cref="MetaProgression"/> to a fresh campaign so prior runs leave a mark
/// on the next one: factions remember you (starting reputation), dungeons you already conquered stay
/// unlocked, and the Reach grows more dangerous the deeper into the multi-run arc you are.
/// Pure — depends only on the supplied state and meta, performs no IO.
/// </summary>
public static class MetaProgressionApplicator
{
    /// <summary>Fraction of accumulated faction power that carries into a new run as starting reputation.</summary>
    public const double ReputationCarryRate = 0.1;

    /// <summary>Cap on the starting reputation swing from meta, so you never begin allied/hostile outright.</summary>
    public const int MaxStartingReputation = 25;

    /// <summary>Added starting Heat per prior completed run (rising baseline difficulty).</summary>
    public const int HeatPerRun = 3;

    /// <summary>Cap on the meta-derived starting Heat.</summary>
    public const int MaxStartingHeat = 30;

    public static void Apply(GameState state, MetaProgression meta)
    {
        if (meta is null) return;

        // 1. Faction starting reputation: a fraction of accumulated power, clamped so it only biases
        //    the opening attitude rather than dictating it. Propagation is off here — this is a flat
        //    starting offset, not an in-run reputation event.
        foreach (var (faction, power) in meta.FactionPower)
        {
            if (string.IsNullOrEmpty(faction)) continue;
            int delta = Math.Clamp(
                (int)Math.Round(power * ReputationCarryRate),
                -MaxStartingReputation,
                MaxStartingReputation);
            if (delta != 0)
                state.Reputation.ApplyDelta(faction, delta, "meta_progression", propagate: false);
        }

        // 2. Unlock state: dungeons conquered in any prior run start this run already unlocked.
        foreach (var dungeon in meta.ConqueredDungeons)
        {
            if (!string.IsNullOrEmpty(dungeon))
                state.Campaign.UnlockedDungeons.Add(dungeon);
        }

        // 3. Difficulty: each completed run raises the starting Heat baseline, up to a cap.
        if (meta.RunsCompleted > 0)
        {
            int heatBonus = Math.Min(meta.RunsCompleted * HeatPerRun, MaxStartingHeat);
            if (heatBonus > 0)
                state.Heat.Add(heatBonus);
        }
    }
}
