namespace RPC.Engine.Character;

public static class LevelingSystem
{
    // XP thresholds for each level (cumulative)
    private static readonly int[] XpThresholds = new[]
    {
        0,    // L1 (already there)
        50,   // L2
        120,  // L3
        220,  // L4
        350,  // L5
        500,  // L6
        700,  // L7
        950,  // L8
        1250, // L9
        1600  // L10
    };

    public static int XpForNextLevel(int currentLevel)
    {
        if (currentLevel >= XpThresholds.Length) return int.MaxValue;
        return XpThresholds[currentLevel];
    }

    public static bool CanLevelUp(CharacterState character)
    {
        return character.Xp >= XpForNextLevel(character.Level);
    }

    public static CharacterState ApplyLevelUp(CharacterState character, ClassDef classDef)
    {
        var newLevel = character.Level + 1;
        var levelEntry = classDef.LevelTable.FirstOrDefault(l => l.Level == newLevel);

        if (levelEntry == null)
            return character; // No level data available

        var newStats = character.BaseStats + levelEntry.StatGain;
        // No new max HP to compute: MaxHp is derived from Constitution and level rather than
        // stored, so raising the stats above raises it. The local this replaced was never read.
        var newHp = character.CurrentHp + levelEntry.HpGain;
        var newAbilities = character.KnownAbilities
            .Concat(levelEntry.NewAbilities)
            .Distinct()
            .ToArray();

        return character with
        {
            Level = newLevel,
            BaseStats = newStats,
            CurrentHp = newHp,
            KnownAbilities = newAbilities
        };
    }

    public static CharacterState CheckAndApplyLevelUps(CharacterState character, ClassDef classDef)
    {
        var current = character;
        while (CanLevelUp(current))
        {
            var next = ApplyLevelUp(current, classDef);
            // No level-table entry for the next level: ApplyLevelUp returns the character unchanged.
            // Stop rather than spin forever (CanLevelUp would stay true on the unchanged level).
            if (next.Level == current.Level) break;
            current = next;
        }
        return current;
    }
}
