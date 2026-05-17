using System.Text;
using RPC.Engine.Campaign;
using RPC.Engine.Dungeons;

namespace RPC.Engine.LLM;

/// <summary>
/// Builds LLM prompts for campaign generation from the content index.
/// Manages token budget by summarizing index data.
/// </summary>
public class PromptBuilder
{
    private readonly ContentIndex _index;
    private readonly int _maxTokens;

    public PromptBuilder(ContentIndex index, int maxTokens = 8000)
    {
        _index = index;
        _maxTokens = maxTokens;
    }

    public LLMPrompt BuildCampaignPrompt(
        int[] sixRolls,
        CampaignConfig? baseConfig = null)
    {
        var system = @"You are a campaign arranger for a dungeon crawler. You select and connect pre-authored content. You do not write original prose.

Rules:
- Select schemes, complications, factions, and NPCs from the provided content index.
- Ensure narrative coherence: the mastermind's scheme must logically involve the selected factions.
- Respect the six random rolls provided by the user.
- Output valid JSON matching the CampaignConfig schema.
- Do not invent content IDs that are not in the index.";

        var indexSummary = SummarizeIndex(_maxTokens - 2000); // Reserve tokens for response

        var user = new StringBuilder();
        user.AppendLine($"Six rolls: {string.Join(", ", sixRolls)}");
        user.AppendLine();
        user.AppendLine("Content index summary:");
        user.AppendLine(indexSummary);
        user.AppendLine();

        if (baseConfig != null)
        {
            user.AppendLine("Base config (override as needed):");
            user.AppendLine($"  Patron: {baseConfig.Patron}");
            user.AppendLine($"  Threat: {baseConfig.Threat}");
            user.AppendLine($"  Mastermind: {baseConfig.Mastermind}");
            user.AppendLine($"  Scheme: {baseConfig.Scheme}");
            user.AppendLine($"  WildCard: {baseConfig.WildCard}");
            user.AppendLine($"  Complication: {baseConfig.Complication}");
        }

        user.AppendLine();
        user.AppendLine("Generate a CampaignConfig JSON with: patron, threat, mastermind, wildcard, scheme, complication, evidenceChain (3-5 strings), factionTimelines (preparing/executing per faction), npcCasting (npcId -> role), and wildcardTrigger.");

        return new LLMPrompt(system, user.ToString(), Temperature: 0.7f, MaxTokens: 2000);
    }

    public LLMPrompt BuildEpiloguePrompt(GameState state, int maxEvents = 20)
    {
        var system = @"You are a campaign epilogue writer for a dungeon crawler.
Write 2-3 paragraphs summarizing the campaign's conclusion.
Rules:
- Do not invent events not in the log.
- Do not contradict template facts.
- Tone: grim, reflective, grounded.";

        var events = state.ActionLog
            .TakeLast(maxEvents)
            .Select(e => $"- Turn {e.Turn}: [{e.Category}] {e.Type}")
            .ToList();

        var user = new StringBuilder();
        user.AppendLine("Campaign facts:");
        user.AppendLine($"- Mastermind: {state.CampaignConfig?.Mastermind ?? "unknown"}");
        user.AppendLine($"- Scheme: {state.CampaignConfig?.Scheme.ToString() ?? "unknown"}");
        user.AppendLine($"- Betrayal: {state.Campaign.BetrayalPath}");
        user.AppendLine($"- Turns: {state.Overworld.Turns}");
        user.AppendLine($"- Deaths: {state.ActionLog.Count(e => e.Type == "character_died")}");
        user.AppendLine();
        user.AppendLine("Key events:");
        foreach (var evt in events)
            user.AppendLine(evt);
        user.AppendLine();
        user.AppendLine("Write the epilogue.");

        return new LLMPrompt(system, user.ToString(), Temperature: 0.8f, MaxTokens: 800);
    }

    private string SummarizeIndex(int tokenBudget)
    {
        // Rough heuristic: ~4 chars per token
        int charBudget = tokenBudget * 4;
        var sb = new StringBuilder();

        sb.AppendLine($"Dungeons ({_index.Dungeons.Length}): " +
            string.Join(", ", _index.Dungeons.Take(20).Select(d => d.Id)));

        sb.AppendLine($"Schemes ({_index.SchemesEncountered.Count}): " +
            string.Join(", ", _index.SchemesEncountered.Take(10)));

        sb.AppendLine($"Factions: bureau, convocation, cartography, stillness, inkblood");

        sb.AppendLine($"NPCs ({_index.Npcs.Length}): " +
            string.Join(", ", _index.Npcs.Take(10).Select(n => n.Id)));

        if (sb.Length > charBudget)
        {
            return sb.ToString()[..charBudget] + "...";
        }
        return sb.ToString();
    }
}

public record ContentIndex
{
    public string ContentHash { get; set; } = "";
    public IndexEntry[] Segments { get; set; } = Array.Empty<IndexEntry>();
    public IndexEntry[] Dungeons { get; set; } = Array.Empty<IndexEntry>();
    public IndexEntry[] Enemies { get; set; } = Array.Empty<IndexEntry>();
    public IndexEntry[] Npcs { get; set; } = Array.Empty<IndexEntry>();
    public IndexEntry[] Items { get; set; } = Array.Empty<IndexEntry>();
    public IndexEntry[] Encounters { get; set; } = Array.Empty<IndexEntry>();
    public HashSet<string> SchemesEncountered { get; set; } = new();
    public HashSet<string> ClassesPlayed { get; set; } = new();
    public HashSet<string> BranchesChosen { get; set; } = new();
    public HashSet<string> OptionalDungeonsUnlocked { get; set; } = new();
}

public record IndexEntry
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string? Template { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public int? Danger { get; set; }
    public IndexSize? Size { get; set; }
    public string[] Connections { get; set; } = Array.Empty<string>();
}

public record IndexSize
{
    public int Width { get; set; }
    public int Height { get; set; }
}
