namespace RPC.Engine.Dungeons;

public record DungeonUnlockConditions(
    string FactionId,
    int MinReputation);

public record DungeonTemplate(
    string Id,
    string Name,
    string[] SegmentPool,
    string[] SegmentPriority,
    int TargetRooms,
    string BossEncounterId,
    string EncounterTableId,
    string? WanderingTableId = null,
    DungeonUnlockConditions[]? UnlockConditions = null);
