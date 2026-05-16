using RPC.Engine.Combat;
using RPC.Engine.Town;

namespace RPC.Engine.Commands;

public static class CommandDispatcher
{
    public static ICommand Parse(PlayerAction action)
    {
        return action.Type.ToLowerInvariant() switch
        {
            "move_forward" => new MoveForwardCommand(),
            "move_back" => new MoveBackCommand(),
            "strafe_left" => new StrafeLeftCommand(),
            "strafe_right" => new StrafeRightCommand(),
            "turn_left" => new TurnLeftCommand(),
            "turn_right" => new TurnRightCommand(),
            "cancel" => new CancelCommand(),
            "combat_action" => new CombatActionCommand(action.Action ?? throw new ArgumentException("CombatAction required")),
            "flee_combat" => new FleeCombatCommand(),
            "enter_combat" => new TriggerEncounterCommand(),
            "enter_dungeon" => new EnterDungeonCommand(action.DungeonType ?? "broken_engine"),
            "rest" => new RestAtInnCommand(),
            "return_to_town" => new ReturnToTownCommand(),
            "save_game" => new SaveGameCommand(),
            "reset_game" => new ResetGameCommand(),
            "swap_row" => new SwapRowCommand(action.Slot ?? throw new ArgumentException("Slot required")),
            "tavern_recruit" => new RecruitFromTavernCommand(action.TargetId ?? throw new ArgumentException("TargetId required")),
            "mission_accept" => new AcceptMissionCommand(action.TargetId ?? throw new ArgumentException("TargetId required")),
            "vendor_purchase" => new VendorPurchaseCommand(action.TargetId ?? throw new ArgumentException("TargetId required")),
            "wildcard_alliance" => new WildCardAllianceCommand(action.TargetId ?? throw new ArgumentException("TargetId required")),
            "travel" => new TravelCommand(action.TargetId ?? throw new ArgumentException("TargetId required")),
            "resolve_travel_encounter" => new ResolveTravelEncounterCommand(action.TargetId ?? "default"),
            "set_reputation" => new SetReputationCommand(
                action.TargetId ?? throw new ArgumentException("TargetId required"),
                action.Value ?? throw new ArgumentException("Value required")),
            "complete_mission" => new CompleteMissionCommand(action.TargetId ?? throw new ArgumentException("TargetId required")),
            "fail_mission" => new FailMissionCommand(action.TargetId ?? throw new ArgumentException("TargetId required")),
            "abandon_mission" => new AbandonMissionCommand(action.TargetId ?? throw new ArgumentException("TargetId required")),
            "dialogue_choice" => new ApplyDialogueReputationCommand(
                action.TargetId ?? throw new ArgumentException("TargetId required"),
                action.Value ?? throw new ArgumentException("Value required")),
            "encounter_choice" => new ResolveParleyCommand(action.TargetId ?? throw new ArgumentException("TargetId required")),
            "branch_choose" => new ChooseBranchCommand(
                Guid.Parse(action.TargetId ?? throw new ArgumentException("TargetId required")),
                action.Branch ?? throw new ArgumentException("Branch required")),
            "accuse_faction" => new AccuseFactionCommand(action.TargetId ?? throw new ArgumentException("TargetId required")),
            "transfer_to_cache" => new TransferToCacheCommand(
                action.Slot ?? throw new ArgumentException("Slot required"),
                action.TargetId ?? throw new ArgumentException("TargetId required"),
                action.Value ?? throw new ArgumentException("Value required")),
            "transfer_from_cache" => new TransferFromCacheCommand(
                action.Slot ?? throw new ArgumentException("Slot required"),
                action.TargetId ?? throw new ArgumentException("TargetId required"),
                action.Value ?? throw new ArgumentException("Value required")),
            "downtime_action" => new DowntimeActionCommand(
                Guid.Parse(action.TargetId ?? throw new ArgumentException("TargetId required")),
                Enum.Parse<DowntimeAction>(action.DowntimeAction ?? throw new ArgumentException("DowntimeAction required"), true)),
            "resurrect_character" => new ResurrectCharacterCommand(
                Guid.Parse(action.TargetId ?? throw new ArgumentException("TargetId required"))),
            "rumor_verify" => new VerifyRumorCommand(
                action.TargetId ?? throw new ArgumentException("TargetId required"),
                action.Source ?? throw new ArgumentException("Source required")),
            _ => throw new ArgumentException($"Unknown action type: {action.Type}")
        };
    }
}
