using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Commands;
using RPC.Engine.Dungeons;
using RPC.Engine.Inventory;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Town;

namespace RPC.Engine.Services;

public record CommandResult(bool StateChanged, bool ClearCombatResult = false);

public class GameCommandHandler
{
    private readonly GameState _gameState;
    private readonly EncounterTableRegistry _encounterTables;
    private readonly List<RoomSegment> _segments;
    private readonly Dictionary<string, DungeonTemplate> _dungeonTemplates;

    public GameCommandHandler(GameState gameState, EncounterTableRegistry encounterTables, List<RoomSegment> segments, Dictionary<string, DungeonTemplate>? dungeonTemplates = null)
    {
        _gameState = gameState;
        _encounterTables = encounterTables;
        _segments = segments;
        _dungeonTemplates = dungeonTemplates ?? new Dictionary<string, DungeonTemplate>();
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
            case CancelCommand:
                stateChanged = true;
                break;
            case CombatActionCommand combatCmd:
                stateChanged = _gameState.SubmitCombatAction(combatCmd.Action);
                if (_gameState.LastCombatResult != null)
                    clearCombatResult = true;
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
                GenerateDungeon(enterDungeonCmd.DungeonType);
                stateChanged = true;
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

        return new CommandResult(stateChanged, clearCombatResult);
    }

    private void GenerateDungeon(string dungeonType)
    {
        var template = _dungeonTemplates.GetValueOrDefault(dungeonType) ?? new DungeonTemplate(
            dungeonType,
            dungeonType,
            new[] { "entrance", "corridor", "chamber", "dead_end", "boss_room" },
            new[] { "entrance", "corridor", "chamber", "dead_end", "boss_room" },
            8,
            "boss-encounter-1",
            dungeonType,
            dungeonType);

        var seed = dungeonType.GetHashCode();
        var builder = new DungeonBuilder(seed: seed);

        var pool = template.SegmentPool.ToHashSet();
        var ordered = template.SegmentPriority
            .Select(id => _segments.FirstOrDefault(s => s.Id == id))
            .Where(s => s != null)
            .Cast<RoomSegment>()
            .Concat(_segments.Where(s => pool.Contains(s.Id) && !template.SegmentPriority.Contains(s.Id)))
            .ToList();

        foreach (var segment in ordered)
        {
            builder.AddSegment(segment);
        }

        var dungeon = builder.Build(template.Name, template.TargetRooms, _encounterTables, template.EncounterTableId);
        dungeon.WanderingTableId = template.WanderingTableId ?? template.EncounterTableId;
        dungeon.EncounterTableId = template.EncounterTableId;
        TagBossTile(dungeon, template.BossEncounterId);
        _gameState.EnterDungeon(dungeon, dungeonType);
    }

    private static void TagBossTile(Dungeon dungeon, string encounterId)
    {
        for (int x = 0; x < dungeon.Width; x++)
        {
            for (int y = 0; y < dungeon.Height; y++)
            {
                if (dungeon.Tiles[x, y].Type == TileType.Floor)
                {
                    var entrance = new Position(x, y);
                    var neighbors = new[]
                    {
                        entrance.Move(Direction.South),
                        entrance.Move(Direction.North),
                        entrance.Move(Direction.East),
                        entrance.Move(Direction.West)
                    };
                    foreach (var n in neighbors)
                    {
                        if (dungeon.IsValidPosition(n) && dungeon.Tiles[n.X, n.Y].Type == TileType.Floor)
                        {
                            dungeon.Tiles[n.X, n.Y] = dungeon.Tiles[n.X, n.Y] with { EncounterId = encounterId };
                            return;
                        }
                    }
                }
            }
        }
    }
}
