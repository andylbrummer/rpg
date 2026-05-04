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
    public EffectiveStats EffectiveStats
        => EffectiveStats.FromBase(BaseStats + Equipment.StatBonus, Level);

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

    public BaseStats StatBonus
    {
        get
        {
            // Equipment bonuses resolved externally via content system.
            // For now, return zero; content loader will hydrate.
            return new BaseStats(0, 0, 0, 0, 0);
        }
    }
}

public record ClassDef(
    string Id,
    string Name,
    BaseStats BaseStats,
    string[] StartingAbilities,
    LevelTableEntry[] LevelTable);

public record LevelTableEntry(
    int Level,
    int HpGain,
    BaseStats StatGain,
    string[] NewAbilities);
