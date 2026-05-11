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

    public static CombatState Tick(CombatState state, CombatAction? action, GameRandom rng, ClassRegistry? classes = null)
    {
        return state.Phase switch
        {
            CombatPhase.RoundStart => StartRound(state, rng),
            CombatPhase.Turn => HandleTurn(state, action, rng),
            CombatPhase.Resolve => Resolve(state, rng, classes),
            CombatPhase.CheckEnd => CheckEnd(state),
            _ => state
        };
    }

    private static CombatState StartRound(CombatState state, GameRandom rng)
    {
        var order = RollInitiative(state.Combatants, rng);
        return state with
        {
            InitiativeOrder = order,
            CurrentTurnIndex = 0,
            Phase = CombatPhase.Turn,
            Log = new List<CombatLogEntry>(state.Log)
            {
                new(Guid.Empty, $"Round {state.Round} begins", state.Round)
            }
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
            var aiAction = GenerateAIAction(state, actor.Value, rng);
            return state with { PendingAction = aiAction, Phase = CombatPhase.Resolve };
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

        // Ranged AI prefers back row targets if in range, otherwise closest
        var target = actor.Speed >= 6 && rng.Next(2) == 0
            ? targets.OrderByDescending(t => t.Row).First()
            : targets[rng.Next(targets.Length)];

        return new CombatAction(actor.Id, ActionType.Attack, target.Id, null, null);
    }

    private static CombatState Resolve(CombatState state, GameRandom rng, ClassRegistry? classes)
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
                        newCombatants[targetIdx] = target with { Hp = newHp };
                        newLog.Add(new(action.ActorId,
                            $"{actor.Name} hits {target.Name} for {damage} damage", state.Round));
                    }
                }
                break;

            case ActionType.UseAbility:
                if (action.AbilityId is not null && action.TargetId is not null)
                {
                    var targetIdx = Array.FindIndex(newCombatants, c => c.Id == action.TargetId);
                    if (targetIdx >= 0)
                    {
                        var damage = ResolveAbilityDamage(actor, action.AbilityId, classes, rng);
                        var target = newCombatants[targetIdx];
                        if (damage > 0)
                        {
                            var newHp = Math.Max(0, target.Hp - damage);
                            newCombatants[targetIdx] = target with { Hp = newHp };
                            newLog.Add(new(action.ActorId,
                                $"{actor.Name} uses {action.AbilityId} on {target.Name} for {damage} damage", state.Round));
                        }
                        else
                        {
                            newLog.Add(new(action.ActorId,
                                $"{actor.Name} uses {action.AbilityId} on {target.Name}", state.Round));
                        }
                    }
                }
                break;

            case ActionType.Defend:
                newLog.Add(new(action.ActorId, $"{actor.Name} takes a defensive stance", state.Round));
                break;

            case ActionType.Wait:
                newLog.Add(new(action.ActorId, $"{actor.Name} waits", state.Round));
                break;

            default:
                newLog.Add(new(action.ActorId, $"{actor.Name} acts", state.Round));
                break;
        }

        return state with
        {
            Combatants = newCombatants,
            Log = newLog,
            PendingAction = null,
            Phase = CombatPhase.CheckEnd
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

    private static CombatState CheckEnd(CombatState state)
    {
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

        var nextIndex = state.CurrentTurnIndex + 1;
        if (nextIndex >= state.InitiativeOrder.Length)
        {
            return state with
            {
                Round = state.Round + 1,
                CurrentTurnIndex = 0,
                Phase = CombatPhase.RoundStart
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

                enemies.Add(new Combatant(
                    id,
                    $"{name}_{i + 1}",
                    false,
                    hp + rng.Roll(0, 3),
                    hp + 3,
                    speed + rng.Roll(-1, 1),
                    spawn.RowOverride ?? (rng.Next(2) == 0 ? 0 : 1),
                    new List<StatusEffect>(),
                    def?.Stats.Strength ?? 0
                ));
                enemyIndex++;
            }
        }
        return enemies.ToArray();
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
            character.ClassId
        );
    }
}
