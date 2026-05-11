using RPC.Engine.Content;

namespace RPC.Engine.Character;

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
    int Row) // 0 = front, 1 = back
{
    public EffectiveStats GetEffectiveStats(ItemRegistry? items = null)
        => EffectiveStats.FromBase(BaseStats + Equipment.StatBonus(items), Level);

    public bool IsAlive => CurrentHp > 0;
}

public readonly record struct Equipment(
    string? MainHand,
    string? OffHand,
    string? Armor,
    string? Accessory1,
    string? Accessory2)
{
    public static Equipment Empty => new(null, null, null, null, null);

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

public record ClassDef(
    string Id,
    string Name,
    string Description,
    BaseStats BaseStats,
    AbilityDef[] Abilities,
    LevelTableEntry[] LevelTable);

public record AbilityDef(
    string Id,
    string Name,
    AbilityCost Cost,
    AbilityEffect Effect,
    string[] Tags,
    string? RequiredRow = null,
    string? RowChangeCost = null)
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
