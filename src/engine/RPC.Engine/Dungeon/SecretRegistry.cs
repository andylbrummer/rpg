using System.Text.Json;
using RPC.Engine.Content;

namespace RPC.Engine.Dungeons;

/// <summary>
/// A hidden location/feature the party can uncover. <see cref="DocLinkId"/> ties the secret to a
/// lore document — reading that document passively reveals the secret (the puzzle path).
/// <see cref="BloodlineRequirement"/>, when set, gates discovery behind the party's family name:
/// only a party whose <c>CampaignState.FamilyName</c> matches (case-insensitive) may uncover it.
/// <para>
/// Spatial fields (<see cref="X"/>, <see cref="Y"/>, <see cref="Wall"/>) place a secret on a
/// dungeon tile. <see cref="Wall"/> names the border direction ("North"/"South"/"East"/"West")
/// carrying a <c>BorderType.BreakableWall</c>. These drive the Cartographer 2-tile auto-detection,
/// explicit-search reveal, and the break action. They are content-supplied and rebuilt from the
/// registry on every load, so they are not persisted in the save.
/// </para>
/// </summary>
public record SecretDef(
    string Id,
    string Type,
    string? DocLinkId = null,
    string? Hint = null,
    string? BloodlineRequirement = null,
    int? X = null,
    int? Y = null,
    string? Wall = null);

/// <summary>
/// Per-run registry of secret definitions, indexed by id and by the document that hints at them.
/// Mirrors the SynergyRegistry pattern — content is loaded per dungeon/campaign, discovery state
/// lives in the campaign Journal.
/// </summary>
public class SecretRegistry
{
    private readonly Dictionary<string, SecretDef> _byId = new();
    private readonly Dictionary<string, List<string>> _byDocLink = new();

    public void Register(SecretDef secret)
    {
        if (string.IsNullOrEmpty(secret.Id)) return;
        _byId[secret.Id] = secret;

        if (!string.IsNullOrEmpty(secret.DocLinkId))
        {
            if (!_byDocLink.TryGetValue(secret.DocLinkId, out var list))
            {
                list = new List<string>();
                _byDocLink[secret.DocLinkId] = list;
            }
            if (!list.Contains(secret.Id))
                list.Add(secret.Id);
        }
    }

    public SecretDef? Get(string id) => _byId.TryGetValue(id, out var s) ? s : null;

    /// <summary>Secret ids hinted at by the given document. Empty when the document links nothing.</summary>
    public IReadOnlyList<string> SecretsForDocument(string docLinkId)
        => _byDocLink.TryGetValue(docLinkId, out var list) ? list : Array.Empty<string>();

    public IReadOnlyCollection<SecretDef> All => _byId.Values;

    public void Clear()
    {
        _byId.Clear();
        _byDocLink.Clear();
    }

    public void LoadFromJson(string json)
    {
        var def = JsonSerializer.Deserialize<SecretDef>(json, ContentJsonOptions.Standard);
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
    /// Catalog-driven load, so the host reads secrets from whichever content pack it resolved
    /// rather than inferring a directory off the filesystem. Mirrors the other registries'
    /// catalog loaders.
    /// </summary>
    public void LoadFromCatalog(IContentCatalog catalog)
    {
        foreach (var file in catalog.EnumerateFiles("secrets", "*.json"))
        {
            var json = catalog.GetString(file) ?? catalog.GetString($"secrets/{Path.GetFileName(file)}");
            if (json != null)
                LoadFromJson(json);
        }
    }
}
