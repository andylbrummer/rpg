using RPC.Engine.Commands;
using RPC.Engine.Combat;
using RPC.Engine.Town;

namespace RPC.Tests;

public class CommandDispatcherTests
{
    [Theory]
    [InlineData("move_forward", typeof(MoveForwardCommand))]
    [InlineData("move_back", typeof(MoveBackCommand))]
    [InlineData("strafe_left", typeof(StrafeLeftCommand))]
    [InlineData("strafe_right", typeof(StrafeRightCommand))]
    [InlineData("turn_left", typeof(TurnLeftCommand))]
    [InlineData("turn_right", typeof(TurnRightCommand))]
    [InlineData("cancel", typeof(CancelCommand))]
    [InlineData("flee_combat", typeof(FleeCombatCommand))]
    [InlineData("enter_combat", typeof(TriggerEncounterCommand))]
    [InlineData("rest", typeof(RestAtInnCommand))]
    [InlineData("return_to_town", typeof(ReturnToTownCommand))]
    [InlineData("save_game", typeof(SaveGameCommand))]
    [InlineData("reset_game", typeof(ResetGameCommand))]
    public void Parse_SimpleActions_ReturnsCorrectType(string actionType, Type expectedType)
    {
        var action = new PlayerAction { Type = actionType };
        var cmd = CommandDispatcher.Parse(action);
        Assert.IsType(expectedType, cmd);
    }

    [Fact]
    public void Parse_CombatAction_ReturnsCombatActionCommand()
    {
        var combatAction = new CombatAction(Guid.NewGuid(), ActionType.UseAbility, Guid.NewGuid(), "test_ability", null);
        var action = new PlayerAction { Type = "combat_action", Action = combatAction };
        var cmd = Assert.IsType<CombatActionCommand>(CommandDispatcher.Parse(action));
        Assert.Equal(combatAction, cmd.Action);
    }

    [Fact]
    public void Parse_CombatAction_MissingAction_Throws()
    {
        var action = new PlayerAction { Type = "combat_action" };
        Assert.Throws<ArgumentException>(() => CommandDispatcher.Parse(action));
    }

    [Fact]
    public void Parse_EnterDungeon_ReturnsEnterDungeonCommand()
    {
        var action = new PlayerAction { Type = "enter_dungeon", DungeonType = "crypt" };
        var cmd = Assert.IsType<EnterDungeonCommand>(CommandDispatcher.Parse(action));
        Assert.Equal("crypt", cmd.DungeonType);
    }

    [Fact]
    public void Parse_EnterDungeon_MissingDungeonType_DefaultsToBrokenEngine()
    {
        var action = new PlayerAction { Type = "enter_dungeon" };
        var cmd = Assert.IsType<EnterDungeonCommand>(CommandDispatcher.Parse(action));
        Assert.Equal("broken_engine", cmd.DungeonType);
    }

    [Fact]
    public void Parse_SwapRow_ReturnsSwapRowCommand()
    {
        var action = new PlayerAction { Type = "swap_row", Slot = 2 };
        var cmd = Assert.IsType<SwapRowCommand>(CommandDispatcher.Parse(action));
        Assert.Equal(2, cmd.Slot);
    }

    [Fact]
    public void Parse_SwapRow_MissingSlot_Throws()
    {
        var action = new PlayerAction { Type = "swap_row" };
        Assert.Throws<ArgumentException>(() => CommandDispatcher.Parse(action));
    }

    [Fact]
    public void Parse_TavernRecruit_ReturnsRecruitFromTavernCommand()
    {
        var action = new PlayerAction { Type = "tavern_recruit", TargetId = "recruit_1" };
        var cmd = Assert.IsType<RecruitFromTavernCommand>(CommandDispatcher.Parse(action));
        Assert.Equal("recruit_1", cmd.RecruitId);
    }

    [Fact]
    public void Parse_VendorPurchase_ReturnsVendorPurchaseCommand()
    {
        var action = new PlayerAction { Type = "vendor_purchase", TargetId = "item_1" };
        var cmd = Assert.IsType<VendorPurchaseCommand>(CommandDispatcher.Parse(action));
        Assert.Equal("item_1", cmd.ItemId);
    }

    [Fact]
    public void Parse_MissionAccept_ReturnsAcceptMissionCommand()
    {
        var action = new PlayerAction { Type = "mission_accept", TargetId = "m1" };
        var cmd = Assert.IsType<AcceptMissionCommand>(CommandDispatcher.Parse(action));
        Assert.Equal("m1", cmd.MissionId);
    }

    [Fact]
    public void Parse_WildCardAlliance_ReturnsWildCardAllianceCommand()
    {
        var action = new PlayerAction { Type = "wildcard_alliance", TargetId = "accept" };
        var cmd = Assert.IsType<WildCardAllianceCommand>(CommandDispatcher.Parse(action));
        Assert.Equal("accept", cmd.Choice);
    }

    [Fact]
    public void Parse_Travel_ReturnsTravelCommand()
    {
        var action = new PlayerAction { Type = "travel", TargetId = "town_1" };
        var cmd = Assert.IsType<TravelCommand>(CommandDispatcher.Parse(action));
        Assert.Equal("town_1", cmd.TargetId);
    }

    [Fact]
    public void Parse_ResolveTravelEncounter_ReturnsResolveTravelEncounterCommand()
    {
        var action = new PlayerAction { Type = "resolve_travel_encounter", TargetId = "fight" };
        var cmd = Assert.IsType<ResolveTravelEncounterCommand>(CommandDispatcher.Parse(action));
        Assert.Equal("fight", cmd.Choice);
    }

    [Fact]
    public void Parse_ResolveTravelEncounter_MissingTargetId_DefaultsToDefault()
    {
        var action = new PlayerAction { Type = "resolve_travel_encounter" };
        var cmd = Assert.IsType<ResolveTravelEncounterCommand>(CommandDispatcher.Parse(action));
        Assert.Equal("default", cmd.Choice);
    }

    [Fact]
    public void Parse_SetReputation_ReturnsSetReputationCommand()
    {
        var action = new PlayerAction { Type = "set_reputation", TargetId = "bureau", Value = 25 };
        var cmd = Assert.IsType<SetReputationCommand>(CommandDispatcher.Parse(action));
        Assert.Equal("bureau", cmd.FactionId);
        Assert.Equal(25, cmd.Value);
    }

    [Fact]
    public void Parse_CompleteMission_ReturnsCompleteMissionCommand()
    {
        var action = new PlayerAction { Type = "complete_mission", TargetId = "m1" };
        var cmd = Assert.IsType<CompleteMissionCommand>(CommandDispatcher.Parse(action));
        Assert.Equal("m1", cmd.MissionId);
    }

    [Fact]
    public void Parse_FailMission_ReturnsFailMissionCommand()
    {
        var action = new PlayerAction { Type = "fail_mission", TargetId = "m1" };
        var cmd = Assert.IsType<FailMissionCommand>(CommandDispatcher.Parse(action));
        Assert.Equal("m1", cmd.MissionId);
    }

    [Fact]
    public void Parse_AbandonMission_ReturnsAbandonMissionCommand()
    {
        var action = new PlayerAction { Type = "abandon_mission", TargetId = "m1" };
        var cmd = Assert.IsType<AbandonMissionCommand>(CommandDispatcher.Parse(action));
        Assert.Equal("m1", cmd.MissionId);
    }

    [Fact]
    public void Parse_DialogueChoice_ReturnsApplyDialogueReputationCommand()
    {
        var action = new PlayerAction { Type = "dialogue_choice", TargetId = "bureau", Value = 5 };
        var cmd = Assert.IsType<ApplyDialogueReputationCommand>(CommandDispatcher.Parse(action));
        Assert.Equal("bureau", cmd.FactionId);
        Assert.Equal(5, cmd.Delta);
    }

    [Fact]
    public void Parse_EncounterChoice_ReturnsResolveParleyCommand()
    {
        var action = new PlayerAction { Type = "encounter_choice", TargetId = "parley" };
        var cmd = Assert.IsType<ResolveParleyCommand>(CommandDispatcher.Parse(action));
        Assert.Equal("parley", cmd.Choice);
    }

    [Fact]
    public void Parse_ChooseBranch_ReturnsChooseBranchCommand()
    {
        var guid = Guid.NewGuid();
        var action = new PlayerAction { Type = "branch_choose", TargetId = guid.ToString(), Branch = "branch_a" };
        var cmd = Assert.IsType<ChooseBranchCommand>(CommandDispatcher.Parse(action));
        Assert.Equal(guid, cmd.CharacterId);
        Assert.Equal("branch_a", cmd.Branch);
    }

    [Fact]
    public void Parse_AccuseFaction_ReturnsAccuseFactionCommand()
    {
        var action = new PlayerAction { Type = "accuse_faction", TargetId = "bureau" };
        var cmd = Assert.IsType<AccuseFactionCommand>(CommandDispatcher.Parse(action));
        Assert.Equal("bureau", cmd.FactionId);
    }

    [Fact]
    public void Parse_TransferToCache_ReturnsTransferToCacheCommand()
    {
        var action = new PlayerAction { Type = "transfer_to_cache", Slot = 1, TargetId = "item_1", Value = 5 };
        var cmd = Assert.IsType<TransferToCacheCommand>(CommandDispatcher.Parse(action));
        Assert.Equal(1, cmd.Slot);
        Assert.Equal("item_1", cmd.ItemId);
        Assert.Equal(5, cmd.Count);
    }

    [Fact]
    public void Parse_TransferFromCache_ReturnsTransferFromCacheCommand()
    {
        var action = new PlayerAction { Type = "transfer_from_cache", Slot = 1, TargetId = "item_1", Value = 5 };
        var cmd = Assert.IsType<TransferFromCacheCommand>(CommandDispatcher.Parse(action));
        Assert.Equal(1, cmd.Slot);
        Assert.Equal("item_1", cmd.ItemId);
        Assert.Equal(5, cmd.Count);
    }

    [Fact]
    public void Parse_DowntimeAction_ReturnsDowntimeActionCommand()
    {
        var guid = Guid.NewGuid();
        var action = new PlayerAction { Type = "downtime_action", TargetId = guid.ToString(), DowntimeAction = "Rest" };
        var cmd = Assert.IsType<DowntimeActionCommand>(CommandDispatcher.Parse(action));
        Assert.Equal(guid, cmd.CharacterId);
        Assert.Equal(DowntimeAction.Rest, cmd.Action);
    }

    [Fact]
    public void Parse_DowntimeAction_CaseInsensitive()
    {
        var guid = Guid.NewGuid();
        var action = new PlayerAction { Type = "downtime_action", TargetId = guid.ToString(), DowntimeAction = "rest" };
        var cmd = Assert.IsType<DowntimeActionCommand>(CommandDispatcher.Parse(action));
        Assert.Equal(DowntimeAction.Rest, cmd.Action);
    }

    [Fact]
    public void Parse_ResurrectCharacter_ReturnsResurrectCharacterCommand()
    {
        var guid = Guid.NewGuid();
        var action = new PlayerAction { Type = "resurrect_character", TargetId = guid.ToString() };
        var cmd = Assert.IsType<ResurrectCharacterCommand>(CommandDispatcher.Parse(action));
        Assert.Equal(guid, cmd.CharacterId);
    }

    [Fact]
    public void Parse_VerifyRumor_ReturnsVerifyRumorCommand()
    {
        var action = new PlayerAction { Type = "rumor_verify", TargetId = "rumor-1", Source = "InkbloodScribe" };
        var cmd = Assert.IsType<VerifyRumorCommand>(CommandDispatcher.Parse(action));
        Assert.Equal("rumor-1", cmd.RumorId);
        Assert.Equal("InkbloodScribe", cmd.Source);
    }

    [Fact]
    public void Parse_UnknownAction_ThrowsArgumentException()
    {
        var action = new PlayerAction { Type = "unknown_action" };
        var ex = Assert.Throws<ArgumentException>(() => CommandDispatcher.Parse(action));
        Assert.Contains("unknown_action", ex.Message);
    }

    [Fact]
    public void Parse_ActionType_IsCaseInsensitive()
    {
        var action = new PlayerAction { Type = "MOVE_FORWARD" };
        var cmd = CommandDispatcher.Parse(action);
        Assert.IsType<MoveForwardCommand>(cmd);
    }
}
