using System.Text.Json;
using RPC.Engine.Combat;
using RPC.Engine.Content;

namespace RPC.Engine.Dungeons;

public record DungeonLootEntry(string ItemId, int Weight);

public record DungeonLootTableDef(string Id, DungeonLootEntry[] Entries)
{
    /// <summary>
    /// Roll one item id by cumulative weight, or null if the table is empty / total weight &lt;= 0.
    /// Single source of truth for the weighted selection; consumes exactly one roll from <paramref name="rng"/>.
    /// </summary>
    public string? Pick(GameRandom rng)
    {
        if (Entries.Length == 0) return null;
        var total = Entries.Sum(e => e.Weight);
        if (total <= 0) return null;
        var roll = rng.Roll(1, total);
        var cumulative = 0;
        foreach (var entry in Entries)
        {
            cumulative += entry.Weight;
            if (roll <= cumulative) return entry.ItemId;
        }
        return Entries[^1].ItemId;
    }
}

public class DungeonLootTableRegistry
{
    private readonly Dictionary<string, DungeonLootTableDef> _tables = new();

    public void LoadFromJson(string id, string json)
    {
        var def = JsonSerializer.Deserialize<DungeonLootTableDef>(json, ContentJsonOptions.CaseInsensitive);
        if (def is not null)
            _tables[id] = def;
    }

    public DungeonLootTableDef? Get(string id)
        => _tables.TryGetValue(id, out var def) ? def : null;

    /// <summary>Roll one item id from the table, or null if the table is missing/empty.</summary>
    public string? Roll(string id, GameRandom rng)
        => Get(id)?.Pick(rng);
}
