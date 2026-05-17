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
        Reputation.Clear();
        Evidence.Clear();
    }
}
