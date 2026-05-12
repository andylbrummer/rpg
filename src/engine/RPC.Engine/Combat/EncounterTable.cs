using System.Text.Json;

namespace RPC.Engine.Combat;

public record EncounterTableEntry(string? Id, int Weight, EnemySpawn[] Enemies, int XpReward = 10, int DangerRating = 1);

public record EncounterTableDef(string Id, string Name, EncounterTableEntry[] Entries);

public class EncounterTableRegistry
{
    private readonly Dictionary<string, EncounterTableDef> _tables = new();

    public void LoadFromJson(string id, string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var def = JsonSerializer.Deserialize<EncounterTableDef>(json, options);
        if (def is not null)
            _tables[id] = def;
    }

    public EncounterTableDef? Get(string id)
        => _tables.TryGetValue(id, out var def) ? def : null;

    public EncounterDef? GetEncounterById(string encounterId)
    {
        foreach (var table in _tables.Values)
        {
            foreach (var entry in table.Entries)
            {
                if (entry.Id == encounterId)
                    return new EncounterDef(encounterId, table.Name, entry.Enemies, entry.XpReward);
            }
        }
        return null;
    }

    public EncounterDef RollEncounter(string id, GameRandom rng, int dangerRating = 5)
    {
        var table = Get(id);
        if (table == null || table.Entries.Length == 0)
            return new EncounterDef("default", "Default", Array.Empty<EnemySpawn>(), 0);

        var eligible = table.Entries.Where(e => e.DangerRating <= dangerRating).ToArray();
        if (eligible.Length == 0)
            eligible = table.Entries;

        var totalWeight = eligible.Sum(e => e.Weight);
        var roll = rng.Roll(1, totalWeight);

        var cumulative = 0;
        foreach (var entry in eligible)
        {
            cumulative += entry.Weight;
            if (roll <= cumulative)
            {
                return new EncounterDef(entry.Id ?? $"{id}_encounter", table.Name, entry.Enemies, entry.XpReward);
            }
        }

        // Fallback to last entry
        var last = eligible[^1];
        return new EncounterDef(last.Id ?? $"{id}_encounter", table.Name, last.Enemies, last.XpReward);
    }
}
