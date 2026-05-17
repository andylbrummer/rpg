namespace RPC.Engine.Campaign;

public class CampaignState
{
    public ReputationState Reputation { get; } = new();
    public EvidenceState Evidence { get; } = new();
    public JournalState Journal { get; } = new();
    public HeatState Heat { get; } = new();
    public WorldState WorldState { get; set; } = new();
    public CampaignConfig? CampaignConfig { get; set; }
    public SchemeDef? CurrentScheme { get; set; }
    public ComplicationDef? CurrentComplication { get; set; }
    public bool CampaignEnded { get; set; } = false;
    public string? AccusedFaction { get; set; }
    public bool MastermindAdvantage { get; set; }
    public bool FinalDungeonUnlocked { get; set; }
    public WildCardAllianceStatus WildCardAllianceStatus { get; set; } = WildCardAllianceStatus.None;
    public int WildCardAllianceTurn { get; set; } = 0;

    // Player-modified faction timeline modifiers: factionId -> delta turns
    public Dictionary<string, int> FactionTimelineModifiers { get; set; } = new();

    // Tracks which campaign events have already fired
    public HashSet<string> FiredEvents { get; set; } = new();

    // Secret content: unlocked optional dungeons and betrayal path
    public HashSet<string> UnlockedDungeons { get; set; } = new();
    public bool BetrayalPath { get; set; } = false;

    public void Reset()
    {
        WorldState.Reset();
        CampaignConfig = null;
        CurrentScheme = null;
        CurrentComplication = null;
        CampaignEnded = false;
        AccusedFaction = null;
        MastermindAdvantage = false;
        FinalDungeonUnlocked = false;
        WildCardAllianceStatus = WildCardAllianceStatus.None;
        WildCardAllianceTurn = 0;
        FactionTimelineModifiers.Clear();
        Reputation.Clear();
        Evidence.Clear();
        UnlockedDungeons.Clear();
        BetrayalPath = false;
    }
}
