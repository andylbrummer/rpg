using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Dungeons;

/// <summary>
/// Content-defined view over the loaded dungeon templates. Owns three concerns the host used to
/// hard-code:
/// <list type="bullet">
///   <item>validating each template against the loaded segments + encounter tables;</item>
///   <item>deriving the segment directories the host should watch for hot reload
///   (<see cref="SegmentDirectories"/>), instead of a single hard-coded broken-engine path;</item>
///   <item>mapping a changed directory back to the templates it feeds
///   (<see cref="TemplatesForDirectory"/>), so a reload can report the affected dungeon.</item>
/// </list>
/// Keeps dungeon content ownership out of the host's composition/request code.
/// </summary>
public sealed class DungeonContentSet
{
    private readonly IReadOnlyDictionary<string, DungeonTemplate> _templates;

    public DungeonContentSet(IReadOnlyDictionary<string, DungeonTemplate> templates)
    {
        _templates = templates ?? throw new ArgumentNullException(nameof(templates));
    }

    public IReadOnlyDictionary<string, DungeonTemplate> Templates => _templates;

    /// <summary>
    /// Distinct, content-defined segment directories declared by templates. These are the paths
    /// the host's hot-reload watcher should observe.
    /// </summary>
    public IReadOnlyList<string> SegmentDirectories => _templates.Values
        .Select(t => t.SegmentDirectory)
        .Where(d => !string.IsNullOrWhiteSpace(d))
        .Select(d => d!.TrimEnd('/'))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(d => d, StringComparer.Ordinal)
        .ToList();

    /// <summary>Templates fed by the given segment directory (case/trailing-slash insensitive).</summary>
    public IReadOnlyList<DungeonTemplate> TemplatesForDirectory(string directory)
    {
        var normalized = (directory ?? string.Empty).TrimEnd('/');
        return _templates.Values
            .Where(t => !string.IsNullOrWhiteSpace(t.SegmentDirectory)
                && string.Equals(t.SegmentDirectory!.TrimEnd('/'), normalized, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// Validate every template against the loaded content. Fail-fast: throws
    /// <see cref="InvalidOperationException"/> aggregating all problems so a bad content pack
    /// cannot start the host with silently broken dungeons.
    /// </summary>
    public void Validate(IReadOnlyCollection<RoomSegment> segments, EncounterTableRegistry encounterTables)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(encounterTables);

        var segmentIds = segments.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var errors = new List<string>();

        foreach (var (key, template) in _templates)
        {
            if (string.IsNullOrWhiteSpace(template.Name))
                errors.Add($"Dungeon template '{key}' has no display name.");

            if (string.IsNullOrWhiteSpace(template.SegmentDirectory))
                errors.Add($"Dungeon template '{key}' declares no segment directory (watcher path).");

            if (template.SegmentPool is null || template.SegmentPool.Length == 0)
            {
                errors.Add($"Dungeon template '{key}' has an empty segment pool.");
            }
            else
            {
                foreach (var segId in template.SegmentPool)
                {
                    if (!segmentIds.Contains(segId))
                        errors.Add($"Dungeon template '{key}' references unknown segment id '{segId}'.");
                }
            }

            if (string.IsNullOrWhiteSpace(template.EncounterTableId))
                errors.Add($"Dungeon template '{key}' has no encounter table id.");
            else if (encounterTables.Get(template.EncounterTableId) is null)
                errors.Add($"Dungeon template '{key}' references unknown encounter table '{template.EncounterTableId}'.");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Invalid dungeon content:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }
}
