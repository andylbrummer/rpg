namespace RPC.Engine.Campaign;

/// <summary>A single validation finding against a generated campaign config.</summary>
public record ConfigIssue(string Category, string Detail);

/// <summary>
/// Comprehensive validation of a (LLM- or roll-) generated <see cref="CampaignConfig"/> across four
/// dimensions: completeness (required fields present), coherence (roles don't collide),
/// completability (the campaign can actually be finished — enough evidence, ordered timelines), and
/// faction consistency (every referenced faction is real and correctly involved). Returns a list of
/// issues so callers can both gate and feed specific critique back into an LLM retry.
/// </summary>
public static class CampaignConfigValidator
{
    /// <summary>
    /// Minimum evidence entries for the mastermind to be exposable. Matches the generation schema,
    /// which asks the LLM for 3-5 evidence clues.
    /// </summary>
    public const int MinEvidenceChain = 3;

    public static List<ConfigIssue> Validate(CampaignConfig config)
    {
        var issues = new List<ConfigIssue>();
        var pool = CampaignConfig.FactionPool;

        // ---- Completeness ----
        if (string.IsNullOrEmpty(config.Patron)) issues.Add(new("completeness", "Missing patron."));
        if (string.IsNullOrEmpty(config.Threat)) issues.Add(new("completeness", "Missing threat."));
        if (string.IsNullOrEmpty(config.Mastermind)) issues.Add(new("completeness", "Missing mastermind."));
        if (string.IsNullOrEmpty(config.WildCard)) issues.Add(new("completeness", "Missing wild card."));
        if (config.EvidenceChain.Count == 0) issues.Add(new("completeness", "Empty evidence chain."));
        else if (config.EvidenceChain.Any(string.IsNullOrWhiteSpace)) issues.Add(new("completeness", "Evidence chain contains empty entries."));
        if (config.FactionTimelines.Count == 0) issues.Add(new("completeness", "No faction timelines."));

        // ---- Faction consistency ----
        foreach (var (label, faction) in new[]
        {
            ("patron", config.Patron), ("threat", config.Threat),
            ("mastermind", config.Mastermind), ("wildCard", config.WildCard),
        })
        {
            if (!string.IsNullOrEmpty(faction) && !pool.Contains(faction))
                issues.Add(new("faction_consistency", $"{label} '{faction}' is not a known faction."));
        }
        foreach (var key in config.FactionTimelines.Keys)
        {
            if (!pool.Contains(key))
                issues.Add(new("faction_consistency", $"Timeline references unknown faction '{key}'."));
        }
        if (!string.IsNullOrEmpty(config.WildCard)
            && new[] { config.Patron, config.Threat, config.Mastermind }.Contains(config.WildCard))
            issues.Add(new("faction_consistency", "Wild card must be uninvolved with the core conflict."));

        // ---- Coherence ----
        if (!string.IsNullOrEmpty(config.Patron) && config.Patron == config.Threat)
            issues.Add(new("coherence", "Patron cannot also be the threat."));
        if (!string.IsNullOrEmpty(config.Threat) && config.Threat == config.Mastermind)
            issues.Add(new("coherence", "Threat cannot also be the mastermind."));
        if (config.NpcCasting.Count > 0 && config.NpcCasting.Values.Distinct().Count() != config.NpcCasting.Count)
            issues.Add(new("coherence", "A role is cast to more than one NPC."));

        // ---- Completability ----
        if (config.EvidenceChain.Count > 0 && config.EvidenceChain.Count < MinEvidenceChain)
            issues.Add(new("completability", $"Evidence chain has {config.EvidenceChain.Count} entries; needs at least {MinEvidenceChain} to expose the mastermind."));
        foreach (var (faction, timeline) in config.FactionTimelines)
        {
            if (timeline.Preparing >= timeline.Executing)
                issues.Add(new("completability", $"Faction '{faction}' timeline: preparing ({timeline.Preparing}) must precede executing ({timeline.Executing})."));
        }
        if (config.WildcardTrigger != null
            && (config.WildcardTrigger.FactionId == config.Threat || config.WildcardTrigger.FactionId == config.Mastermind))
            issues.Add(new("completability", "Wildcard trigger faction cannot be the threat or mastermind."));

        return issues;
    }

    public static bool IsValid(CampaignConfig config) => Validate(config).Count == 0;

    /// <summary>A single critique string suitable for feeding back into an LLM retry prompt.</summary>
    public static string Summarize(IReadOnlyList<ConfigIssue> issues)
        => issues.Count == 0 ? "" : string.Join("; ", issues.Select(i => $"[{i.Category}] {i.Detail}"));
}
