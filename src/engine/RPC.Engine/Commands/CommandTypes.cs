using RPC.Engine.Combat;
using RPC.Engine.Town;

namespace RPC.Engine.Commands;

// Movement
public record MoveForwardCommand : ICommand;
public record MoveBackCommand : ICommand;
public record StrafeLeftCommand : ICommand;
public record StrafeRightCommand : ICommand;
public record TurnLeftCommand : ICommand;
public record TurnRightCommand : ICommand;

// Combat
public record CombatActionCommand(CombatAction Action) : ICommand;
public record FleeCombatCommand : ICommand;
public record TriggerEncounterCommand : ICommand;
public record ResolveParleyCommand(string Choice) : ICommand;

// Dungeon & Exploration
public record EnterDungeonCommand(string DungeonType) : ICommand;

// Town
public record RestAtInnCommand : ICommand;
public record ReturnToTownCommand : ICommand;
public record SwapRowCommand(int Slot) : ICommand;
public record RecruitFromTavernCommand(string RecruitId) : ICommand;
public record VendorPurchaseCommand(string ItemId) : ICommand;
public record DowntimeActionCommand(Guid CharacterId, DowntimeAction Action) : ICommand;
public record ResurrectCharacterCommand(Guid CharacterId) : ICommand;
public record VerifyRumorCommand(string RumorId, string Source) : ICommand;

// Overworld & Travel
public record TravelCommand(string TargetId) : ICommand;
public record ResolveTravelEncounterCommand(string Choice) : ICommand;

// Campaign
public record SetReputationCommand(string FactionId, int Value) : ICommand;
public record ApplyDialogueReputationCommand(string FactionId, int Delta) : ICommand;
public record AccuseFactionCommand(string FactionId) : ICommand;
public record ChooseBranchCommand(Guid CharacterId, string Branch) : ICommand;
public record WildCardAllianceCommand(string Choice) : ICommand;

// Missions
public record AcceptMissionCommand(string MissionId) : ICommand;
public record CompleteMissionCommand(string MissionId) : ICommand;
public record FailMissionCommand(string MissionId) : ICommand;
public record AbandonMissionCommand(string MissionId) : ICommand;

// Inventory
public record TransferToCacheCommand(int Slot, string ItemId, int Count) : ICommand;
public record TransferFromCacheCommand(int Slot, string ItemId, int Count) : ICommand;

// Meta
public record SaveGameCommand : ICommand;
public record ResetGameCommand : ICommand;
public record CancelCommand : ICommand;
