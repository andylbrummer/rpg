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
    EnemySpawn[]? Enemies = null,
    string? ClassAlternative = null);

public record TravelEncounterDef(
    string Id,
    string Name,
    TravelResolutionType ResolutionType,
    string? StatName = null,
    string? FactionId = null,
    EnemySpawn[]? Enemies = null,
    string? ClassAlternative = null);

public record TravelEncounterState(
    string Id,
    string Name,
    string ResolutionType,
    string? StatName = null,
    string? FactionId = null,
    int ReputationValue = 0,
    bool HasSurpriseRound = true,
    int PriceTier = 0,
    string[]? Options = null,
    string? ClassAlternative = null);

public static class TravelEncounterTable
{
    private static readonly TravelEncounterEntry[] BaseEncounters =
    [
        new("faction_patrol", "Faction Patrol", 8, 2, TravelResolutionType.Dialogue, null, "bureau", null),
        new("bureau_patrol", "Bureau Patrol", 4, 2, TravelResolutionType.Dialogue, null, "bureau", null),
        new("convocation_patrol", "Convocation Patrol", 4, 2, TravelResolutionType.Dialogue, null, "convocation", null),
        new("stillness_patrol", "Stillness Patrol", 4, 2, TravelResolutionType.Dialogue, null, "stillness", null),
        new("cartography_patrol", "Cartography Patrol", 4, 2, TravelResolutionType.Dialogue, null, "cartography", null),
        new("inkblood_patrol", "Compact Patrol", 4, 2, TravelResolutionType.Dialogue, null, "inkblood", null),
        new("merchant", "Traveling Merchant", 14, 0, TravelResolutionType.Dialogue),
        new("refugees", "Refugees", 10, 1, TravelResolutionType.Dialogue),
        new("ambush", "Ambush", 12, 3, TravelResolutionType.Combat, null, null, [new EnemySpawn("goblin_scavenger", 2), new EnemySpawn("rat", 1)]),
        new("environmental_hazard", "Environmental Hazard", 8, 2, TravelResolutionType.StatTest, "dexterity", null, null)
    ];

    private static readonly Dictionary<string, TravelEncounterEntry[]> TerrainTables = new()
    {
        ["plains"] = BaseEncounters.Concat(new TravelEncounterEntry[]
        {
            new("bandits", "Bandit Ambush", 10, 2, TravelResolutionType.Combat, null, null, [new EnemySpawn("bandit", 2)]),
            new("wandering_monk", "Wandering Monk", 6, 0, TravelResolutionType.Dialogue),
        }).ToArray(),

        ["forest"] = BaseEncounters.Concat(new TravelEncounterEntry[]
        {
            new("forest_ambush", "Forest Ambush", 12, 3, TravelResolutionType.Combat, null, null, [new EnemySpawn("wolf", 2), new EnemySpawn("spider", 1)]),
            new("lost_traveler", "Lost Traveler", 8, 1, TravelResolutionType.Dialogue),
            new("poison_thicket", "Poison Thicket", 10, 2, TravelResolutionType.StatTest, "constitution", null, null, "cauterist_scorcher"),
        }).ToArray(),

        ["mountain"] = BaseEncounters.Concat(new TravelEncounterEntry[]
        {
            new("rockslide", "Rockslide", 12, 2, TravelResolutionType.StatTest, "dexterity", null, null, "marcher_pathfinder"),
            new("mountain_beasts", "Mountain Beasts", 12, 3, TravelResolutionType.Combat, null, null, [new EnemySpawn("wolf", 2), new EnemySpawn("bear", 1)]),
            new("hermit", "Mountain Hermit", 6, 0, TravelResolutionType.Dialogue),
            new("narrow_pass", "Narrow Pass", 8, 2, TravelResolutionType.StatTest, "strength", null, null, "marcher_pathfinder"),
        }).ToArray(),

        ["caves"] = BaseEncounters.Concat(new TravelEncounterEntry[]
        {
            new("cave_in", "Cave-In", 12, 2, TravelResolutionType.StatTest, "dexterity", null, null, "marcher_pathfinder"),
            new("goblin_tunnelers", "Goblin Tunnelers", 14, 3, TravelResolutionType.Combat, null, null, [new EnemySpawn("goblin_scavenger", 3)]),
            new("fungal_spores", "Fungal Spores", 10, 2, TravelResolutionType.StatTest, "constitution", null, null, "cauterist_scorcher"),
            new("smuggler_cache", "Smuggler Cache", 8, 1, TravelResolutionType.Dialogue, null, null, null, "hollow_liar"),
        }).ToArray(),

        ["marsh"] = BaseEncounters.Concat(new TravelEncounterEntry[]
        {
            new("sinking_mire", "Sinking Mire", 10, 2, TravelResolutionType.StatTest, "strength", null, null, "marcher_pathfinder"),
            new("marsh_dwellers", "Marsh Dwellers", 12, 3, TravelResolutionType.Combat, null, null, [new EnemySpawn("lizardfolk", 2), new EnemySpawn("snake", 1)]),
            new("will_o_wisp", "Will-o'-Wisp", 8, 1, TravelResolutionType.StatTest, "willpower", null, null, "cauterist_scorcher"),
            new("bog_merchant", "Bog Merchant", 8, 1, TravelResolutionType.Dialogue, null, null, null, "ashmouth_broker"),
        }).ToArray(),

        ["default"] = BaseEncounters.Concat(new TravelEncounterEntry[]
        {
            new("bloom_pocket", "Bloom Pocket", 10, 3, TravelResolutionType.StatTest, "constitution", null, null)
        }).ToArray()
    };

    public static int RollEncounterCount(GameRandom rng, int dangerRating)
    {
        var roll = rng.Roll(1, 100);
        return dangerRating switch
        {
            <= 1 => roll switch
            {
                <= 60 => 0,
                <= 95 => 1,
                _ => 2
            },
            <= 3 => roll switch
            {
                <= 20 => 0,
                <= 80 => 1,
                _ => 2
            },
            _ => roll switch
            {
                <= 5 => 0,
                <= 50 => 1,
                _ => 2
            }
        };
    }

    public static TravelEncounterDef? RollEncounter(GameRandom rng, int dangerRating, string terrain)
    {
        var table = TerrainTables.GetValueOrDefault(terrain?.ToLowerInvariant() ?? "", TerrainTables["default"]);
        var eligible = table.Where(e => e.DangerThreshold <= dangerRating).ToArray();
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
                    entry.Enemies,
                    entry.ClassAlternative);
            }
        }

        var last = eligible[^1];
        return new TravelEncounterDef(
            last.Id,
            last.Name,
            last.ResolutionType,
            last.StatName,
            last.FactionId,
            last.Enemies,
            last.ClassAlternative);
    }

    public static string[] GetTerrainTypes() => TerrainTables.Keys.Where(k => k != "default").ToArray();
}
