using RPC.Engine.Content;

namespace RPC.Engine.Character;

public readonly record struct TempStatModifier(string Stat, int Delta, int Duration, string Source)
{
    public TempStatModifier Decrement() => this with { Duration = Duration - 1 };
}

public readonly record struct MemoryCost(string Stat, int Amount, int Duration);

public readonly record struct ComponentStack(
    string ItemId,
    int Count,
    int MaxStack = 99,
    int DungeonTurnsAlive = 0,
    bool Stabilized = false)
{
    public int RemainingSpace => MaxStack - Count;
    public bool IsFull => Count >= MaxStack;
}

public readonly record struct CharacterState(
    Guid Id,
    string Name,
    string ClassId,
    int Level,
    int Xp,
    BaseStats BaseStats,
    int CurrentHp,
    Equipment Equipment,
    string[] KnownAbilities,
    int Row,
    string? BranchChoice = null,
    string? BranchLevel6 = null,
    TempStatModifier[]? TempModifiers = null,
    int ResurrectionAttempts = 0,
    bool BranchAdvancementLocked = false,
    ComponentStack[]? ComponentInventory = null)
{
    public TempStatModifier[] TempModifiers { get; init; } = TempModifiers ?? Array.Empty<TempStatModifier>();
    public ComponentStack[] ComponentInventory { get; init; } = ComponentInventory ?? Array.Empty<ComponentStack>();
    public const int MaxComponentSlots = 8;

    public EffectiveStats GetEffectiveStats(ItemRegistry? items = null)
    {
        var baseWithEquipment = BaseStats + Equipment.StatBonus(items);
        var modifiedBase = ApplyTempModifiers(baseWithEquipment);
        var effective = EffectiveStats.FromBase(modifiedBase, Level);

        var maxHp = effective.MaxHp;
        var speed = effective.Speed;
        var accuracy = effective.Accuracy;
        var evade = effective.Evade;
        var power = effective.Power;

        foreach (var mod in TempModifiers ?? Array.Empty<TempStatModifier>())
        {
            switch (mod.Stat.ToLowerInvariant())
            {
                case "maxhp": maxHp += mod.Delta; break;
                case "speed": speed += mod.Delta; break;
                case "accuracy": accuracy += mod.Delta; break;
                case "evade": evade += mod.Delta; break;
                case "power": power += mod.Delta; break;
            }
        }

        return new EffectiveStats(
            Math.Max(1, maxHp),
            Math.Max(1, speed),
            Math.Max(0, accuracy),
            Math.Max(0, evade),
            Math.Max(0, power));
    }

    private BaseStats ApplyTempModifiers(BaseStats baseStats)
    {
        var str = baseStats.Strength;
        var dex = baseStats.Dexterity;
        var con = baseStats.Constitution;
        var inte = baseStats.Intelligence;
        var wil = baseStats.Willpower;

        foreach (var mod in TempModifiers ?? Array.Empty<TempStatModifier>())
        {
            switch (mod.Stat.ToLowerInvariant())
            {
                case "strength": str += mod.Delta; break;
                case "dexterity": dex += mod.Delta; break;
                case "constitution": con += mod.Delta; break;
                case "intelligence": inte += mod.Delta; break;
                case "willpower": wil += mod.Delta; break;
            }
        }

        return new BaseStats(str, dex, con, inte, wil);
    }

    public bool IsAlive => CurrentHp > 0;

    public bool AwaitingBranchChoice => (Level >= 3 && BranchChoice == null) || (Level >= 6 && BranchLevel6 == null);
}

public readonly record struct Equipment(
    string? MainHand,
    string? OffHand,
    string? Armor,
    string? Accessory1,
    string? Accessory2)
{
    public static Equipment Empty => new(null, null, null, null, null);

    /// <summary>Canonical equipment slot names accepted by the equip/unequip protocol.</summary>
    public static readonly string[] SlotNames = { "mainHand", "offHand", "armor", "accessory1", "accessory2" };

    public static bool IsValidSlot(string slot) =>
        SlotNames.Any(s => string.Equals(s, slot, StringComparison.OrdinalIgnoreCase));

    public string? GetSlot(string slot) => slot.ToLowerInvariant() switch
    {
        "mainhand" => MainHand,
        "offhand" => OffHand,
        "armor" => Armor,
        "accessory1" => Accessory1,
        "accessory2" => Accessory2,
        _ => throw new ArgumentException($"Unknown equipment slot: {slot}", nameof(slot)),
    };

    public Equipment WithSlot(string slot, string? value) => slot.ToLowerInvariant() switch
    {
        "mainhand" => this with { MainHand = value },
        "offhand" => this with { OffHand = value },
        "armor" => this with { Armor = value },
        "accessory1" => this with { Accessory1 = value },
        "accessory2" => this with { Accessory2 = value },
        _ => throw new ArgumentException($"Unknown equipment slot: {slot}", nameof(slot)),
    };

    public BaseStats StatBonus(ItemRegistry? items = null)
    {
        if (items is null) return new BaseStats(0, 0, 0, 0, 0);

        var bonus = new BaseStats(0, 0, 0, 0, 0);
        foreach (var slot in new[] { MainHand, OffHand, Armor, Accessory1, Accessory2 })
        {
            if (slot is not null && items.Get(slot) is { } item && item.StatBonus is { } sb)
                bonus += sb;
        }
        return bonus;
    }
}

public record FactionGate(string FactionId, int Threshold);

public record BranchDef(
    string Id,
    string? RequiresBranch = null,
    string? FallbackBranch = null,
    FactionGate? FactionGate = null);

public record ClassDef(
    string Id,
    string Name,
    string Description,
    BaseStats BaseStats,
    AbilityDef[] Abilities,
    LevelTableEntry[] LevelTable,
    string[]? AvailableBranches = null,
    BranchDef[]? Branches = null);

public record AbilityDef(
    string Id,
    string Name,
    AbilityCost Cost,
    AbilityEffect Effect,
    string[] Tags,
    string? RequiredRow = null,
    string? RowChangeCost = null,
    string? Branch = null,
    MemoryCost? MemoryCost = null)
{
    public bool IsAvailableInRow(int row)
        => RequiredRow == null
            || (RequiredRow == "front" && row == 0)
            || (RequiredRow == "back" && row == 1);
}

public record AbilityCost(
    string Type,
    int? Amount);

public record AbilityEffect(
    string Type,
    System.Text.Json.JsonElement? Value,
    string? Range,
    string? Target);

public record LevelTableEntry(
    int Level,
    int HpGain,
    BaseStats StatGain,
    string[] NewAbilities);
