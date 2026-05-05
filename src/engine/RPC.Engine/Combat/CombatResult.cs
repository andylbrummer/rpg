namespace RPC.Engine.Combat;

public record CombatResult(
    bool Victory,
    int XpGained,
    string[] LevelUps,
    int RoundCount);
