using RPC.Engine.Character;

namespace RPC.Engine.Combat;

public readonly record struct Combatant(
    Guid Id,
    string Name,
    bool IsPlayer,
    int Hp,
    int MaxHp,
    int Speed,
    int Row,
    List<StatusEffect> StatusEffects,
    int Power = 0,
    string? ClassId = null,
    bool IsSummoned = false,
    int SummonDuration = 0,
    TempStatModifier[]? TempModifiers = null,
    string? AiBehavior = null,
    string[]? Abilities = null)
{
    public TempStatModifier[] TempModifiers { get; init; } = TempModifiers ?? Array.Empty<TempStatModifier>();

    public bool IsAlive => Hp > 0;
    public bool IsFrontRow => Row == 0;
}

public readonly record struct StatusEffect(string Type, int Duration, int? Potency);

public enum ActionType
{
    Attack,
    Defend,
    Wait,
    UseAbility,
    UseItem,
    Flee
}

public record CombatAction(
    Guid ActorId,
    ActionType Type,
    Guid? TargetId,
    string? AbilityId,
    string? ItemId);

public readonly record struct CombatLogEntry(Guid ActorId, string Message, int Round);

public enum CombatPhase
{
    RoundStart,
    Turn,
    Resolve,
    CheckEnd,
    Ended
}

public record CombatState(
    Combatant[] Combatants,
    int Round,
    Guid[] InitiativeOrder,
    int CurrentTurnIndex,
    List<CombatLogEntry> Log,
    CombatAction? PendingAction,
    CombatPhase Phase,
    int XpReward = 10)
{
    public HashSet<string> AbilitiesUsedThisRound { get; init; } = new();
    public Dictionary<int, Guid> SummonSlotAssignments { get; init; } = new();

    public Combatant? CurrentActor =>
        Phase == CombatPhase.Turn && CurrentTurnIndex < InitiativeOrder.Length
            ? Combatants.FirstOrDefault(c => c.Id == InitiativeOrder[CurrentTurnIndex] && c.IsAlive)
            : null;

    public bool AllEnemiesDead => Combatants.All(c => c.IsPlayer || !c.IsAlive);
    public bool AllPlayersDead => Combatants.All(c => !c.IsPlayer || c.IsSummoned || !c.IsAlive);
    public bool IsFinished => Phase == CombatPhase.Ended;
}

public record EnemySpawn(string EnemyId, int Count, int? RowOverride = null);

public record EncounterDef(string Id, string Name, EnemySpawn[] Enemies, int XpReward = 10);
