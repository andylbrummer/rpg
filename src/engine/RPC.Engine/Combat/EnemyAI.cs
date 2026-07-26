namespace RPC.Engine.Combat;

/// <summary>
/// Decides what a non-player combatant does on its turn: who it goes for, and with what.
///
/// <para>
/// This is a pure decision — state in, a <see cref="CombatAction"/> out — with no part in
/// resolving that action. Keeping it apart from <see cref="CombatEngine"/> is what lets a
/// behaviour be reasoned about and tested as "given this board, a pack hunter picks that target",
/// without standing up a round of combat around it.
/// </para>
/// </summary>
internal static class EnemyAI
{
    /// <summary>
    /// Chooses this actor's action. Behaviour names come from content (<c>aiBehavior</c> on an
    /// enemy definition); an unrecognised one falls through to the default attack profile rather
    /// than failing, so a content typo costs a tactic, not the fight.
    /// </summary>
    public static CombatAction Decide(CombatState state, Combatant actor, GameRandom rng)
    {
        var targets = state.Combatants.Where(c => c.IsPlayer && c.IsAlive).ToArray();
        if (targets.Length == 0)
            return new CombatAction(actor.Id, ActionType.Wait, null, null, null);

        var behavior = actor.AiBehavior?.ToLowerInvariant() ?? "";

        // Faction soldier retreat check
        if (behavior == "soldier_tactical" && ShouldRetreat(state))
        {
            return new CombatAction(actor.Id, ActionType.Flee, null, null, null);
        }

        var target = SelectTarget(state, actor, targets, behavior, rng);
        var (actionType, abilityId) = SelectAction(actor, behavior, state);

        return new CombatAction(actor.Id, actionType, target.Id, abilityId, null);
    }

    private static bool ShouldRetreat(CombatState state)
    {
        var allies = state.Combatants.Where(c => !c.IsPlayer && c.IsAlive).ToArray();
        var enemies = state.Combatants.Where(c => c.IsPlayer && c.IsAlive).ToArray();

        if (enemies.Length == 0) return false;

        var allyHp = allies.Sum(a => a.Hp);
        var enemyHp = enemies.Sum(e => e.Hp);

        return allyHp < enemyHp * 0.5;
    }

    private static Combatant SelectTarget(CombatState state, Combatant actor, Combatant[] targets, string behavior, GameRandom rng)
    {
        // Reach-through: Unaccounted can target back row directly
        if (actor.IsUnaccounted)
        {
            var backRowTargets = targets.Where(t => t.Row == 1).ToArray();
            if (backRowTargets.Length > 0)
            {
                // Animator summons absorb back-row targeting
                var summon = state.Combatants.FirstOrDefault(c => c.IsSummoned && c.IsAlive);
                if (summon.IsAlive)
                {
                    return summon;
                }
                return backRowTargets[rng.Next(backRowTargets.Length)];
            }
        }

        return behavior switch
        {
            "aggressive" or "zealot_aggressive" => targets.OrderBy(t => t.Hp).ThenBy(t => t.Id).First(),
            "pack_hunter" => SelectPackHunterTarget(state, targets),
            "ranged_priority" => targets.OrderByDescending(t => t.Row).ThenBy(t => t.Id).First(),
            "defensive" => targets.OrderByDescending(t => t.Power).ThenByDescending(t => t.MaxHp).ThenBy(t => t.Id).First(),
            "soldier_tactical" => targets.OrderBy(t => t.Hp).ThenBy(t => t.Id).First(),
            _ => DefaultTarget(actor, targets, rng)
        };
    }

    private static Combatant SelectPackHunterTarget(CombatState state, Combatant[] targets)
    {
        var enemyRows = state.Combatants
            .Where(c => !c.IsPlayer && c.IsAlive)
            .Select(c => c.Row)
            .ToArray();

        return targets
            .Select(t => (Target: t, Count: enemyRows.Count(r => r == t.Row)))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Target.Id)
            .First()
            .Target;
    }

    private static Combatant DefaultTarget(Combatant actor, Combatant[] targets, GameRandom rng)
    {
        return actor.Speed >= 6 && rng.Next(2) == 0
            ? targets.OrderByDescending(t => t.Row).First()
            : targets[rng.Next(targets.Length)];
    }

    private static (ActionType Type, string? AbilityId) SelectAction(Combatant actor, string behavior, CombatState state)
    {
        var abilityId = behavior switch
        {
            "aggressive" or "pack_hunter" => FindMatchingAbility(actor, "melee"),
            "ranged_priority" => FindMatchingAbility(actor, "ranged"),
            "defensive" => FindMatchingAbility(actor, "defensive"),
            "soldier_tactical" or "zealot_aggressive" => ChooseSoldierAbility(actor, state),
            _ => null
        };

        if (abilityId != null)
            return (ActionType.UseAbility, abilityId);

        return behavior == "defensive"
            ? (ActionType.Defend, null)
            : (ActionType.Attack, null);
    }

    private static string? ChooseSoldierAbility(Combatant actor, CombatState state)
    {
        if (actor.Abilities == null || actor.Abilities.Length == 0)
            return null;

        // If another ability from this actor's list was used this round, pick a different one
        var usedThisRound = state.AbilitiesUsedThisRound
            .Where(a => actor.Abilities.Contains(a))
            .ToHashSet();

        var available = actor.Abilities.Where(a => !usedThisRound.Contains(a)).ToArray();
        if (available.Length > 0)
            return available[0];

        return actor.Abilities[0];
    }

    /// <summary>
    /// Picks the actor's first ability whose id reads like the wanted category. Enemy abilities
    /// are content ids rather than typed data, so the category is inferred from the name.
    /// </summary>
    private static string? FindMatchingAbility(Combatant actor, string category)
    {
        if (actor.Abilities == null || actor.Abilities.Length == 0)
            return null;

        var keywords = category switch
        {
            "ranged" => new[] { "arrow", "shot", "bolt", "ranged", "throw" },
            "melee" => new[] { "slash", "strike", "bite", "crack", "rend", "shiv", "spear", "blade", "thrust" },
            "defensive" => new[] { "ward", "shield", "block", "stance", "heal", "buff", "guard", "suppress" },
            _ => Array.Empty<string>()
        };

        foreach (var ability in actor.Abilities)
        {
            var lower = ability.ToLowerInvariant();
            if (keywords.Any(k => lower.Contains(k)))
                return ability;
        }

        return null;
    }
}
