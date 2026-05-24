namespace RPC.Engine.Campaign;

/// <summary>
/// Canonical settlement fates tracked by the campaign. Stored as plain strings in
/// <see cref="WorldState.Settlements"/> so existing saves and the world-state presenter
/// keep working; <see cref="Normalize"/> maps any legacy/free-form value onto a bucket.
/// </summary>
public static class SettlementFate
{
    public const string Contested = "contested";
    public const string Saved = "saved";
    public const string Lost = "lost";
    public const string Abandoned = "abandoned";

    /// <summary>Fates that represent a decided outcome — never re-rolled.</summary>
    public static readonly string[] Terminal = { Saved, Lost, Abandoned };

    public static bool IsValid(string? fate) => Normalize(fate) is Saved or Lost or Abandoned or Contested
        && (fate is not null);

    public static bool IsTerminal(string? fate) => Array.IndexOf(Terminal, Normalize(fate)) >= 0;

    /// <summary>
    /// Collapse a stored fate string onto a canonical bucket. Unknown or empty values are
    /// treated as <see cref="Contested"/>; legacy values written by other systems map on:
    /// "destroyed" → Lost, "changed" → Contested.
    /// </summary>
    public static string Normalize(string? fate)
    {
        if (string.IsNullOrWhiteSpace(fate)) return Contested;
        return fate.Trim().ToLowerInvariant() switch
        {
            Saved => Saved,
            Lost or "destroyed" => Lost,
            Abandoned => Abandoned,
            Contested or "changed" => Contested,
            _ => Contested
        };
    }
}
