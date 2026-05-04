namespace RPC.Engine.Character;

public readonly record struct BaseStats(
    int Strength,
    int Dexterity,
    int Constitution,
    int Intelligence,
    int Willpower)
{
    public static BaseStats operator +(BaseStats a, BaseStats b)
        => new(
            a.Strength + b.Strength,
            a.Dexterity + b.Dexterity,
            a.Constitution + b.Constitution,
            a.Intelligence + b.Intelligence,
            a.Willpower + b.Willpower);
}

public readonly record struct EffectiveStats(
    int MaxHp,
    int Speed,
    int Accuracy,
    int Evade,
    int Power)
{
    public static EffectiveStats FromBase(BaseStats stats, int level)
        => new(
            MaxHp: stats.Constitution * 3 + level * 2,
            Speed: stats.Dexterity + level / 2,
            Accuracy: stats.Dexterity + level,
            Evade: stats.Dexterity / 2 + level / 2,
            Power: stats.Strength);
}
