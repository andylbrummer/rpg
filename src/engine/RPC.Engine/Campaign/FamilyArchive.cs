using System.Text.Json;
using RPC.Engine.Content;

namespace RPC.Engine.Campaign;

/// <summary>
/// A Family Archive — an interactable record-object (Compact signature content). Reading one grants
/// faction intel: reputation, evidence toward the faction's case, and/or a journal entry. Modelled
/// like a lore document but carrying explicit intel rewards rather than only revealing secrets.
/// </summary>
public record FamilyArchiveDef(
    string Id,
    string FactionId,
    int RepReward = 0,
    int EvidenceReward = 0,
    string? JournalEntryId = null,
    string? Name = null,
    string? Description = null);

/// <summary>Outcome of reading a Family Archive — the intel granted on the first read.</summary>
public record ArchiveReadResult(
    string ArchiveId,
    string FactionId,
    int RepReward,
    int EvidenceReward,
    string? JournalEntryId);

/// <summary>
/// Per-run registry of Family Archive definitions, indexed by id. Mirrors the SecretRegistry
/// pattern — content is loaded per dungeon/campaign; which archives have been read lives in the
/// campaign's document-read tracking set.
/// </summary>
public class ArchiveRegistry
{
    private readonly Dictionary<string, FamilyArchiveDef> _byId = new();

    public void Register(FamilyArchiveDef archive)
    {
        if (string.IsNullOrEmpty(archive.Id)) return;
        _byId[archive.Id] = archive;
    }

    public FamilyArchiveDef? Get(string id) => _byId.TryGetValue(id, out var a) ? a : null;

    public IReadOnlyCollection<FamilyArchiveDef> All => _byId.Values;

    public void Clear() => _byId.Clear();

    public void LoadFromJson(string json)
    {
        var def = JsonSerializer.Deserialize<FamilyArchiveDef>(json, ContentJsonOptions.Standard);
        if (def is null || string.IsNullOrEmpty(def.Id))
            return;
        Register(def);
    }

    public void LoadFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return;
        foreach (var file in Directory.EnumerateFiles(directoryPath, "*.json"))
            LoadFromJson(File.ReadAllText(file));
    }

    /// <summary>
    /// Catalog-driven load, so the host reads archives from whichever content pack it resolved
    /// rather than inferring a directory off the filesystem. Mirrors the other registries'
    /// catalog loaders.
    /// </summary>
    public void LoadFromCatalog(IContentCatalog catalog)
    {
        foreach (var file in catalog.EnumerateFiles("archives", "*.json"))
        {
            var json = catalog.GetString(file) ?? catalog.GetString($"archives/{Path.GetFileName(file)}");
            if (json != null)
                LoadFromJson(json);
        }
    }
}
