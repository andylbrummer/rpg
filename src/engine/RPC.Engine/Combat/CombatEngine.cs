using RPC.Engine.Character;
using RPC.Engine.Party;

namespace RPC.Engine.Combat;

public static class CombatEngine
{
    public static CombatState Enter(
        PartyState party,
        EncounterDef encounter,
        GameRandom rng,
        EnemyRegistry? enemies = null)
    {
        var enemyCombatants = SpawnEnemies(encounter, rng, enemies);
        var all = party.Active.Select(ToCombatant)
            .Concat(enemyCombatants)
            .ToArray();

        if (all.All(c => !c.IsPlayer || c.IsAlive) && enemyCombatants.Length == 0)
        {
            return new CombatState(
                all,
                1,
                Array.Empty<Guid>(),
                0,
                new List<CombatLogEntry> { new(Guid.Empty, "Victory!", 1) },
                null,
                CombatPhase.Ended,
                encounter.XpReward);
        }

        var order = RollInitiative(all, rng);

        return new CombatState(
            all,
            1,
            order,
            0,
            new List<CombatLogEntry>(),
            null,
            CombatPhase.RoundStart,
            encounter.XpReward);
    }

    public static CombatState Tick(CombatState state, CombatAction? action, GameRandom rng, ClassRegistry? classes = null, Action<string, string, Dictionary<string, string>>? actionLogEmitter = null, SynergyRegistry? synergies = null)
    {
        return state.Phase switch
        {
            CombatPhase.RoundStart => StartRound(state, rng),
            CombatPhase.Turn => HandleTurn(state, action, rng),
            CombatPhase.Resolve => Resolve(state, rng, classes, actionLogEmitter, synergies),
            CombatPhase.CheckEnd => CheckEnd(state, actionLogEmitter),
            _ => state
        };
    }

    private static CombatState StartRound(CombatState state, GameRandom rng)
    {
        var order = RollInitiative(state.Combatants, rng);
        var unaccounted = state.Combatants
            .Where(c => c.IsAlive && IsUnaccounted(c))
            .Select(c => c.Id)
            .ToArray();

        if (unaccounted.Length > 0)
        {
            // Shield Wall: Bonewarden ability blocks Unaccounted interrupts
            var hasShieldWall = state.Combatants.Any(c => c.IsPlayer && c.IsAlive && c.StatusEffects.Any(s => s.Type == "shield_wall"));
            if (!hasShieldWall)
            {
                // Interrupt: insert Unaccounted turns at random positions
                var expanded = new List<Guid>(order);
                foreach (var uid in unaccounted)
                {
                    var pos = rng.Roll(1, expanded.Count); // avoid position 0
                    expanded.Insert(pos, uid);
                }
                order = expanded.ToArray();
            }
        }

        return state with
        {
            InitiativeOrder = order,
            CurrentTurnIndex = 0,
            Phase = CombatPhase.Turn,
            Log = new List<CombatLogEntry>(state.Log)
            {
                new(Guid.Empty, $"Round {state.Round} begins", state.Round)
            },
            AbilitiesUsedThisRound = new HashSet<string>()
        };
    }

    private static CombatState HandleTurn(CombatState state, CombatAction? action, GameRandom rng)
    {
        var actor = state.CurrentActor;

        // Dead actor or no actor -> skip to CheckEnd
        if (actor is null || !actor.Value.IsAlive)
            return state with { Phase = CombatPhase.CheckEnd };

        // AI turn -> generate action automatically
        if (!actor.Value.IsPlayer)
        {
            var newCombatants = state.Combatants.ToArray();
            var actorIdx = Array.FindIndex(newCombatants, c => c.Id == actor.Value.Id);
            var currentActor = newCombatants[actorIdx];

            // Phase: Unaccounted can teleport between rows before acting
            if (IsUnaccounted(currentActor) && rng.Next(2) == 0)
            {
                var newRow = currentActor.Row == 0 ? 1 : 0;
                newCombatants[actorIdx] = currentActor with { Row = newRow };
                currentActor = newCombatants[actorIdx];
            }

            var aiAction = GenerateAIAction(state, currentActor, rng);
            return state with { Combatants = newCombatants, PendingAction = aiAction, Phase = CombatPhase.Resolve };
        }

        // Player turn -> wait for action
        if (action is null)
            return state;

        return state with { PendingAction = action, Phase = CombatPhase.Resolve };
    }

    private static CombatAction GenerateAIAction(CombatState state, Combatant actor, GameRandom rng)
    {
        var targets = state.Combatants.Where(c => c.IsPlayer && c.IsAlive).ToArray();
        if (targets.Length == 0)
            return new CombatAction(actor.Id, ActionType.Wait, null, null, null);

        var behavior = actor.AiBehavior?.ToLowerInvariant() ?? "";

        // Faction soldier retreat check
        if (behavior == "soldier_tactical" && ShouldRetreat(state, actor))
        {
            return new CombatAction(actor.Id, ActionType.Flee, null, null, null);
        }

        var target = SelectTarget(state, actor, targets, behavior, rng);
        var (actionType, abilityId) = ChooseAction(actor, behavior, state);

        return new CombatAction(actor.Id, actionType, target.Id, abilityId, null);
    }

    private static bool ShouldRetreat(CombatState state, Combatant actor)
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
        if (IsUnaccounted(actor))
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

    private static (ActionType Type, string? AbilityId) ChooseAction(Combatant actor, string behavior, CombatState state)
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

    private static CombatState Resolve(CombatState state, GameRandom rng, ClassRegistry? classes, Action<string, string, Dictionary<string, string>>? actionLogEmitter = null, SynergyRegistry? synergies = null)
    {
        if (state.PendingAction is null)
            return state with { Phase = CombatPhase.CheckEnd };

        var action = state.PendingAction;
        var actor = state.Combatants.First(c => c.Id == action.ActorId);
        var newLog = new List<CombatLogEntry>(state.Log);
        var newCombatants = state.Combatants.ToArray();

        switch (action.Type)
        {
            case ActionType.Attack:
                if (action.TargetId is not null)
                {
                    var targetIdx = Array.FindIndex(newCombatants, c => c.Id == action.TargetId);
                    if (targetIdx >= 0)
                    {
                        var target = newCombatants[targetIdx];
                        var damage = Math.Max(1, rng.Roll(1, 6) + 2); // placeholder damage
                        var newHp = Math.Max(0, target.Hp - damage);
                        var newEffects = new List<StatusEffect>(target.StatusEffects);

                        // Dread: Unaccounted attacks inflict dread
                        if (IsUnaccounted(actor))
                        {
                            newEffects.Add(new StatusEffect("dread", -1, null, actor.Id));
                            newLog.Add(new(action.ActorId, $"{target.Name} is stricken with dread", state.Round));
                        }

                        newCombatants[targetIdx] = target with { Hp = newHp, StatusEffects = newEffects };
                        newLog.Add(new(action.ActorId,
                            $"{actor.Name} hits {target.Name} for {damage} damage", state.Round));
                        if (target.IsSummoned && newHp == 0)
                            newLog.Add(new(Guid.Empty, $"{target.Name} died", state.Round));
                    }
                }
                break;

            case ActionType.UseAbility:
                if (action.AbilityId is not null && action.TargetId is not null)
                {
                    var targetIdx = Array.FindIndex(newCombatants, c => c.Id == action.TargetId);
                    var actorIdx = Array.FindIndex(newCombatants, c => c.Id == action.ActorId);
                    if (targetIdx >= 0 && actorIdx >= 0)
                    {
                        var damage = ResolveAbilityDamage(actor, action.AbilityId, classes, rng);
                        var target = newCombatants[targetIdx];
                        if (damage > 0)
                        {
                            var newHp = Math.Max(0, target.Hp - damage);
                            var newEffects = new List<StatusEffect>(target.StatusEffects);

                            // Cauterist fire: burned corpses cannot reassemble
                            if (IsUnaccounted(target) && newHp == 0 && IsFireAbility(actor, action.AbilityId, classes))
                            {
                                newEffects.Add(new StatusEffect("burned", 999, null));
                            }

                            newCombatants[targetIdx] = target with { Hp = newHp, StatusEffects = newEffects };
                            newLog.Add(new(action.ActorId,
                                $"{actor.Name} uses {action.AbilityId} on {target.Name} for {damage} damage", state.Round));
                            if (target.IsSummoned && newHp == 0)
                                newLog.Add(new(Guid.Empty, $"{target.Name} died", state.Round));
                        }
                        else
                        {
                            newLog.Add(new(action.ActorId,
                                $"{actor.Name} uses {action.AbilityId} on {target.Name}", state.Round));
                        }

                        ApplyMemoryCost(actorIdx, action.AbilityId, classes);
                        ApplySynergies(action.AbilityId, actor, targetIdx);

                        // War Cry: Ashmouth ability dispels dread from all allies
                        if (action.AbilityId == "war_cry")
                        {
                            for (int i = 0; i < newCombatants.Length; i++)
                            {
                                var c = newCombatants[i];
                                if (c.IsPlayer && c.StatusEffects.Any(s => s.Type == "dread"))
                                {
                                    newCombatants[i] = c with { StatusEffects = c.StatusEffects.Where(s => s.Type != "dread").ToList() };
                                }
                            }
                            newLog.Add(new(action.ActorId, $"{actor.Name}'s war cry dispels the dread", state.Round));
                        }
                    }
                }
                break;

            case ActionType.Defend:
                newLog.Add(new(action.ActorId, $"{actor.Name} takes a defensive stance", state.Round));
                break;

            case ActionType.Flee:
                {
                    var actorIdx = Array.FindIndex(newCombatants, c => c.Id == action.ActorId);
                    if (actorIdx >= 0)
                    {
                        newCombatants[actorIdx] = newCombatants[actorIdx] with { Hp = 0 };
                        newLog.Add(new(action.ActorId, $"{actor.Name} flees", state.Round));
                    }
                }
                break;

            case ActionType.Wait:
                newLog.Add(new(action.ActorId, $"{actor.Name} waits", state.Round));
                break;

            default:
                newLog.Add(new(action.ActorId, $"{actor.Name} acts", state.Round));
                break;
        }

        void ApplySynergies(string abilityId, Combatant a, int idx)
        {
            foreach (var used in state.AbilitiesUsedThisRound)
            {
                var synEntry = synergies?.LookupWithId(abilityId, used);
                if (synEntry is not null)
                {
                    ApplySynergyEffect(synEntry.Value.Effect, a, new SynergyContext(newCombatants, newLog, state.Round, idx));
                    actionLogEmitter?.Invoke("combat", "synergy_triggered", new Dictionary<string, string>
                    {
                        { "synergyId", synEntry.Value.Id ?? "" },
                        { "targetId", newCombatants[idx].Id.ToString() }
                    });
                }
            }
        }

        void ApplyMemoryCost(int actorIdx, string abilityId, ClassRegistry? classRegistry)
        {
            var a = newCombatants[actorIdx];
            if (string.IsNullOrEmpty(a.ClassId) || classRegistry is null)
                return;

            var def = classRegistry.Get(a.ClassId);
            var ab = def?.Abilities.FirstOrDefault(x => x.Id == abilityId);
            var mc = ab?.MemoryCost;
            if (mc is null)
                return;

            var mod = new TempStatModifier(mc.Value.Stat, -mc.Value.Amount, mc.Value.Duration, abilityId);
            var updatedMods = new List<TempStatModifier>(a.TempModifiers) { mod };

            newCombatants[actorIdx] = a with { TempModifiers = updatedMods.ToArray() };
            ApplyModifierToCombatant(ref newCombatants[actorIdx], mod);

            newLog.Add(new(a.Id, $"{a.Name}'s {mc.Value.Stat} reduced by {mc.Value.Amount}", state.Round));
            actionLogEmitter?.Invoke("combat", "stat_reduced", new Dictionary<string, string>
            {
                { "characterId", a.Id.ToString() },
                { "stat", mc.Value.Stat },
                { "amount", mc.Value.Amount.ToString() },
                { "duration", mc.Value.Duration.ToString() },
                { "source", abilityId }
            });
        }

        var updatedAbilities = new HashSet<string>(state.AbilitiesUsedThisRound);
        if (action.Type == ActionType.UseAbility && action.AbilityId is not null)
        {
            updatedAbilities.Add(action.AbilityId);
        }

        return state with
        {
            Combatants = newCombatants,
            Log = newLog,
            PendingAction = null,
            Phase = CombatPhase.CheckEnd,
            AbilitiesUsedThisRound = updatedAbilities
        };
    }

    private static int ResolveAbilityDamage(Combatant actor, string abilityId, ClassRegistry? classes, GameRandom rng)
    {
        if (string.IsNullOrEmpty(actor.ClassId) || classes is null)
            return 0;

        var classDef = classes.Get(actor.ClassId);
        var ability = classDef?.Abilities.FirstOrDefault(a => a.Id == abilityId);
        if (ability is null)
            return 0;

        var effect = ability.Effect;
        if (effect.Type != "damage")
            return 0;

        var valueStr = effect.Value?.GetString();
        if (string.IsNullOrEmpty(valueStr))
            return 0;

        var parts = valueStr.Split('+');
        var diceParts = parts[0].Split('d');
        if (diceParts.Length != 2 || !int.TryParse(diceParts[0], out var count) || !int.TryParse(diceParts[1], out var sides))
            return 0;

        var bonus = 0;
        if (parts.Length > 1)
        {
            if (parts[1] == "PWR")
                bonus = actor.Power;
            else
                int.TryParse(parts[1], out bonus);
        }

        var roll = 0;
        for (int i = 0; i < count; i++)
            roll += rng.Roll(1, sides);

        return Math.Max(1, roll + bonus);
    }

    private record struct SynergyContext(
        Combatant[] Combatants,
        List<CombatLogEntry> Log,
        int Round,
        int TargetIdx);

    private static void ApplySynergyEffect(SynergyEffect synergy, Combatant actor, SynergyContext ctx)
    {
        var target = ctx.Combatants[ctx.TargetIdx];
        switch (synergy.Type)
        {
            case "bonus_damage":
                var bonus = Math.Max(0, synergy.Value);
                ctx.Combatants[ctx.TargetIdx] = target with { Hp = Math.Max(0, target.Hp - bonus) };
                ctx.Log.Add(new(actor.Id,
                    $"{actor.Name} synergy deals {bonus} bonus damage to {target.Name}", ctx.Round));
                break;

            case "apply_status":
                var effects = new List<StatusEffect>(target.StatusEffects)
                {
                    new(synergy.StatusType ?? "unknown", synergy.StatusDuration ?? 1, synergy.Value)
                };
                ctx.Combatants[ctx.TargetIdx] = target with { StatusEffects = effects };
                ctx.Log.Add(new(actor.Id,
                    $"{actor.Name} synergy applies {synergy.StatusType} to {target.Name}", ctx.Round));
                break;

            default:
                ctx.Log.Add(new(actor.Id,
                    $"{actor.Name} synergy triggers with {target.Name}", ctx.Round));
                break;
        }
    }

    private static CombatState CheckEnd(CombatState state, Action<string, string, Dictionary<string, string>>? actionLogEmitter = null)
    {
        var nextIndex = state.CurrentTurnIndex + 1;
        if (nextIndex >= state.InitiativeOrder.Length)
        {
            var newRound = state.Round + 1;
            var newCombatants = state.Combatants.ToArray();
            var newLog = new List<CombatLogEntry>(state.Log);
            var expiredIds = new HashSet<Guid>();
            var deadUnaccounted = new List<DeadUnaccounted>(state.DeadUnaccounted);

            // Track newly dead Unaccounted
            foreach (var c in newCombatants)
            {
                if (!c.IsAlive && IsUnaccounted(c) && !deadUnaccounted.Any(d => d.Id == c.Id))
                {
                    var isBurned = c.StatusEffects.Any(s => s.Type == "burned");
                    deadUnaccounted.Add(new DeadUnaccounted(c.Id, state.Round, isBurned));
                }
            }

            // Reassemble: merge 2+ dead Unaccounted after 2 rounds (burned corpses excluded)
            var readyToReassemble = deadUnaccounted.Where(d => !d.Burned && state.Round - d.RoundDied >= 2).ToArray();
            if (readyToReassemble.Length >= 2)
            {
                var toMerge = readyToReassemble.Take(2).ToArray();
                deadUnaccounted = deadUnaccounted.Where(d => !toMerge.Any(t => t.Id == d.Id)).ToList();

                var newId = Guid.NewGuid();
                var reassembled = new Combatant(
                    newId,
                    "Reassembled",
                    false,
                    18,
                    18,
                    6,
                    0,
                    new List<StatusEffect>(),
                    6,
                    null,
                    false,
                    0,
                    null,
                    "unaccounted",
                    new[] { "unaccounted_strike" });

                newCombatants = newCombatants.Append(reassembled).ToArray();
                newLog.Add(new(Guid.Empty, "Fallen Unaccounted reassemble into something worse", newRound));
            }

            // Clean up dread from dead Unaccounted
            for (int i = 0; i < newCombatants.Length; i++)
            {
                var c = newCombatants[i];
                var deadUnaccountedIds = deadUnaccounted.Select(d => d.Id).ToHashSet();
                if (c.StatusEffects.Any(s => s.Type == "dread" && deadUnaccountedIds.Contains(s.SourceId)))
                {
                    var cleaned = c.StatusEffects
                        .Where(s => !(s.Type == "dread" && deadUnaccountedIds.Contains(s.SourceId)))
                        .ToList();
                    newCombatants[i] = c with { StatusEffects = cleaned };
                }
            }

            // War Cry: Ashmouth ability dispels dread from all allies
            // (handled in Resolve; this ensures any lingering dread from dead sources is also cleared)

            for (int i = 0; i < newCombatants.Length; i++)
            {
                var c = newCombatants[i];
                if (c.TempModifiers.Length > 0)
                {
                    var remaining = new List<TempStatModifier>();
                    foreach (var mod in c.TempModifiers)
                    {
                        var decremented = mod.Decrement();
                        if (decremented.Duration > 0)
                        {
                            remaining.Add(decremented);
                        }
                        else
                        {
                            RemoveModifierFromCombatant(ref c, mod);
                            newLog.Add(new(Guid.Empty, $"{c.Name}'s {mod.Stat} restored", newRound));
                            actionLogEmitter?.Invoke("combat", "stat_restored", new Dictionary<string, string>
                            {
                                { "characterId", c.Id.ToString() },
                                { "stat", mod.Stat },
                                { "source", mod.Source }
                            });
                        }
                    }
                    newCombatants[i] = c with { TempModifiers = remaining.ToArray() };
                }

                c = newCombatants[i];
                if (c.IsSummoned && c.IsAlive && c.SummonDuration > 0)
                {
                    var newDuration = c.SummonDuration - 1;
                    if (newDuration <= 0)
                    {
                        newCombatants[i] = c with { Hp = 0, SummonDuration = 0 };
                        newLog.Add(new(Guid.Empty, $"{c.Name} expired", newRound));
                        expiredIds.Add(c.Id);
                    }
                    else
                    {
                        newCombatants[i] = c with { SummonDuration = newDuration };
                    }
                }
            }

            // Check for victory/defeat AFTER reassembly
            var allEnemiesDead = newCombatants.All(c => c.IsPlayer || !c.IsAlive)
                && !deadUnaccounted.Any(d => !d.Burned && state.Round - d.RoundDied < 2);
            var allPlayersDead = newCombatants.All(c => !c.IsPlayer || c.IsSummoned || !c.IsAlive);

            if (allEnemiesDead)
            {
                newLog.Add(new(Guid.Empty, "Victory!", newRound));
                return state with
                {
                    Round = newRound,
                    CurrentTurnIndex = 0,
                    Phase = CombatPhase.Ended,
                    Combatants = newCombatants,
                    Log = newLog,
                    SummonSlotAssignments = state.SummonSlotAssignments
                        .Where(kv => !expiredIds.Contains(kv.Value))
                        .ToDictionary(kv => kv.Key, kv => kv.Value),
                    DeadUnaccounted = deadUnaccounted
                };
            }

            if (allPlayersDead)
            {
                newLog.Add(new(Guid.Empty, "Defeat...", newRound));
                return state with
                {
                    Round = newRound,
                    CurrentTurnIndex = 0,
                    Phase = CombatPhase.Ended,
                    Combatants = newCombatants,
                    Log = newLog,
                    SummonSlotAssignments = state.SummonSlotAssignments
                        .Where(kv => !expiredIds.Contains(kv.Value))
                        .ToDictionary(kv => kv.Key, kv => kv.Value),
                    DeadUnaccounted = deadUnaccounted
                };
            }

            var newAssignments = state.SummonSlotAssignments
                .Where(kv => !expiredIds.Contains(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            return state with
            {
                Round = newRound,
                CurrentTurnIndex = 0,
                Phase = CombatPhase.RoundStart,
                AbilitiesUsedThisRound = new HashSet<string>(),
                Combatants = newCombatants,
                Log = newLog,
                SummonSlotAssignments = newAssignments,
                DeadUnaccounted = deadUnaccounted
            };
        }

        // Check for victory/defeat mid-round
        if (state.AllEnemiesDead)
        {
            return state with
            {
                Phase = CombatPhase.Ended,
                Log = new List<CombatLogEntry>(state.Log)
                { new(Guid.Empty, "Victory!", state.Round) }
            };
        }

        if (state.AllPlayersDead)
        {
            return state with
            {
                Phase = CombatPhase.Ended,
                Log = new List<CombatLogEntry>(state.Log)
                { new(Guid.Empty, "Defeat...", state.Round) }
            };
        }

        return state with
        {
            CurrentTurnIndex = nextIndex,
            Phase = CombatPhase.Turn
        };
    }

    private static Guid[] RollInitiative(Combatant[] combatants, GameRandom rng)
    {
        return combatants
            .Where(c => c.IsAlive)
            .Select(c => (c.Id, Roll: c.Speed + rng.Roll(-3, 3)))
            .OrderByDescending(x => x.Roll)
            .ThenBy(x => x.Id) // tie-breaker for determinism
            .Select(x => x.Id)
            .ToArray();
    }

    private static bool IsUnaccounted(Combatant c) => c.AiBehavior == "unaccounted";

    private static bool IsFireAbility(Combatant actor, string abilityId, ClassRegistry? classes)
    {
        if (string.IsNullOrEmpty(actor.ClassId) || classes is null)
            return false;
        var classDef = classes.Get(actor.ClassId);
        var ability = classDef?.Abilities.FirstOrDefault(a => a.Id == abilityId);
        return ability?.Tags.Any(t => t.Contains("fire")) == true;
    }

    private static Combatant[] SpawnEnemies(EncounterDef encounter, GameRandom rng, EnemyRegistry? registry)
    {
        var enemies = new List<Combatant>();
        int enemyIndex = 0;
        foreach (var spawn in encounter.Enemies)
        {
            var def = registry?.Get(spawn.EnemyId);
            for (int i = 0; i < spawn.Count; i++)
            {
                // Deterministic pseudo-GUID based on index for reproducibility
                var id = new Guid(
                    0x11111111, 0x2222, 0x3333,
                    0x44, 0x55,
                    (byte)(0x66 + enemyIndex), (byte)(0x77 + i),
                    (byte)spawn.EnemyId.Length, (byte)spawn.Count,
                    (byte)(spawn.RowOverride ?? 99), 0xFF);

                var hp = def?.HpBase ?? 10;
                var speed = def?.Speed ?? 5;
                var name = def?.Name ?? spawn.EnemyId;

                // Apply faction equipment modifiers
                if (def?.FactionId == "bureau")
                {
                    hp += 2; // Bureau armor bonus
                }

                var statusEffects = new List<StatusEffect>();
                if (def?.FactionId == "convocation")
                {
                    statusEffects.Add(new StatusEffect("bloom_resistance", 999, null));
                }

                // Phase: Unaccounted can appear at any range band
                var row = spawn.RowOverride ?? (def?.Ai == "unaccounted" ? rng.Next(2) : (rng.Next(2) == 0 ? 0 : 1));

                enemies.Add(new Combatant(
                    id,
                    $"{name}_{i + 1}",
                    false,
                    hp + rng.Roll(0, 3),
                    hp + 3,
                    speed + rng.Roll(-1, 1),
                    row,
                    statusEffects,
                    def?.Stats.Strength ?? 0,
                    null,
                    false,
                    0,
                    null,
                    def?.Ai,
                    def?.Abilities
                ));
                enemyIndex++;
            }
        }
        return enemies.ToArray();
    }

    public static CombatState SummonAlly(CombatState state, PartyState party, SummonDef def, GameRandom rng)
    {
        var newAssignments = new Dictionary<int, Guid>(state.SummonSlotAssignments);

        int slot = -1;
        for (int i = 0; i < party.Members.Length; i++)
        {
            if (party.Members[i].Id == Guid.Empty && !newAssignments.ContainsKey(i))
            {
                slot = i;
                break;
            }
        }

        int row = slot >= 0 ? (slot < 3 ? 0 : 1) : def.Row;
        var id = Guid.NewGuid();
        var summon = new Combatant(
            id,
            def.Name,
            true,
            def.Hp,
            def.Hp,
            def.Speed,
            row,
            new List<StatusEffect>(),
            def.Power,
            null,
            true,
            def.Duration);

        if (slot >= 0)
            newAssignments[slot] = id;

        var newCombatants = state.Combatants.Append(summon).ToArray();

        return state with
        {
            Combatants = newCombatants,
            Log = new List<CombatLogEntry>(state.Log)
            {
                new(Guid.Empty, $"{def.Name} summoned", state.Round)
            },
            SummonSlotAssignments = newAssignments
        };
    }



    private static void ApplyModifierToCombatant(ref Combatant combatant, TempStatModifier mod)
    {
        var hp = combatant.Hp;
        var maxHp = combatant.MaxHp;
        var speed = combatant.Speed;
        var power = combatant.Power;

        switch (mod.Stat.ToLowerInvariant())
        {
            case "strength": power += mod.Delta; break;
            case "dexterity": speed += mod.Delta; break;
            case "constitution": maxHp += mod.Delta * 3; break;
            case "maxhp": maxHp += mod.Delta; break;
            case "speed": speed += mod.Delta; break;
            case "power": power += mod.Delta; break;
        }

        if (hp > maxHp) hp = maxHp;
        if (maxHp < 1) maxHp = 1;
        if (speed < 1) speed = 1;
        if (power < 0) power = 0;

        combatant = combatant with { Hp = hp, MaxHp = maxHp, Speed = speed, Power = power };
    }

    private static void RemoveModifierFromCombatant(ref Combatant combatant, TempStatModifier mod)
    {
        ApplyModifierToCombatant(ref combatant, mod with { Delta = -mod.Delta });
    }

    public static CombatState AutoResolveToPlayerTurn(
        CombatState state, GameRandom rng, ClassRegistry? classes = null,
        Action<string, string, Dictionary<string, string>>? actionLogEmitter = null, SynergyRegistry? synergies = null)
    {
        while (!state.IsFinished && !(state.Phase == CombatPhase.Turn && state.CurrentActor?.IsPlayer == true))
        {
            state = Tick(state, null, rng, classes, actionLogEmitter, synergies);
        }
        return state;
    }

    private static Combatant ToCombatant(CharacterState character)
    {
        var stats = character.GetEffectiveStats();
        return new Combatant(
            character.Id,
            character.Name,
            true,
            character.CurrentHp,
            stats.MaxHp,
            stats.Speed,
            character.Row,
            new List<StatusEffect>(),
            stats.Power,
            character.ClassId,
            false,
            0,
            character.TempModifiers.ToArray()
        );
    }
}

public record SummonDef(string Id, string Name, int Hp, int Speed, int Power, int Duration, int Row = 0);
