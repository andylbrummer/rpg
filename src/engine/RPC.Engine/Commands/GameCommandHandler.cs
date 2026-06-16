using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Commands;
using RPC.Engine.Content;
using RPC.Engine.Dungeons;
using RPC.Engine.Inventory;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Town;

namespace RPC.Engine.Commands;

public record CommandResult(bool StateChanged, bool ClearCombatResult = false);

public class GameCommandHandler
{
    private readonly GameState _gameState;
    private readonly IDungeonGenerator _dungeonGenerator;
    private readonly ItemRegistry? _itemRegistry;

    public GameCommandHandler(GameState gameState, IDungeonGenerator dungeonGenerator, ItemRegistry? itemRegistry = null)
    {
        _gameState = gameState;
        _dungeonGenerator = dungeonGenerator;
        _itemRegistry = itemRegistry;
    }

    public CommandResult Execute(ICommand cmd)
    {
        bool stateChanged = false;
        bool clearCombatResult = false;

        switch (cmd)
        {
            case MoveForwardCommand:
                stateChanged = _gameState.TryMoveForward();
                break;
            case MoveBackCommand:
                stateChanged = _gameState.TryMoveBack();
                break;
            case StrafeLeftCommand:
                stateChanged = _gameState.TryStrafeLeft();
                break;
            case StrafeRightCommand:
                stateChanged = _gameState.TryStrafeRight();
                break;
            case TurnLeftCommand:
                _gameState.TurnLeft();
                stateChanged = true;
                break;
            case TurnRightCommand:
                _gameState.TurnRight();
                stateChanged = true;
                break;
            case PickupLootCommand:
                stateChanged = _gameState.TryPickupLoot();
                break;
            case SearchSecretsCommand:
                stateChanged = _gameState.SearchForSecrets().Count > 0;
                break;
            case BreakWallCommand breakCmd:
                stateChanged = _gameState.BreakWall(breakCmd.SecretId);
                break;
            case CancelCommand:
                stateChanged = true;
                break;
            case CombatActionCommand combatCmd:
                stateChanged = _gameState.SubmitCombatAction(combatCmd.Action);
                if (_gameState.LastCombatResult != null)
                    clearCombatResult = true;
                break;
            case UseConsumableCommand useCmd:
                stateChanged = UseConsumable(useCmd);
                break;
            case FleeCombatCommand:
                _gameState.FleeCombat();
                stateChanged = true;
                clearCombatResult = true;
                break;
            case TriggerEncounterCommand:
                _gameState.TriggerEncounter();
                stateChanged = true;
                break;
            case EnterDungeonCommand enterDungeonCmd:
                {
                    var request = new DungeonGenerationRequest(enterDungeonCmd.DungeonType, ContentHash: _gameState.ContentHash);
                    var result = _dungeonGenerator.Generate(request);
                    _gameState.EnterDungeon(result.Dungeon, enterDungeonCmd.DungeonType);
                    stateChanged = true;
                }
                break;
            case RestAtInnCommand:
                _gameState.RestAtInn();
                stateChanged = true;
                break;
            case ReturnToTownCommand:
                _gameState.ReturnToTown();
                stateChanged = true;
                break;
            case SaveGameCommand:
                _gameState.SaveGame();
                stateChanged = true;
                break;

            case SetIronmanCommand ironmanCmd:
                _gameState.IsIronman = ironmanCmd.Enabled;
                stateChanged = true;
                break;
            case ResetGameCommand:
                _gameState.Reset();
                stateChanged = true;
                break;
            case SwapRowCommand swapCmd:
                {
                    var member = _gameState.Party.Members[swapCmd.Slot];
                    _gameState.Party.SwapRows(swapCmd.Slot);
                    stateChanged = true;
                    if (member.Id != Guid.Empty)
                    {
                        _gameState.EmitActionLog("roster", "row_changed", new Dictionary<string, string>
                        {
                            { "characterId", member.Id.ToString() },
                            { "characterName", member.Name },
                            { "newRow", member.Row == 0 ? "1" : "0" }
                        });
                    }
                }
                break;
            case RecruitFromTavernCommand recruitCmd:
                stateChanged = _gameState.RecruitFromTavern(recruitCmd.RecruitId);
                break;
            case SwapActiveBenchCommand swapBenchCmd:
                stateChanged = _gameState.SwapActiveBench(swapBenchCmd.ActiveSlot, swapBenchCmd.BenchCharacterId);
                break;
            case DismissCharacterCommand dismissCmd:
                stateChanged = _gameState.DismissCharacter(dismissCmd.CharacterId);
                break;
            case AcceptMissionCommand acceptCmd:
                stateChanged = _gameState.AcceptMission(acceptCmd.MissionId);
                break;
            case VendorPurchaseCommand purchaseCmd:
                stateChanged = _gameState.PurchaseVendorItem(purchaseCmd.ItemId);
                break;
            case WildCardAllianceCommand allianceCmd:
                stateChanged = allianceCmd.Choice.ToLowerInvariant() switch
                {
                    "accept" => _gameState.AcceptWildCardAlliance(),
                    "refuse" => _gameState.RefuseWildCardAlliance(),
                    "ignore" => _gameState.IgnoreWildCardAlliance(),
                    _ => false
                };
                break;
            case ChooseBetrayalCommand:
                stateChanged = _gameState.ChooseBetrayal();
                break;
            case TravelCommand travelCmd:
                stateChanged = _gameState.Travel(travelCmd.TargetId);
                break;
            case ResolveTravelEncounterCommand resolveCmd:
                stateChanged = _gameState.ResolveTravelEncounter(resolveCmd.Choice);
                break;
            case SetReputationCommand repCmd:
                _gameState.SetReputation(repCmd.FactionId, repCmd.Value);
                stateChanged = true;
                break;
            case CompleteMissionCommand completeCmd:
                stateChanged = _gameState.CompleteMission(completeCmd.MissionId);
                break;
            case FailMissionCommand failCmd:
                stateChanged = _gameState.FailMission(failCmd.MissionId);
                break;
            case AbandonMissionCommand abandonCmd:
                stateChanged = _gameState.AbandonMission(abandonCmd.MissionId);
                break;
            case ApplyDialogueReputationCommand dialogueCmd:
                stateChanged = _gameState.ApplyDialogueReputation(dialogueCmd.FactionId, dialogueCmd.Delta);
                break;
            case ResolveParleyCommand parleyCmd:
                stateChanged = _gameState.ResolveParley(parleyCmd.Choice);
                break;
            case ChooseBranchCommand branchCmd:
                {
                    var member = _gameState.Party.Members.FirstOrDefault(m => m.Id == branchCmd.CharacterId);
                    if (member.Id != Guid.Empty && member.AwaitingBranchChoice)
                    {
                        stateChanged = _gameState.ChooseBranch(branchCmd.CharacterId, branchCmd.Branch);
                    }
                }
                break;
            case AccuseFactionCommand accuseCmd:
                stateChanged = _gameState.AccuseFaction(accuseCmd.FactionId);
                break;
            case ReadArchiveCommand archiveCmd:
                stateChanged = _gameState.ReadArchive(archiveCmd.ArchiveId) != null;
                break;
            case TransferToCacheCommand toCacheCmd:
                ComponentInventorySystem.TransferToExpeditionCache(_gameState.Party, toCacheCmd.Slot, toCacheCmd.ItemId, toCacheCmd.Count);
                stateChanged = true;
                break;
            case TransferFromCacheCommand fromCacheCmd:
                ComponentInventorySystem.TransferFromExpeditionCache(_gameState.Party, fromCacheCmd.Slot, fromCacheCmd.ItemId, fromCacheCmd.Count);
                stateChanged = true;
                break;
            case DowntimeActionCommand downtimeCmd:
                {
                    var result = _gameState.PerformDowntimeAction(downtimeCmd.CharacterId, downtimeCmd.Action);
                    stateChanged = result != null && result.Success;
                }
                break;
            case ResurrectCharacterCommand resurrectCmd:
                {
                    var result = _gameState.ResurrectCharacter(resurrectCmd.CharacterId);
                    stateChanged = result != null && result.Success;
                }
                break;
            case PayTitheCommand:
                stateChanged = _gameState.PayTithe();
                break;
            case EquipItemCommand equipCmd:
                stateChanged = TryEquip(equipCmd);
                break;
            case UnequipItemCommand unequipCmd:
                stateChanged = TryUnequip(unequipCmd);
                break;
            case VerifyRumorCommand verifyCmd:
                {
                    if (Enum.TryParse<RumorVerificationSource>(verifyCmd.Source, true, out var source))
                    {
                        stateChanged = _gameState.VerifyRumor(verifyCmd.RumorId, source);
                    }
                }
                break;
            default:
                throw new ArgumentException($"Unhandled command type: {cmd.GetType().Name}");
        }

        // Ironman: auto-save after every state-changing action
        if (stateChanged && _gameState.IsIronman && cmd is not SaveGameCommand)
        {
            _gameState.SaveGame(_gameState.SavePath);
        }

        return new CommandResult(stateChanged, clearCombatResult);
    }

    private bool UseConsumable(UseConsumableCommand cmd)
    {
        if (_gameState.Combat == null || _gameState.Mode != GameMode.Combat)
            throw new InvalidOperationException("Cannot use a consumable outside of combat.");
        if (_itemRegistry == null)
            throw new InvalidOperationException("Item registry is unavailable; cannot resolve consumable.");

        var item = _itemRegistry.Get(cmd.ItemId)
            ?? throw new InvalidOperationException($"Unknown item: {cmd.ItemId}");
        if (item.Type != "consumable")
            throw new InvalidOperationException($"Item {cmd.ItemId} is not a consumable.");

        var memberIndex = Array.FindIndex(_gameState.Party.Members, m => m.Id == cmd.ActorId);
        if (memberIndex < 0)
            throw new InvalidOperationException($"Actor {cmd.ActorId} is not an active party member.");

        var member = _gameState.Party.Members[memberIndex];
        if (!ComponentInventorySystem.HasComponent(member.ComponentInventory, cmd.ItemId, 1))
            throw new InvalidOperationException($"Actor does not hold consumable {cmd.ItemId}.");

        var combat = _gameState.Combat;
        var actorIdx = Array.FindIndex(combat.Combatants, c => c.Id == cmd.ActorId);
        if (actorIdx < 0)
            throw new InvalidOperationException($"Actor {cmd.ActorId} is not a combatant.");

        var targetId = cmd.TargetId ?? cmd.ActorId; // default: use on self
        var targetIdx = Array.FindIndex(combat.Combatants, c => c.Id == targetId);
        if (targetIdx < 0)
            throw new InvalidOperationException($"Target {targetId} is not a combatant.");

        var (newTarget, logMessage) = ConsumableSystem.ApplyEffect(
            item, combat.Combatants[actorIdx], combat.Combatants[targetIdx], _gameState._encounterRng);

        var newCombatants = combat.Combatants.ToArray();
        newCombatants[targetIdx] = newTarget;
        var newLog = new List<CombatLogEntry>(combat.Log) { new(cmd.ActorId, logMessage, combat.Round) };
        _gameState.Combat = combat with { Combatants = newCombatants, Log = newLog };

        var newInventory = ComponentInventorySystem.RemoveComponent(member.ComponentInventory, cmd.ItemId, 1);
        _gameState.Party.SetMember(memberIndex, member with { ComponentInventory = newInventory });

        _gameState.EmitActionLog("combat", "consumable_used", new Dictionary<string, string>
        {
            { "actorId", cmd.ActorId.ToString() },
            { "itemId", cmd.ItemId },
            { "targetId", targetId.ToString() }
        });
        return true;
    }

    private bool TryEquip(EquipItemCommand cmd)
    {
        var index = Array.FindIndex(_gameState.Party.Members, m => m.Id == cmd.CharacterId);
        if (index < 0) return false;

        var result = EquipmentSystem.Equip(_gameState.Party.Members[index], cmd.ItemId, cmd.Slot, _itemRegistry);
        if (!result.Success) return false;

        _gameState.Party.SetMember(index, result.Character);
        _gameState.EmitActionLog("inventory", "item_equipped", new Dictionary<string, string>
        {
            { "characterId", cmd.CharacterId.ToString() },
            { "itemId", cmd.ItemId },
            { "slot", cmd.Slot }
        });
        return true;
    }

    private bool TryUnequip(UnequipItemCommand cmd)
    {
        var index = Array.FindIndex(_gameState.Party.Members, m => m.Id == cmd.CharacterId);
        if (index < 0) return false;

        if (!Equipment.IsValidSlot(cmd.Slot)) return false;

        var equippedItem = _gameState.Party.Members[index].Equipment.GetSlot(cmd.Slot);
        var result = EquipmentSystem.Unequip(_gameState.Party.Members[index], cmd.Slot);
        if (!result.Success) return false;

        _gameState.Party.SetMember(index, result.Character);
        _gameState.EmitActionLog("inventory", "item_unequipped", new Dictionary<string, string>
        {
            { "characterId", cmd.CharacterId.ToString() },
            { "itemId", equippedItem ?? "" },
            { "slot", cmd.Slot }
        });
        return true;
    }
}
