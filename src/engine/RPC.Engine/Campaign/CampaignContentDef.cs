namespace RPC.Engine.Campaign;

public record CampaignEventDef(
    string Id,
    string Name,
    string Description,
    int TurnThreshold,
    string FactionInvolved,
    string Effect,
    string? TargetNodeType = null,
    string? EncounterType = null,
    string? DungeonType = null,
    string? TargetFaction = null,
    int ReputationDelta = 0,
    string? WorldStateKey = null,
    string? WorldStateValue = null,
    int RouteCount = 0,
    double Multiplier = 1.0);

public record SchemeDef(
    string Id,
    string Name,
    string Description,
    string FinaleDungeonFeel,
    string[] EvidenceChain,
    CampaignEventDef[] Events);

public record WorldStateModifiers(
    Dictionary<string, double>? RouteStatusChance = null,
    TownEffects? TownEffects = null,
    TravelEffects? TravelEffects = null,
    Dictionary<string, int>? ReputationEffects = null,
    double? ResurrectionCostMultiplier = null,
    double? TitheTokenValue = null);

public record TownEffects(
    bool? SupplyShortage = null,
    double? VendorPriceMultiplier = null,
    double? RecruitCostMultiplier = null,
    bool? FactionAlignmentRequired = null,
    bool? NeutralVendorPenalty = null,
    bool? BoneClerkClosed = null,
    bool? PanicBuying = null,
    double? VendorComponentPriceMultiplier = null);

public record TravelEffects(
    double? EncounterChanceBonus = null,
    int? BloomEnemyWeight = null,
    int? FactionPatrolWeight = null);

public record ComplicationDef(
    string Id,
    string Name,
    string Description,
    WorldStateModifiers WorldStateModifiers,
    CampaignEventDef[] Events);
