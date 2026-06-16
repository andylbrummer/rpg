namespace RPC.Engine.Save;

// Root save aggregate. Feature-specific DTO definitions live in sibling
// SaveData.<Feature>.cs files (Party, Town, Overworld, Campaign, Journal, Heat,
// ActionLog, World, Dungeon). Restore logic lives in SaveRestorer; build/serialize
// logic lives in SaveBuilder. Keep this file limited to the top-level shape.
public class SaveData
{
    public int SchemaVersion { get; set; }
    public string? ContentHash { get; set; }
    public SavePartyMember?[] Party { get; set; } = new SavePartyMember?[6];
    public SavePlayer Player { get; set; } = new();
    public string? DungeonType { get; set; }
    public string[] ExploredTiles { get; set; } = Array.Empty<string>();
    public string Mode { get; set; } = "Menu";
    public SaveTownState? Town { get; set; }
    public SaveActionLogEntry[] ActionLog { get; set; } = Array.Empty<SaveActionLogEntry>();
    public Dictionary<string, int> Reputation { get; set; } = new();
    public Dictionary<string, int> Evidence { get; set; } = new();
    public string? SuspectedFaction { get; set; }
    public string? Settings { get; set; }
    public int OverworldTurns { get; set; } = 0;
    public string OverworldCurrentNodeId { get; set; } = "the_reach";
    public bool CampaignEnded { get; set; } = false;
    public string? AccusedFaction { get; set; }
    public bool MastermindAdvantage { get; set; } = false;
    public bool FinalDungeonUnlocked { get; set; } = false;
    public int PartyGold { get; set; } = 500;
    public int TitheTokens { get; set; } = 0;
    public int TitheDebt { get; set; } = 0;
    public int[] TitheBilledMilestones { get; set; } = Array.Empty<int>();
    public int? TitheOutstandingSinceTurn { get; set; }
    public string[] PartyInventory { get; set; } = Array.Empty<string>();
    public SaveComponentStack[] ExpeditionCache { get; set; } = Array.Empty<SaveComponentStack>();
    public string[] CollectedLoot { get; set; } = Array.Empty<string>();
    public SavePartyMember[] DeadCharacters { get; set; } = Array.Empty<SavePartyMember>();
    public SavePartyMember[] Bench { get; set; } = Array.Empty<SavePartyMember>();
    public SaveJournalState? Journal { get; set; }
    public SaveHeatState? Heat { get; set; }
    public SaveCampaignConfig? CampaignConfig { get; set; }
    public SaveOverworldNode[] OverworldNodes { get; set; } = Array.Empty<SaveOverworldNode>();
    public SaveOverworldRoute[] OverworldRoutes { get; set; } = Array.Empty<SaveOverworldRoute>();
    public int CurrentAct { get; set; } = 1;
    public SaveWorldState? WorldState { get; set; }
    public string[] DowntimeCompleted { get; set; } = Array.Empty<string>();
    public string? WildCardAllianceStatus { get; set; }
    public int WildCardAllianceTurn { get; set; }
    public int StepsSinceEncounter { get; set; } = 0;
    public int DungeonSeed { get; set; } = 0;
    public bool IsIronman { get; set; } = false;
    public Dictionary<string, int> FactionTimelineModifiers { get; set; } = new();
    public string[] FiredEvents { get; set; } = Array.Empty<string>();
    public string[] UnlockedDungeons { get; set; } = Array.Empty<string>();
    public bool BetrayalPath { get; set; } = false;
    public string FamilyName { get; set; } = "";
}

public class SavePlayer
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Facing { get; set; } = "North";
}
