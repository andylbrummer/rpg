using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Dungeons;
using RPC.Engine.Town;

namespace RPC.Engine.Save;

/// <summary>
/// Minimal public contract describing a save's identity and version, independent
/// of the full restore payload. Extracted from <see cref="SaveData"/> so callers can
/// inspect compatibility without depending on every feature DTO.
/// </summary>
public readonly record struct SaveMetadata(int SchemaVersion, string? ContentHash)
{
    public static SaveMetadata From(SaveData data) => new(data.SchemaVersion, data.ContentHash);
}

/// <summary>
/// Compatibility checks surfaced when loading a save. Returns human-readable
/// warnings; an empty list means the save is fully compatible with the running build.
/// </summary>
public static class SaveCompatibility
{
    public static IReadOnlyList<string> CheckContentHash(SaveMetadata metadata, string? expectedContentHash)
    {
        if (string.IsNullOrEmpty(expectedContentHash) || metadata.ContentHash == expectedContentHash)
            return Array.Empty<string>();

        return new[]
        {
            $"Content hash mismatch: save was created with '{metadata.ContentHash ?? "(none)"}', " +
            $"current is '{expectedContentHash}'. Loading anyway."
        };
    }

    /// <summary>
    /// Validates that content ids referenced by a save resolve against the currently-loaded
    /// content registries, returning a human-readable warning per unresolved reference.
    ///
    /// Validation is <em>fail-open</em>: a registry that is null or empty means the running
    /// build has no catalog to validate against (common in tests and lightweight hosts), so the
    /// corresponding references are skipped rather than reported as broken. Warnings never block
    /// loading — an unresolved reference degrades the save but does not make it unloadable.
    ///
    /// Scope covers the references whose registries <see cref="GameState"/> carries: class ids,
    /// dungeon template ids, faction ids, campaign-scheme ids, and complication ids. Each registry
    /// is an independent extension point — supply only the ones the running build has loaded.
    /// </summary>
    public static IReadOnlyList<string> CheckContentReferences(
        SaveData data,
        ClassRegistry? classRegistry,
        IReadOnlyDictionary<string, DungeonTemplate>? dungeonTemplates,
        FactionContentRepository? factionContent = null,
        CampaignContentRegistry? campaignContent = null)
    {
        var warnings = new List<string>();

        if (classRegistry is not null && classRegistry.All.Any())
        {
            foreach (var classId in EnumerateClassIds(data))
            {
                if (classRegistry.Get(classId) is null)
                    warnings.Add($"Save references unknown class id '{classId}'; that character may not load correctly.");
            }
        }

        if (dungeonTemplates is { Count: > 0 })
        {
            if (!string.IsNullOrEmpty(data.DungeonType) && !dungeonTemplates.ContainsKey(data.DungeonType))
                warnings.Add($"Save references unknown dungeon type '{data.DungeonType}'.");

            foreach (var node in data.OverworldNodes ?? Array.Empty<SaveOverworldNode>())
            {
                if (!string.IsNullOrEmpty(node.DungeonTemplateId) && !dungeonTemplates.ContainsKey(node.DungeonTemplateId))
                    warnings.Add($"Overworld node '{node.Id}' references unknown dungeon template '{node.DungeonTemplateId}'.");
            }
        }

        if (factionContent is { Definitions.Count: > 0 })
        {
            var known = new HashSet<string>(factionContent.Definitions.Select(d => d.Id));
            foreach (var factionId in EnumerateFactionIds(data).Distinct())
            {
                if (!known.Contains(factionId))
                    warnings.Add($"Save references unknown faction id '{factionId}'.");
            }
        }

        if (campaignContent is not null && data.CampaignConfig is { } config)
        {
            if (!string.IsNullOrEmpty(config.Scheme) && campaignContent.GetSchemeById(config.Scheme) is null)
                warnings.Add($"Save references unknown campaign scheme id '{config.Scheme}'.");

            if (!string.IsNullOrEmpty(config.Complication) && campaignContent.GetComplicationById(config.Complication) is null)
                warnings.Add($"Save references unknown complication id '{config.Complication}'.");
        }

        return warnings;
    }

    private static IEnumerable<string> EnumerateFactionIds(SaveData data)
    {
        if (!string.IsNullOrEmpty(data.AccusedFaction))
            yield return data.AccusedFaction;
        if (!string.IsNullOrEmpty(data.SuspectedFaction))
            yield return data.SuspectedFaction;

        foreach (var factionId in data.Reputation.Keys)
            yield return factionId;
        foreach (var factionId in data.Evidence.Keys)
            yield return factionId;

        if (data.CampaignConfig is { } config)
        {
            if (!string.IsNullOrEmpty(config.Patron)) yield return config.Patron;
            if (!string.IsNullOrEmpty(config.Threat)) yield return config.Threat;
            if (!string.IsNullOrEmpty(config.Mastermind)) yield return config.Mastermind;
            if (!string.IsNullOrEmpty(config.WildCard)) yield return config.WildCard;
            if (config.WildcardTrigger is { } trigger && !string.IsNullOrEmpty(trigger.FactionId))
                yield return trigger.FactionId;
            foreach (var factionId in config.FactionTimelines.Keys)
                yield return factionId;
        }
    }

    private static IEnumerable<string> EnumerateClassIds(SaveData data)
    {
        foreach (var member in data.Party ?? Array.Empty<SavePartyMember?>())
        {
            if (member is { } m && !string.IsNullOrEmpty(m.ClassId))
                yield return m.ClassId;
        }
        foreach (var dead in data.DeadCharacters ?? Array.Empty<SavePartyMember>())
        {
            if (!string.IsNullOrEmpty(dead.ClassId))
                yield return dead.ClassId;
        }
    }
}
