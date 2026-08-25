namespace RPC.Engine.Save;

public class SaveCampaignConfig
{
    public string Patron { get; set; } = "";
    public string Threat { get; set; } = "";
    public string Mastermind { get; set; } = "";
    public string Scheme { get; set; } = "";
    public string WildCard { get; set; } = "";
    public string Complication { get; set; } = "";
    public string[] EvidenceChain { get; set; } = Array.Empty<string>();
    public Dictionary<string, SaveFactionTimeline> FactionTimelines { get; set; } = new();
    public Dictionary<string, string> NpcCasting { get; set; } = new();
    public SaveWildcardTrigger? WildcardTrigger { get; set; }
}

public class SaveFactionTimeline
{
    public int Preparing { get; set; }
    public int Executing { get; set; }
}

public class SaveWildcardTrigger
{
    public string FactionId { get; set; } = "";
    public int TurnThreshold { get; set; }
}
