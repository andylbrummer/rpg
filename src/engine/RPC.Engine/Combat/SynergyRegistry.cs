using System.Text.Json;
using RPC.Engine.Content;

namespace RPC.Engine.Combat;

/// <summary>
/// Instance registry for ability synergies. Order-independent pair lookup.
/// Anti-synergy: Bonewarden + Stillblade — no positive cross-class effects registered.
/// </summary>
public class SynergyRegistry
{
    private readonly Dictionary<string, (string? Id, SynergyEffect Effect, bool Hidden)> _effects = new();

    public static string MakeKey(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a == b)
            return string.Empty;

        return string.Compare(a, b, StringComparison.Ordinal) <= 0
            ? $"{a}|{b}"
            : $"{b}|{a}";
    }

    public void Register(string a, string b, SynergyEffect effect, string? id = null, bool hidden = false)
    {
        _effects[MakeKey(a, b)] = (id, effect, hidden);
    }

    public SynergyEffect? Lookup(string a, string b)
    {
        var key = MakeKey(a, b);
        if (string.IsNullOrEmpty(key))
            return null;

        return _effects.TryGetValue(key, out var entry) ? entry.Effect : null;
    }

    public (string? Id, SynergyEffect Effect)? LookupWithId(string a, string b)
    {
        var key = MakeKey(a, b);
        if (string.IsNullOrEmpty(key))
            return null;

        return _effects.TryGetValue(key, out var entry) ? (entry.Id, entry.Effect) : null;
    }

    public bool IsHidden(string a, string b)
    {
        var key = MakeKey(a, b);
        if (string.IsNullOrEmpty(key))
            return false;
        return _effects.TryGetValue(key, out var entry) && entry.Hidden;
    }

    public bool IsHiddenById(string synergyId)
    {
        return _effects.Values.Any(e => e.Id == synergyId && e.Hidden);
    }

    public void Clear() => _effects.Clear();

    public IReadOnlyDictionary<string, (string? Id, SynergyEffect Effect)> GetAll()
        => _effects.ToDictionary(
            kvp => kvp.Key,
            kvp => (kvp.Value.Id, kvp.Value.Effect));

    public void LoadFromJson(string json)
    {
        var def = JsonSerializer.Deserialize<SynergyDef>(json, ContentJsonOptions.Standard);

        if (def is null || def.Anti || def.Abilities.Length != 2)
            return;

        var effect = new SynergyEffect(
            def.Effect.Type,
            def.Effect.Value);

        Register(def.Abilities[0], def.Abilities[1], effect, def.Id, def.Hidden);
    }

    public void LoadFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return;

        foreach (var file in Directory.EnumerateFiles(directoryPath, "*.json"))
        {
            var json = File.ReadAllText(file);
            LoadFromJson(json);
        }
    }
}

public record SynergyEffect(
    string Type,
    int Value,
    string? StatusType = null,
    int? StatusDuration = null);

public record SynergyDef(
    string Id,
    string[] Abilities,
    bool Anti,
    SynergyDefEffect Effect,
    string Hint,
    SynergyFieldNotes FieldNotes,
    bool Hidden = false);

public record SynergyDefEffect(
    string Type,
    int Value,
    string AppliesAfter);

public record SynergyFieldNotes(
    string? DiscoveredBy);
