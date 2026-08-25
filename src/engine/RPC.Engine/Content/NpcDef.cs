namespace RPC.Engine.Content;

public record NpcDef(
    string Id,
    string Name,
    string Description,
    string Type,
    int Level,
    NpcStats Stats,
    string[] Abilities,
    string Ai,
    string[] Loot);

public record NpcStats(
    int Hp,
    int Strength,
    int Dexterity,
    int Speed);
