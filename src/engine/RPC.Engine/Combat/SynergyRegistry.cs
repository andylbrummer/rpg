using System.Text.Json;

namespace RPC.Engine.Combat;

/// <summary>
/// Static registry for ability synergies. Order-independent pair lookup.
/// Anti-synergy: Bonewarden + Stillblade — no positive cross-class effects registered.
/// </summary>
public static class SynergyRegistry
{
    private static readonly Dictionary<string, SynergyEffect> _effects = new();

    public static string MakeKey(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a == b)
            return string.Empty;

        return string.Compare(a, b, StringComparison.Ordinal) <= 0
            ? $"{a}|{b}"
            : $"{b}|{a}";
    }

    public static void Register(string a, string b, SynergyEffect effect)
    {
        _effects[MakeKey(a, b)] = effect;
    }

    public static SynergyEffect? Lookup(string a, string b)
    {
        var key = MakeKey(a, b);
        if (string.IsNullOrEmpty(key))
            return null;

        return _effects.TryGetValue(key, out var effect) ? effect : null;
    }

    public static void Clear() => _effects.Clear();

    public static IReadOnlyDictionary<string, SynergyEffect> GetAll() => _effects;

    public static void LoadFromJson(string json)
    {
        var def = JsonSerializer.Deserialize<SynergyDef>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (def is null || def.Anti || def.Abilities.Length != 2)
            return;

        var effect = new SynergyEffect(
            def.Effect.Type,
            def.Effect.Value);

        Register(def.Abilities[0], def.Abilities[1], effect);
    }

    public static void LoadFromDirectory(string directoryPath)
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
    SynergyFieldNotes FieldNotes);

public record SynergyDefEffect(
    string Type,
    int Value,
    string AppliesAfter);

public record SynergyFieldNotes(
    string? DiscoveredBy);
