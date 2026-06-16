using System.Text.Json;
using RPC.Engine.Content;

namespace RPC.Engine.Dungeons;

/// <summary>
/// A hidden location/feature the party can uncover. <see cref="DocLinkId"/> ties the secret to a
/// lore document — reading that document passively reveals the secret (the puzzle path).
/// <see cref="BloodlineRequirement"/>, when set, gates discovery behind the party's family name:
/// only a party whose <c>CampaignState.FamilyName</c> matches (case-insensitive) may uncover it.
/// </summary>
public record SecretDef(
    string Id,
    string Type,
    string? DocLinkId = null,
    string? Hint = null,
    string? BloodlineRequirement = null);

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
}
