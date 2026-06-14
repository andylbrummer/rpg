using System.Text.Json;
using RPC.Engine.Combat;
using RPC.Engine.Content;

namespace RPC.Engine.Dungeons;

public record DungeonLootEntry(string ItemId, int Weight);

public record DungeonLootTableDef(string Id, DungeonLootEntry[] Entries);

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
    {
        var table = Get(id);
        if (table is null || table.Entries.Length == 0) return null;

        var total = table.Entries.Sum(e => e.Weight);
        if (total <= 0) return null;
        var roll = rng.Roll(1, total);
        var cumulative = 0;
        foreach (var entry in table.Entries)
        {
            cumulative += entry.Weight;
            if (roll <= cumulative) return entry.ItemId;
        }
        return table.Entries[^1].ItemId;
    }
}
