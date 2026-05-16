using System.Text.Json.Serialization;
using RPC.Engine.Combat;

namespace RPC.Engine.Campaign;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SchemeType
{
    BloomHarvest,
    EngineSeizure,
    CascadeFailure,
    TheResurrection,
    ManufacturedCrisis,
    TheVault
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ComplicationType
{
    BloomSiege,
    TitheCollapse,
    OpenWar,
    ErraticEngine,
    MissingTeam,
    ClosingPasses
}

public record FactionTimeline(int Preparing, int Executing);

public record WildcardTrigger(string FactionId, int TurnThreshold);

public class CampaignConfig
{
    public string Patron { get; set; } = "";
    public string Threat { get; set; } = "";
    public string Mastermind { get; set; } = "";
    public SchemeType Scheme { get; set; }
    public string WildCard { get; set; } = "";
    public ComplicationType Complication { get; set; }
    public List<string> EvidenceChain { get; set; } = new();
    public Dictionary<string, FactionTimeline> FactionTimelines { get; set; } = new();
    public Dictionary<string, string> NpcCasting { get; set; } = new();
    public WildcardTrigger? WildcardTrigger { get; set; }

    public static readonly string[] FactionPool =
    [
        "bureau", "convocation", "cartography", "stillness", "inkblood"
    ];

    public static readonly SchemeType[] SchemePool =
    [
        SchemeType.BloomHarvest,
        SchemeType.EngineSeizure,
        SchemeType.CascadeFailure,
        SchemeType.TheResurrection,
        SchemeType.ManufacturedCrisis,
        SchemeType.TheVault
    ];

    public static readonly ComplicationType[] ComplicationPool =
    [
        ComplicationType.BloomSiege,
        ComplicationType.TitheCollapse,
        ComplicationType.OpenWar,
        ComplicationType.ErraticEngine,
        ComplicationType.MissingTeam,
        ComplicationType.ClosingPasses
    ];

    public static CampaignConfig Roll(GameRandom rng)
    {
        var shuffled = Shuffle(FactionPool, rng);
        var patron = shuffled[0];
        var threat = shuffled[1];

        var mastermindPool = FactionPool.Where(f => f != threat).ToArray();
        var mastermind = mastermindPool[rng.Next(mastermindPool.Length)];

        var scheme = SchemePool[rng.Next(SchemePool.Length)];

        var involved = new HashSet<string> { patron, threat, mastermind };
        var wildCardPool = FactionPool.Where(f => !involved.Contains(f)).ToArray();
        var wildCard = wildCardPool[rng.Next(wildCardPool.Length)];

        var complication = ComplicationPool[rng.Next(ComplicationPool.Length)];

        var wildcardTurn = rng.Roll(18, 24); // 18-24 inclusive

        return new CampaignConfig
        {
            Patron = patron,
            Threat = threat,
            Mastermind = mastermind,
            Scheme = scheme,
            WildCard = wildCard,
            Complication = complication,
            WildcardTrigger = new WildcardTrigger(wildCard, wildcardTurn)
        };
    }

    public bool Validate(out string error)
    {
        error = "";

        if (string.IsNullOrEmpty(Patron) || !FactionPool.Contains(Patron))
            error = "Invalid patron.";
        else if (Patron == Threat)
            error = "Patron cannot be the threat.";
        else if (Threat == Mastermind)
            error = "Threat cannot be the mastermind.";
        else if (string.IsNullOrEmpty(WildCard) || new[] { Patron, Threat, Mastermind }.Contains(WildCard))
            error = "Wild card must be uninvolved.";
        else if (EvidenceChain.Count < 10)
            error = "Evidence chain must have at least 10 entries.";
        else if (NpcCasting.Values.Distinct().Count() != NpcCasting.Count)
            error = "NPC cannot fill two roles.";
        else if (FactionTimelines.Values.Any(t => t.Preparing >= t.Executing))
            error = "Faction timeline preparing must be less than executing.";
        else if (WildcardTrigger != null && (WildcardTrigger.FactionId == Threat || WildcardTrigger.FactionId == Mastermind))
            error = "Wildcard trigger faction cannot be threat or mastermind.";

        return error == "";
    }

    private static string[] Shuffle(string[] source, GameRandom rng)
    {
        var result = source.ToArray();
        for (int i = result.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }
        return result;
    }
}
