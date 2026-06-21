using System.Text.Json;
using RPC.Engine.Content;

namespace RPC.Engine.Town;

public record DialogueDef(string Speaker, string Kind, Dictionary<string, string> Tiers);

public class DialogueRepository
{
    private readonly Dictionary<(string kind, string speaker), DialogueDef> _byKey;
    private readonly List<DialogueDef> _defs;

    public DialogueRepository(List<DialogueDef> defs)
    {
        _defs = defs ?? new List<DialogueDef>();
        _byKey = _defs.ToDictionary(d => (d.Kind, d.Speaker));
    }

    public DialogueRepository(IContentCatalog catalog)
        : this(LoadDefs(catalog)) { }

    private static List<DialogueDef> LoadDefs(IContentCatalog catalog)
    {
        var defs = new List<DialogueDef>();
        foreach (var file in catalog.EnumerateFiles("dialogue", "*.json"))
        {
            var json = catalog.GetString(file) ?? catalog.GetString($"dialogue/{Path.GetFileName(file)}");
            if (json == null) continue;
            var parsed = JsonSerializer.Deserialize<List<DialogueDef>>(json, ContentJsonOptions.Standard);
            if (parsed != null) defs.AddRange(parsed);
        }
        return defs;
    }

    public IReadOnlyList<DialogueDef> Defs => _defs;

    /// <summary>Tier by reputation: rep &lt; 0 → low, rep &gt;= 30 → high, otherwise neutral.</summary>
    private static string TierFor(int rep) => rep < 0 ? "low" : rep >= 30 ? "high" : "neutral";

    public string GetLine(string kind, string speaker, int rep)
    {
        var tier = TierFor(rep);
        if (TryLine(kind, speaker, tier, out var line)) return line;
        if (TryLine(kind, speaker, "neutral", out line)) return line;
        if (TryLine(kind, "generic", tier, out line)) return line;
        if (TryLine(kind, "generic", "neutral", out line)) return line;
        return "...";
    }

    private bool TryLine(string kind, string speaker, string tier, out string line)
    {
        line = "...";
        if (_byKey.TryGetValue((kind, speaker), out var def) && def.Tiers.TryGetValue(tier, out var l))
        {
            line = l;
            return true;
        }
        return false;
    }
}
