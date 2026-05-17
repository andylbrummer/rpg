using RPC.Engine.Character;

namespace RPC.Engine.Save;

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
    public string[] PartyInventory { get; set; } = Array.Empty<string>();
    public SaveComponentStack[] ExpeditionCache { get; set; } = Array.Empty<SaveComponentStack>();
    public SavePartyMember[] DeadCharacters { get; set; } = Array.Empty<SavePartyMember>();
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

public class SaveJournalState
{
    public string[] DiscoveredSynergies { get; set; } = Array.Empty<string>();
}

public class SaveHeatState
{
    public int Value { get; set; }
}

public class SaveWorldState
{
    public Dictionary<string, string> Settlements { get; set; } = new();
    public string[] AccessibleDungeons { get; set; } = Array.Empty<string>();
    public Dictionary<string, string[]> FactionTerritory { get; set; } = new();
}

public class SaveTownState
{
    public string CurrentTownId { get; set; } = "the_reach";
    public SaveMissionOffer[] AvailableMissions { get; set; } = Array.Empty<SaveMissionOffer>();
    public SaveVendorItem[] VendorStock { get; set; } = Array.Empty<SaveVendorItem>();
    public SaveFactionVendor[] FactionVendors { get; set; } = Array.Empty<SaveFactionVendor>();
    public SaveFactionContact[] FactionContacts { get; set; } = Array.Empty<SaveFactionContact>();
    public SaveTavernRecruit[] TavernRoster { get; set; } = Array.Empty<SaveTavernRecruit>();
    public string[] ViewedMissions { get; set; } = Array.Empty<string>();
    public SaveActiveMission[] QuestLog { get; set; } = Array.Empty<SaveActiveMission>();
    public SaveTownRumor[] Rumors { get; set; } = Array.Empty<SaveTownRumor>();
}

public class SaveMissionOffer
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int MinLevel { get; set; }
    public string[] Rewards { get; set; } = Array.Empty<string>();
    public int RepReward { get; set; }
    public string FactionId { get; set; } = "";
}

public class SaveVendorItem
{
    public string ItemId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Price { get; set; }
    public int Quantity { get; set; }
}

public class SaveFactionVendor
{
    public string FactionId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Threshold { get; set; }
    public SaveVendorItem[] Stock { get; set; } = Array.Empty<SaveVendorItem>();
}

public class SaveActionLogEntry
{
    public int Turn { get; set; }
    public string Category { get; set; } = "";
    public string Type { get; set; } = "";
    public Dictionary<string, string> Payload { get; set; } = new();
}

public class SaveTavernRecruit
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ClassId { get; set; } = "";
    public int Level { get; set; }
    public BaseStats BaseStats { get; set; }
    public int Cost { get; set; }
}

public class SaveFactionContact
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FactionId { get; set; } = "";
    public string Portrait { get; set; } = "";
}

public class SaveActiveMission
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int RepReward { get; set; }
    public string FactionId { get; set; } = "";
    public string Status { get; set; } = "";
}

public class SaveTownRumor
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public string TruthStatus { get; set; } = "";
    public bool Verified { get; set; }
    public bool? VerificationResult { get; set; }
    public string? RelatedContentId { get; set; }
    public string? RelatedFactionId { get; set; }
}

public class SaveOverworldNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string[] FactionPresence { get; set; } = Array.Empty<string>();
    public string? DungeonTemplateId { get; set; }
}

public class SaveOverworldRoute
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public int Distance { get; set; }
    public int DangerRating { get; set; }
    public string Terrain { get; set; } = "";
    public string Status { get; set; } = "";
}

public class SaveComponentStack
{
    public string ItemId { get; set; } = "";
    public int Count { get; set; }
    public int MaxStack { get; set; } = 99;
}

public class SavePartyMember
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string ClassId { get; set; } = "";
    public int Level { get; set; }
    public int Xp { get; set; }
    public BaseStats BaseStats { get; set; }
    public int CurrentHp { get; set; }
    public Equipment Equipment { get; set; }
    public string[] KnownAbilities { get; set; } = Array.Empty<string>();
    public int Row { get; set; }
    public string? BranchChoice { get; set; }
    public string? BranchLevel6 { get; set; }
    public TempStatModifier[] TempModifiers { get; set; } = Array.Empty<TempStatModifier>();
    public int ResurrectionAttempts { get; set; } = 0;
    public bool BranchAdvancementLocked { get; set; } = false;
    public SaveComponentStack[] ComponentInventory { get; set; } = Array.Empty<SaveComponentStack>();
}

public class SavePlayer
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Facing { get; set; } = "North";
}
