namespace RPC.Engine.Combat;

public static class RangeBands
{
    /// <summary>
    /// Range band between two combatants based on their row positions.
    /// 0 = melee, 1 = close, 2 = far, 3 = extreme
    /// </summary>
    public static int Distance(Combatant a, Combatant b)
    {
        // Same side: distance is row difference (0 or 1)
        // Opposite side: front-front = 1, front-back = 2, back-back = 3
        if (a.IsPlayer == b.IsPlayer)
            return Math.Abs(a.Row - b.Row);

        // Opposite sides
        return a.Row + b.Row + 1;
    }

    /// <summary>
    /// Check if a target is within range of an attack/ability.
    /// Range string formats: "melee", "close", "far", "any", "self", or numeric "0", "1", "2", "3"
    /// </summary>
    public static bool InRange(Combatant actor, Combatant target, string? range)
    {
        if (string.IsNullOrEmpty(range) || range == "any")
            return true;

        if (range == "self")
            return actor.Id == target.Id;

        var maxRange = ParseRange(range);
        return Distance(actor, target) <= maxRange;
    }

    /// <summary>
    /// Get all valid targets for an action from the actor's perspective.
    /// </summary>
    public static Combatant[] ValidTargets(Combatant actor, Combatant[] all, string? range, string? targetFilter)
    {
        var candidates = targetFilter switch
        {
            "ally" => all.Where(c => c.IsPlayer == actor.IsPlayer && c.IsAlive && c.Id != actor.Id),
            "enemies" => all.Where(c => c.IsPlayer != actor.IsPlayer && c.IsAlive),
            "all" => all.Where(c => c.IsAlive),
            "self" => all.Where(c => c.Id == actor.Id),
            _ => all.Where(c => c.IsPlayer != actor.IsPlayer && c.IsAlive) // default: enemies
        };

        return candidates.Where(t => InRange(actor, t, range)).ToArray();
    }

    private static int ParseRange(string range)
    {
        return range.ToLowerInvariant() switch
        {
            "melee" => 0,
            "close" => 1,
            "far" => 2,
            "extreme" => 3,
            _ => int.TryParse(range, out var n) ? n : 3
        };
    }
}
