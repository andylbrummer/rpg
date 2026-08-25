using RPC.Engine.Character;

namespace RPC.Engine.Save;

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
    public string? HiddenTag { get; set; }
}
