namespace RPC.Engine.Campaign;

/// <summary>
/// Template-driven campaign epilogue generator.
/// Populates pre-authored templates from the action log and campaign state.
/// </summary>
public class EpilogueTemplate
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string[] Paragraphs { get; set; } = Array.Empty<string>();
}

public class EpilogueGenerator
{
    public static string Generate(GameState state)
    {
        var log = state.ActionLog;
        var campaign = state.Campaign;

        // Extract key facts from action log
        var mastermind = campaign.CampaignConfig?.Mastermind ?? "unknown";
        var scheme = campaign.CampaignConfig?.Scheme.ToString().ToLowerInvariant() ?? "unknown scheme";
        var patron = campaign.CampaignConfig?.Patron ?? "unknown";

        // Count character deaths
        var deaths = log.Where(e => e.Type == "character_died").ToList();
        var deathCount = deaths.Count;

        // Settlement fates chosen
        var settlementFates = log
            .Where(e => e.Type == "settlement_fate_chosen")
            .Select(e => e.Payload.GetValueOrDefault("fate", "unknown"))
            .ToList();

        // Check wild card alliance
        var wildCardStatus = campaign.WildCardAllianceStatus.ToString().ToLowerInvariant();
        var wildCardTurn = campaign.WildCardAllianceTurn;

        // Betrayal path
        var betrayal = campaign.BetrayalPath;

        // Build paragraphs
        var paragraphs = new List<string>();

        // Opening
        paragraphs.Add($"The campaign against the {mastermind} reached its conclusion. " +
            $"What began as a simple contract from the {patron} spiraled into something far darker.");

        // Scheme outcome
        if (campaign.CampaignEnded)
        {
            paragraphs.Add($"The {scheme} was ultimately foiled. " +
                $"The mastermind's plans came to nothing.");
        }
        else
        {
            paragraphs.Add($"The {scheme} remains unresolved. The Reach continues to suffer.");
        }

        // Settlement fates
        if (settlementFates.Count > 0)
        {
            var verb = settlementFates.Count == 1 ? "was" : "were";
            paragraphs.Add($"Across the Reach, {settlementFates.Count} settlement{(settlementFates.Count > 1 ? "s" : "")} {verb} shaped by your choices.");
        }

        // Party losses
        if (deathCount > 0)
        {
            var companionWord = deathCount == 1 ? "companion fell" : "companions fell";
            paragraphs.Add($"Your party paid a heavy price. {deathCount} {companionWord} in the darkness.");
        }
        else
        {
            paragraphs.Add("Remarkably, every member of your party survived the ordeal.");
        }

        // Wild card
        if (wildCardTurn > 0)
        {
            paragraphs.Add($"The wild card faction revealed their true nature — alliance status: {wildCardStatus}.");
        }

        // Betrayal
        if (betrayal)
        {
            paragraphs.Add("In the end, you chose to stand with the mastermind. The Reach will remember your betrayal.");
        }

        return string.Join("\n\n", paragraphs);
    }
}
