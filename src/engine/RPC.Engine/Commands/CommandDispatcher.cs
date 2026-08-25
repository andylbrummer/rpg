using RPC.Engine.Combat;
using RPC.Engine.Town;

namespace RPC.Engine.Commands;

public static class CommandDispatcher
{
    // Single source of truth for the action type strings the server accepts.
    // Each entry maps an action type to the factory that builds its command.
    // KnownActions is derived from these keys so the accepted-action set and the
    // dispatch logic can never drift apart. Drift against the protocol schema
    // fixture (Fixtures/protocol-schema.json) is caught by ProtocolActionSyncTests.
    private static readonly IReadOnlyDictionary<string, Func<PlayerAction, ICommand>> Factories =
        new Dictionary<string, Func<PlayerAction, ICommand>>
        {
            ["move_forward"] = _ => new MoveForwardCommand(),
            ["move_back"] = _ => new MoveBackCommand(),
            ["strafe_left"] = _ => new StrafeLeftCommand(),
            ["strafe_right"] = _ => new StrafeRightCommand(),
            ["turn_left"] = _ => new TurnLeftCommand(),
            ["turn_right"] = _ => new TurnRightCommand(),
            ["pickup_loot"] = _ => new PickupLootCommand(),
            ["search_secrets"] = _ => new SearchSecretsCommand(),
            ["break_wall"] = a => new BreakWallCommand(a.TargetId ?? throw new ArgumentException("TargetId required")),
            ["cancel"] = _ => new CancelCommand(),
            ["combat_action"] = a => new CombatActionCommand(a.Action ?? throw new ArgumentException("CombatAction required")),
            ["use_consumable"] = a =>
            {
                var act = a.Action ?? throw new ArgumentException("CombatAction required");
                return new UseConsumableCommand(
                    act.ActorId,
                    act.ItemId ?? throw new ArgumentException("ItemId required"),
                    act.TargetId);
            },
            ["flee_combat"] = _ => new FleeCombatCommand(),
            ["enter_combat"] = _ => new TriggerEncounterCommand(),
            ["enter_dungeon"] = a => new EnterDungeonCommand(a.DungeonType ?? "broken_engine"),
            ["rest"] = _ => new RestAtInnCommand(),
            ["return_to_town"] = _ => new ReturnToTownCommand(),
            ["save_game"] = _ => new SaveGameCommand(),
            ["reset_game"] = _ => new ResetGameCommand(),
            ["set_ironman"] = a => new SetIronmanCommand(a.Enabled ?? throw new ArgumentException("Enabled required")),
            ["swap_row"] = a => new SwapRowCommand(a.Slot ?? throw new ArgumentException("Slot required")),
            ["tavern_recruit"] = a => new RecruitFromTavernCommand(a.TargetId ?? throw new ArgumentException("TargetId required")),
            ["swap_active_bench"] = a => new SwapActiveBenchCommand(
                a.Slot ?? throw new ArgumentException("Slot required"),
                string.IsNullOrEmpty(a.TargetId) ? null : Guid.Parse(a.TargetId)),
            ["dismiss_character"] = a => new DismissCharacterCommand(
                Guid.Parse(a.TargetId ?? throw new ArgumentException("TargetId required"))),
            ["mission_accept"] = a => new AcceptMissionCommand(a.TargetId ?? throw new ArgumentException("TargetId required")),
            ["vendor_purchase"] = a => new VendorPurchaseCommand(a.TargetId ?? throw new ArgumentException("TargetId required")),
            ["wildcard_alliance"] = a => new WildCardAllianceCommand(a.TargetId ?? throw new ArgumentException("TargetId required")),
            ["travel"] = a => new TravelCommand(a.TargetId ?? throw new ArgumentException("TargetId required")),
            ["resolve_travel_encounter"] = a => new ResolveTravelEncounterCommand(a.TargetId ?? "default"),
            ["set_reputation"] = a => new SetReputationCommand(
                a.TargetId ?? throw new ArgumentException("TargetId required"),
                a.Value ?? throw new ArgumentException("Value required")),
            ["complete_mission"] = a => new CompleteMissionCommand(a.TargetId ?? throw new ArgumentException("TargetId required")),
            ["fail_mission"] = a => new FailMissionCommand(a.TargetId ?? throw new ArgumentException("TargetId required")),
            ["abandon_mission"] = a => new AbandonMissionCommand(a.TargetId ?? throw new ArgumentException("TargetId required")),
            ["dialogue_choice"] = a => new ApplyDialogueReputationCommand(
                a.TargetId ?? throw new ArgumentException("TargetId required"),
                a.Value ?? throw new ArgumentException("Value required")),
            ["encounter_choice"] = a => new ResolveParleyCommand(a.TargetId ?? throw new ArgumentException("TargetId required")),
            ["branch_choose"] = a => new ChooseBranchCommand(
                Guid.Parse(a.TargetId ?? throw new ArgumentException("TargetId required")),
                a.Branch ?? throw new ArgumentException("Branch required")),
            ["accuse_faction"] = a => new AccuseFactionCommand(a.TargetId ?? throw new ArgumentException("TargetId required")),
            ["choose_betrayal"] = _ => new ChooseBetrayalCommand(),
            ["read_archive"] = a => new ReadArchiveCommand(a.TargetId ?? throw new ArgumentException("TargetId required")),
            ["transfer_to_cache"] = a => new TransferToCacheCommand(
                a.Slot ?? throw new ArgumentException("Slot required"),
                a.TargetId ?? throw new ArgumentException("TargetId required"),
                a.Value ?? throw new ArgumentException("Value required")),
            ["transfer_from_cache"] = a => new TransferFromCacheCommand(
                a.Slot ?? throw new ArgumentException("Slot required"),
                a.TargetId ?? throw new ArgumentException("TargetId required"),
                a.Value ?? throw new ArgumentException("Value required")),
            ["transfer_to_town_storage"] = a => new TransferToTownStorageCommand(
                a.Slot ?? throw new ArgumentException("Slot required"),
                a.TargetId ?? throw new ArgumentException("TargetId required"),
                a.Value ?? throw new ArgumentException("Value required")),
            ["transfer_from_town_storage"] = a => new TransferFromTownStorageCommand(
                a.Slot ?? throw new ArgumentException("Slot required"),
                a.TargetId ?? throw new ArgumentException("TargetId required"),
                a.Value ?? throw new ArgumentException("Value required")),
            ["downtime_action"] = a => new DowntimeActionCommand(
                Guid.Parse(a.TargetId ?? throw new ArgumentException("TargetId required")),
                Enum.Parse<DowntimeAction>(a.DowntimeAction ?? throw new ArgumentException("DowntimeAction required"), true)),
            ["resurrect_character"] = a => new ResurrectCharacterCommand(
                Guid.Parse(a.TargetId ?? throw new ArgumentException("TargetId required"))),
            ["pay_tithe"] = _ => new PayTitheCommand(),
            ["equip_item"] = a => new EquipItemCommand(
                Guid.Parse(a.TargetId ?? throw new ArgumentException("TargetId required")),
                a.ItemId ?? throw new ArgumentException("ItemId required"),
                a.EquipSlot ?? throw new ArgumentException("EquipSlot required")),
            ["unequip_item"] = a => new UnequipItemCommand(
                Guid.Parse(a.TargetId ?? throw new ArgumentException("TargetId required")),
                a.EquipSlot ?? throw new ArgumentException("EquipSlot required")),
            ["rumor_verify"] = a => new VerifyRumorCommand(
                a.TargetId ?? throw new ArgumentException("TargetId required"),
                a.Source ?? throw new ArgumentException("Source required")),
        };

    /// <summary>
    /// The set of action type strings the server accepts. Server-side source of
    /// truth for the protocol action contract, derived from the dispatch table.
    /// </summary>
    public static IReadOnlySet<string> KnownActions { get; } = Factories.Keys.ToHashSet();

    public static ICommand Parse(PlayerAction action)
    {
        var type = action.Type.ToLowerInvariant();
        return Factories.TryGetValue(type, out var factory)
            ? factory(action)
            : throw new ArgumentException($"Unknown action type: {action.Type}");
    }
}
