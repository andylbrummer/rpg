using System.Text.Json;
using RPC.Engine.Character;
using RPC.Engine.Content;

namespace RPC.Engine.Combat;

public record EnemyDef(
    string Id,
    string Name,
    string Description,
    BaseStats Stats,
    int HpBase,
    int Speed,
    string Ai,
    string[] Abilities,
    LootEntry[] LootTable,
    string? FactionId = null);

public record LootEntry(string ItemId, double Chance);

public class EnemyRegistry
{
    private readonly Dictionary<string, EnemyDef> _enemies = new();

    public void LoadFromJson(string id, string json)
    {
        var def = JsonSerializer.Deserialize<EnemyDef>(json, ContentJsonOptions.CaseInsensitive);
        if (def is not null)
            _enemies[id] = def;
    }

    public EnemyDef? Get(string id)
        => _enemies.TryGetValue(id, out var def) ? def : null;

    public IEnumerable<EnemyDef> All => _enemies.Values;
}
