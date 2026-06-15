using RPC.Engine.Character;
using RPC.Engine.Dungeons;

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
    /// Scope is intentionally limited to the references whose registries <see cref="GameState"/>
    /// already carries (class ids and dungeon template ids). Broader content-id validation
    /// (faction / campaign-scheme / complication ids) depends on the content-catalog architecture
    /// being wired into load and is tracked separately.
    /// </summary>
    public static IReadOnlyList<string> CheckContentReferences(
        SaveData data,
        ClassRegistry? classRegistry,
        IReadOnlyDictionary<string, DungeonTemplate>? dungeonTemplates)
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

        return warnings;
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
