using System.Text.Json;
using RPC.Engine.Content;

namespace RPC.Engine.Combat;

/// <summary>
/// Instance registry for ability synergies. Order-independent pair lookup.
/// Anti-synergy: Bonewarden + Stillblade — no positive cross-class effects registered.
/// </summary>
public class SynergyRegistry
{
    private readonly Dictionary<string, (string? Id, SynergyEffect Effect, bool Hidden, string? Environment)> _effects = new();

    public static string MakeKey(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a == b)
            return string.Empty;

        return string.Compare(a, b, StringComparison.Ordinal) <= 0
            ? $"{a}|{b}"
            : $"{b}|{a}";
    }

    /// <summary>
    /// Index an effect under its order-independent pair key. A pair that cannot form a key — an
    /// empty ability id, or an ability paired with itself — is rejected rather than stored: it used
    /// to land under the empty-string key, where <see cref="Lookup"/> could never reach it and the
    /// next bad pair overwrote it.
    /// </summary>
    public void Register(string a, string b, SynergyEffect effect, string? id = null, bool hidden = false, string? environment = null)
    {
        var key = MakeKey(a, b);
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException(
                $"Synergy '{id ?? "(no id)"}' pairs '{a}' with '{b}', which is not two distinct ability ids, so it could never be looked up.",
                nameof(a));

        _effects[key] = (id, effect, hidden, environment);
    }

    public SynergyEffect? Lookup(string a, string b)
    {
        var key = MakeKey(a, b);
        if (string.IsNullOrEmpty(key))
            return null;

        return _effects.TryGetValue(key, out var entry) ? entry.Effect : null;
    }

    public (string? Id, SynergyEffect Effect)? LookupWithId(string a, string b, string? environment = null)
    {
        var key = MakeKey(a, b);
        if (string.IsNullOrEmpty(key))
            return null;

        if (!_effects.TryGetValue(key, out var entry))
            return null;

        // Environment-gated synergies only trigger in their dungeon; ungated ones trigger anywhere.
        if (!string.IsNullOrEmpty(entry.Environment) && !string.Equals(entry.Environment, environment, StringComparison.OrdinalIgnoreCase))
            return null;

        return (entry.Id, entry.Effect);
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

    /// <summary>
    /// Load one authored synergy. <paramref name="source"/> names the file in error messages.
    /// A definition that cannot be registered is reported, not dropped: a silently skipped synergy
    /// looks exactly like a pair the designers chose not to give an effect.
    /// </summary>
    public void LoadFromJson(string json, string? source = null)
    {
        var where = source is null ? "A synergy definition" : $"Synergy definition '{source}'";
        var def = JsonSerializer.Deserialize<SynergyDef>(json, ContentJsonOptions.Standard)
            ?? throw new InvalidOperationException($"{where} did not parse into a definition.");

        // Anti-synergies are authored to record that a pair deliberately does nothing. There is no
        // effect to index, so skipping one is the intended outcome rather than a lost definition.
        if (def.Anti)
            return;

        if (def.Abilities is not { Length: 2 })
            throw new InvalidOperationException(
                $"{where} lists {def.Abilities?.Length ?? 0} abilities; a synergy pairs exactly two.");

        var effect = new SynergyEffect(
            def.Effect.Type,
            def.Effect.Value);

        Register(def.Abilities[0], def.Abilities[1], effect, def.Id, def.Hidden, def.Environment);
    }

    public void LoadFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return;

        foreach (var file in Directory.EnumerateFiles(directoryPath, "*.json"))
        {
            var json = File.ReadAllText(file);
            LoadFromJson(json, Path.GetFileName(file));
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
    bool Hidden = false,
    // When set, the synergy only triggers in this dungeon type (environmental secret synergy).
    string? Environment = null);

public record SynergyDefEffect(
    string Type,
    int Value,
    string AppliesAfter);

public record SynergyFieldNotes(
    string? DiscoveredBy);
