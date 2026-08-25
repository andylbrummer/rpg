using System.Text.Json;

namespace RPC.Engine.Content;

/// <summary>A single indexed content entry, located by id and queryable by tag.</summary>
public record ContentRef(string Id, string Category, IReadOnlyList<string> Tags);

/// <summary>
/// Searchable index of all content (enemies, items, NPCs, encounters, factions, …) keyed by id and
/// by tag. Built from an <see cref="IContentCatalog"/> so it works against the filesystem or an
/// in-memory catalog. Backs reference validation of generated campaigns
/// (see <see cref="ContentReferenceValidator"/>).
/// </summary>
public class ContentLibraryIndex
{
    /// <summary>Flat content categories indexed by default (nested dirs like segments are not recursed).</summary>
    public static readonly string[] DefaultCategories =
    {
        "enemies", "items", "npcs", "encounters", "factions",
        "schemes", "complications", "synergies", "rumors", "classes",
    };

    private readonly Dictionary<string, ContentRef> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _byTag = new(StringComparer.Ordinal);
    private readonly List<string> _duplicateIds = new();

    public int Count => _byId.Count;
    public IReadOnlyCollection<string> Ids => _byId.Keys;
    /// <summary>Ids that appeared in more than one file (first occurrence wins in the index).</summary>
    public IReadOnlyList<string> DuplicateIds => _duplicateIds;

    public bool Contains(string id) => _byId.ContainsKey(id);
    public ContentRef? Get(string id) => _byId.TryGetValue(id, out var r) ? r : null;

    public IReadOnlyList<string> ByTag(string tag)
        => _byTag.TryGetValue(tag, out var list) ? list : Array.Empty<string>();

    public static ContentLibraryIndex Build(IContentCatalog catalog, IEnumerable<string>? categories = null)
    {
        var index = new ContentLibraryIndex();
        foreach (var category in categories ?? DefaultCategories)
        {
            foreach (var file in catalog.EnumerateFiles(category, "*.json"))
            {
                var json = catalog.GetString(file);
                if (string.IsNullOrWhiteSpace(json)) continue;

                JsonDocument doc;
                try { doc = JsonDocument.Parse(json); }
                catch { continue; } // skip malformed files rather than fail the whole index

                using (doc)
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var elem in doc.RootElement.EnumerateArray())
                            index.AddEntry(elem, category);
                    }
                    else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        index.AddEntry(doc.RootElement, category);
                    }
                }
            }
        }
        return index;
    }

    private void AddEntry(JsonElement elem, string category)
    {
        if (elem.ValueKind != JsonValueKind.Object) return;
        if (!elem.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.String) return;
        var id = idProp.GetString();
        if (string.IsNullOrEmpty(id)) return;

        if (_byId.ContainsKey(id))
        {
            if (!_duplicateIds.Contains(id)) _duplicateIds.Add(id); // distinct duplicate ids
            return; // first occurrence wins
        }

        var tags = new List<string>();
        if (elem.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in tagsProp.EnumerateArray())
                if (t.ValueKind == JsonValueKind.String && t.GetString() is { } ts)
                    tags.Add(ts);
        }

        _byId[id] = new ContentRef(id, category, tags);
        foreach (var tag in tags)
        {
            if (!_byTag.TryGetValue(tag, out var list))
            {
                list = new List<string>();
                _byTag[tag] = list;
            }
            list.Add(id);
        }
    }
}

/// <summary>Validates that the ids a generated campaign references all resolve in the content index.</summary>
public static class ContentReferenceValidator
{
    /// <summary>Referenced ids absent from the index (order-preserving, de-duplicated).</summary>
    public static IReadOnlyList<string> FindMissing(ContentLibraryIndex index, IEnumerable<string> referencedIds)
    {
        var missing = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in referencedIds)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (index.Contains(id)) continue;
            if (seen.Add(id)) missing.Add(id);
        }
        return missing;
    }

    public static bool AllResolve(ContentLibraryIndex index, IEnumerable<string> referencedIds)
        => FindMissing(index, referencedIds).Count == 0;
}
