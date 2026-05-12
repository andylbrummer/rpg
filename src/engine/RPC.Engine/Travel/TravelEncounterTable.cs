using RPC.Engine.Combat;

namespace RPC.Engine.Travel;

public enum TravelResolutionType
{
    Combat,
    StatTest,
    Dialogue
}

public record TravelEncounterEntry(
    string Id,
    string Name,
    int Weight,
    int DangerThreshold,
    TravelResolutionType ResolutionType,
    string? StatName = null,
    string? FactionId = null,
    EnemySpawn[]? Enemies = null);

public record TravelEncounterDef(
    string Id,
    string Name,
    TravelResolutionType ResolutionType,
    string? StatName = null,
    string? FactionId = null,
    EnemySpawn[]? Enemies = null);

public record TravelEncounterState(
    string Id,
    string Name,
    string ResolutionType,
    string? StatName = null,
    string? FactionId = null,
    int ReputationValue = 0,
    bool HasSurpriseRound = true,
    int PriceTier = 0,
    string[]? Options = null);

public static class TravelEncounterTable
{
    public static readonly TravelEncounterEntry[] Entries =
    [
        new("faction_patrol", "Faction Patrol", 8, 2, TravelResolutionType.Dialogue,
            null, "bureau", null),
        new("bureau_patrol", "Bureau Patrol", 6, 2, TravelResolutionType.Dialogue,
            null, "bureau", null),
        new("convocation_patrol", "Convocation Patrol", 6, 2, TravelResolutionType.Dialogue,
            null, "convocation", null),
        new("stillness_patrol", "Stillness Patrol", 6, 2, TravelResolutionType.Dialogue,
            null, "stillness", null),
        new("cartography_patrol", "Cartography Patrol", 6, 2, TravelResolutionType.Dialogue,
            null, "cartography", null),
        new("inkblood_patrol", "Compact Patrol", 6, 2, TravelResolutionType.Dialogue,
            null, "inkblood", null),
        new("bloom_pocket", "Bloom Pocket", 12, 3, TravelResolutionType.StatTest,
            "constitution", null, null),
        new("merchant", "Traveling Merchant", 18, 0, TravelResolutionType.Dialogue),
        new("refugees", "Refugees", 12, 1, TravelResolutionType.Dialogue),
        new("ambush", "Ambush", 16, 3, TravelResolutionType.Combat,
            null, null, [new EnemySpawn("goblin_scavenger", 2), new EnemySpawn("rat", 1)]),
        new("environmental_hazard", "Environmental Hazard", 12, 2, TravelResolutionType.StatTest,
            "dexterity", null, null)
    ];

    public static int RollEncounterCount(GameRandom rng)
    {
        var roll = rng.Roll(1, 100);
        return roll switch
        {
            <= 20 => 0,
            <= 80 => 1,
            _ => 2
        };
    }

    public static TravelEncounterDef? RollEncounter(GameRandom rng, int dangerRating)
    {
        var eligible = Entries.Where(e => e.DangerThreshold <= dangerRating).ToArray();
        if (eligible.Length == 0) return null;

        var totalWeight = eligible.Sum(e => e.Weight);
        var roll = rng.Roll(1, totalWeight);
        var cumulative = 0;
        foreach (var entry in eligible)
        {
            cumulative += entry.Weight;
            if (roll <= cumulative)
            {
                return new TravelEncounterDef(
                    entry.Id,
                    entry.Name,
                    entry.ResolutionType,
                    entry.StatName,
                    entry.FactionId,
                    entry.Enemies);
            }
        }

        var last = eligible[^1];
        return new TravelEncounterDef(
            last.Id,
            last.Name,
            last.ResolutionType,
            last.StatName,
            last.FactionId,
            last.Enemies);
    }
}
