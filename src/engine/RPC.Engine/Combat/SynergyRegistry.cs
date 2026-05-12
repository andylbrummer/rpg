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
}

public record SynergyEffect(
    string Type,
    int Value,
    string? StatusType = null,
    int? StatusDuration = null);
