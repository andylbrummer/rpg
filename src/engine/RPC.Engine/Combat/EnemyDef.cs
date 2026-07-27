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
    string? FactionId = null,
    // Creature family, e.g. "bloom" — bloom creatures may mutate mid-combat.
    string? Type = null);

public record LootEntry(string ItemId, double Chance);

public class EnemyRegistry
{
    private readonly Dictionary<string, EnemyDef> _enemies = new();

    /// <summary>
    /// The enemy id a faction's soldiers spawn under. Hostile patrols, reinforcements, and parley
    /// escalations all know which faction they are fighting; they used to spawn a literal
    /// "faction_soldier", which no content file defines, so every one of them was the anonymous
    /// fallback even though bureau_soldier and convocation_soldier were authored for exactly this.
    /// </summary>
    public static string SoldierIdFor(string factionId) => $"{factionId}_soldier";

    /// <summary>
    /// Load one authored enemy under <paramref name="id"/>. A definition that does not parse is
    /// reported rather than skipped: a missing enemy does not fail combat, it silently spawns the
    /// 10 HP unnamed fallback, which looks like a balance problem instead of a content problem.
    /// </summary>
    public void LoadFromJson(string id, string json)
    {
        var def = JsonSerializer.Deserialize<EnemyDef>(json, ContentJsonOptions.CaseInsensitive)
            ?? throw new InvalidOperationException($"Enemy definition '{id}' did not parse into a definition.");

        _enemies[id] = def;
    }

    /// <summary>
    /// Catalog-driven load, so the host reads enemies from whichever content pack it resolved
    /// rather than inferring a directory off the filesystem. Mirrors the other registries'
    /// catalog loaders.
    /// </summary>
    public void LoadFromCatalog(IContentCatalog catalog)
    {
        foreach (var file in catalog.EnumerateFiles("enemies", "*.json"))
        {
            var json = catalog.GetString(file) ?? catalog.GetString($"enemies/{Path.GetFileName(file)}");
            if (json != null)
                LoadFromJson(Path.GetFileNameWithoutExtension(file), json);
        }
    }

    public EnemyDef? Get(string id)
        => _enemies.TryGetValue(id, out var def) ? def : null;

    public IEnumerable<EnemyDef> All => _enemies.Values;
}
