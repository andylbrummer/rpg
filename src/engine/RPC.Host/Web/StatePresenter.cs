using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Content;

namespace RPC.Host.Web;

public class StatePresenter
{
    private static readonly Dictionary<string, string> ClassColors = new()
    {
        ["bonewarden"] = "#8B7355",
        ["stillblade"] = "#6B8E9F",
        ["cauterist"] = "#B85C38",
        ["hollow"] = "#6B6B6B",
    };

    private readonly ClassRegistry _classRegistry;
    private readonly ItemRegistry _itemRegistry;

    public StatePresenter(ClassRegistry classRegistry, ItemRegistry itemRegistry)
    {
        _classRegistry = classRegistry;
        _itemRegistry = itemRegistry;
    }

    private static object SerializeTile(int x, int y, Tile tile)
        => new { x, y, type = tile.Type.ToString(), north = tile.North.ToString(), south = tile.South.ToString(), east = tile.East.ToString(), west = tile.West.ToString() };

    public object CreateStateMessage(GameState state)
    {
        var tiles = new List<object>();
        var explored = new List<object>();
        if (state.CurrentDungeon != null)
        {
            var px = state.Player.Position.X;
            var py = state.Player.Position.Y;
            var sendRadius = 8;

            for (int x = Math.Max(0, px - sendRadius); x < Math.Min(state.CurrentDungeon.Width, px + sendRadius + 1); x++)
            {
                for (int y = Math.Max(0, py - sendRadius); y < Math.Min(state.CurrentDungeon.Height, py + sendRadius + 1); y++)
                {
                    var tile = state.CurrentDungeon.Tiles[x, y];
                    if (tile.Type != TileType.Empty)
                    {
                        tiles.Add(SerializeTile(x, y, tile));
                    }
                }
            }

            foreach (var key in state.ExploredTiles)
            {
                var parts = key.Split(',');
                var x = int.Parse(parts[0]);
                var y = int.Parse(parts[1]);
                var tile = state.CurrentDungeon.Tiles[x, y];
                explored.Add(SerializeTile(x, y, tile));
            }
        }

        var party = state.Party.Members
            .Where(c => c.Id != Guid.Empty)
            .Select((c, i) =>
            {
                var effective = c.GetEffectiveStats(_itemRegistry);
                var classDef = _classRegistry.Get(c.ClassId);
                return new
                {
                    slot = i,
                    id = c.Id.ToString(),
                    name = c.Name,
                    classId = c.ClassId,
                    className = classDef?.Name ?? c.ClassId,
                    color = ClassColors.GetValueOrDefault(c.ClassId, "#888888"),
                    level = c.Level,
                    xp = c.Xp,
                    hp = c.CurrentHp,
                    maxHp = effective.MaxHp,
                    row = c.Row,
                    alive = c.IsAlive,
                    branchChoice = c.BranchChoice,
                    branchLevel6 = c.BranchLevel6,
                    awaitingBranchChoice = c.AwaitingBranchChoice,
                    availableBranches = c.Level < 6
                        ? (classDef?.AvailableBranches ?? classDef?.Branches?.Where(b => b.RequiresBranch == null).Select(b => b.Id).ToArray() ?? Array.Empty<string>())
                        : (classDef?.Branches?.Where(b => b.RequiresBranch == c.BranchChoice).Select(b => b.Id).ToArray() ?? Array.Empty<string>()),
                    branchWarnings = classDef?.Branches?
                        .Where(b => b.RequiresBranch != null && b.FactionGate != null)
                        .Select(b => b.RequiresBranch)
                        .Distinct()
                        .ToArray() ?? Array.Empty<string>(),
                    stats = new
                    {
                        strength = c.BaseStats.Strength,
                        dexterity = c.BaseStats.Dexterity,
                        constitution = c.BaseStats.Constitution,
                        intelligence = c.BaseStats.Intelligence,
                        willpower = c.BaseStats.Willpower,
                        maxHp = effective.MaxHp,
                        speed = effective.Speed,
                        accuracy = effective.Accuracy,
                        evade = effective.Evade,
                        power = effective.Power,
                    },
                    equipment = new
                    {
                        mainHand = c.Equipment.MainHand,
                        offHand = c.Equipment.OffHand,
                        armor = c.Equipment.Armor,
                        accessory1 = c.Equipment.Accessory1,
                        accessory2 = c.Equipment.Accessory2,
                    },
                    knownAbilities = c.KnownAbilities,
                    availableAbilities = classDef?.Abilities
                        .Where(a => a.IsAvailableInRow(c.Row))
                        .Select(a => a.Id)
                        .ToArray() ?? Array.Empty<string>(),
                    abilities = classDef?.Abilities.Select(a => new { id = a.Id, name = a.Name, branch = a.Branch }).ToArray() ?? Array.Empty<object>(),
                    tempModifiers = c.TempModifiers.Select(m => new { stat = m.Stat, delta = m.Delta, duration = m.Duration, source = m.Source }).ToArray(),
                    componentInventory = c.ComponentInventory.Select(ci => new { itemId = ci.ItemId, count = ci.Count, maxStack = ci.MaxStack }).ToArray(),
                };
            }).ToArray();

        object? combat = null;
        if (state.Mode == GameMode.Combat && state.Combat != null)
        {
            var c = state.Combat;
            combat = new
            {
                phase = c.Phase.ToString(),
                round = c.Round,
                combatants = c.Combatants.Select(x =>
                {
                    CharacterState? member = x.IsPlayer ? state.Party.Members.FirstOrDefault(m => m.Id == x.Id) : (CharacterState?)null;
                    var classDef = member?.ClassId is not null ? _classRegistry.Get(member.Value.ClassId) : null;
                    return new
                    {
                        id = x.Id,
                        name = x.Name,
                        isPlayer = x.IsPlayer,
                        classId = member?.ClassId,
                        hp = x.Hp,
                        maxHp = x.MaxHp,
                        speed = x.Speed,
                        row = x.Row,
                        alive = x.IsAlive,
                        isCurrent = c.CurrentActor?.Id == x.Id,
                        abilities = classDef?.Abilities
                            .Where(a => member?.KnownAbilities.Contains(a.Id) == true && a.IsAvailableInRow(x.Row))
                            .Select(a => new
                            {
                                id = a.Id,
                                name = a.Name,
                                range = a.Effect.Range,
                                target = a.Effect.Target,
                                requiredRow = a.RequiredRow
                            }).ToArray() ?? Array.Empty<object>(),
                        tempModifiers = x.TempModifiers.Select(m => new { stat = m.Stat, delta = m.Delta, duration = m.Duration, source = m.Source }).ToArray(),
                    };
                }).ToArray(),
                initiativeOrder = c.InitiativeOrder,
                currentTurnIndex = c.CurrentTurnIndex,
                log = c.Log.Select(l => new { actor = l.ActorId, message = l.Message, round = l.Round }).ToArray(),
                isFinished = c.IsFinished
            };
        }

        object? combatResult = null;
        if (state.LastCombatResult != null)
        {
            var r = state.LastCombatResult;
            combatResult = new
            {
                victory = r.Victory,
                xpGained = r.XpGained,
                levelUps = r.LevelUps,
                roundCount = r.RoundCount
            };
        }

        var town = new
        {
            currentTownId = state.Town.CurrentTownId,
            availableMissions = state.Town.AvailableMissions.Select(m => new
            {
                id = m.Id,
                title = m.Title,
                description = m.Description,
                minLevel = m.MinLevel,
                rewards = m.Rewards,
                repReward = m.RepReward,
                factionId = m.FactionId
            }).ToArray(),
            vendorStock = state.Town.VendorStock.Select(v => new
            {
                itemId = v.ItemId,
                name = v.Name,
                price = v.Price,
                quantity = v.Quantity
            }).ToArray(),
            factionVendors = state.Town.FactionVendors.Select(fv => new
            {
                factionId = fv.FactionId,
                name = fv.Name,
                threshold = fv.Threshold,
                stock = fv.Stock.Select(v => new
                {
                    itemId = v.ItemId,
                    name = v.Name,
                    price = v.Price,
                    quantity = v.Quantity
                }).ToArray()
            }).ToArray(),
            factionContacts = state.Town.FactionContacts.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                factionId = c.FactionId,
                portrait = c.Portrait,
                attitude = state.Reputation.GetAttitudeTier(c.FactionId).ToString().ToLowerInvariant()
            }).ToArray(),
            tavernRoster = state.Town.TavernRoster.Select(r => new
            {
                id = r.Id,
                name = r.Name,
                classId = r.ClassId,
                level = r.Level,
                baseStats = new
                {
                    strength = r.BaseStats.Strength,
                    dexterity = r.BaseStats.Dexterity,
                    constitution = r.BaseStats.Constitution,
                    intelligence = r.BaseStats.Intelligence,
                    willpower = r.BaseStats.Willpower
                },
                cost = r.Cost
            }).ToArray(),
            viewedMissions = state.Town.ViewedMissions.ToArray(),
            questLog = state.Town.QuestLog.Select(q => new
            {
                id = q.Id,
                title = q.Title,
                description = q.Description,
                repReward = q.RepReward,
                factionId = q.FactionId,
                status = q.Status.ToString().ToLowerInvariant()
            }).ToArray(),
            rumors = state.Town.Rumors.Select(r => new
            {
                id = r.Id,
                text = r.Text,
                truthStatus = r.TruthStatus.ToString().ToLowerInvariant(),
                verified = r.Verified,
                verificationResult = r.VerificationResult,
                relatedContentId = r.RelatedContentId,
                relatedFactionId = r.RelatedFactionId
            }).ToArray()
        };

        var overworld = new
        {
            currentNodeId = state.Overworld.CurrentNodeId,
            nodes = state.Overworld.Nodes.Values.Select(n => new
            {
                id = n.Id,
                name = n.Name,
                type = n.Type.ToString().ToLowerInvariant(),
                factionPresence = n.FactionPresence,
                dungeonTemplateId = n.DungeonTemplateId
            }).ToArray(),
            routes = state.Overworld.Routes.Select(r => new
            {
                from = r.From,
                to = r.To,
                distance = r.Distance,
                dangerRating = r.DangerRating,
                terrain = r.Terrain,
                status = r.Status.ToString().ToLowerInvariant()
            }).ToArray(),
            turns = state.Overworld.Turns,
            currentAct = state.CurrentAct
        };

        object? travelEncounter = null;
        if (state.CurrentTravelEncounter != null)
        {
            var te = state.CurrentTravelEncounter;
            travelEncounter = new
            {
                id = te.Id,
                name = te.Name,
                resolutionType = te.ResolutionType,
                statName = te.StatName,
                factionId = te.FactionId,
                reputationValue = te.ReputationValue,
                hasSurpriseRound = te.HasSurpriseRound,
                priceTier = te.PriceTier,
                options = te.Options
            };
        }

        return new
        {
            type = "state",
            mode = state.Mode.ToString(),
            player = new
            {
                x = state.Player.Position.X,
                y = state.Player.Position.Y,
                facing = state.Player.Facing.ToString()
            },
            tiles,
            explored,
            hasDungeon = state.CurrentDungeon != null,
            dungeonType = state.CurrentDungeonType,
            party,
            combat,
            combatResult,
            town,
            overworld,
            travelEncounter,
            pendingParley = state.CurrentParley != null ? new
            {
                encounterId = state.CurrentParley.EncounterId,
                factionId = state.CurrentParley.FactionId,
                options = state.CurrentParley.Options
            } : null,
            reputation = state.Reputation.ToDictionary(r => r.Key, r => r.Value),
            heat = new
            {
                value = state.Heat.Value,
                tier = state.Heat.Tier.ToString().ToLowerInvariant()
            },
            evidence = new
            {
                suspectedFaction = state.Evidence.SuspectedFaction,
                canConfront = state.Evidence.Counters.Values.Any(v => v >= 5),
                canAccuse = state.Evidence.Counters.Values.Any(v => v >= 7),
                hasIrrefutableProof = state.Evidence.Counters.Values.Any(v => v >= 10),
                accusedFaction = state.AccusedFaction,
                mastermindRevealed = state.CampaignConfig != null && state.AccusedFaction == state.CampaignConfig.Mastermind,
                mastermindAdvantage = state.MastermindAdvantage,
                finalDungeonUnlocked = state.FinalDungeonUnlocked
            },
            partyGold = state.PartyGold,
            partyInventory = state.PartyInventory.ToArray(),
            expeditionCache = state.Party.ExpeditionCache.Select(c => new { itemId = c.ItemId, count = c.Count, maxStack = c.MaxStack }).ToArray(),
            downtimeCompleted = state.DowntimeCompleted.Select(id => id.ToString()).ToArray(),
            wildCardAlliance = new
            {
                status = state.WildCardAllianceStatus.ToString().ToLowerInvariant(),
                factionId = state.WildCardFactionId,
                turn = state.WildCardAllianceTurn
            },
            deadCharacters = state.Party.DeadCharacters.Select(c =>
            {
                var (goldCost, titheCost, _, _) = c.ResurrectionAttempts switch
                {
                    0 => (500, 1, 1, false),
                    1 => (1500, 2, 2, true),
                    _ => (0, 0, 0, false)
                };
                return new
                {
                    id = c.Id.ToString(),
                    name = c.Name,
                    classId = c.ClassId,
                    level = c.Level,
                    resurrectionAttempts = c.ResurrectionAttempts,
                    branchAdvancementLocked = c.BranchAdvancementLocked,
                    resurrectionCost = goldCost,
                    titheTokenCost = titheCost
                };
            }).ToArray(),
            titheTokens = state.TitheTokens,
            campaignEnded = state.CampaignEnded,
            factionStates = CampaignConfig.FactionPool.ToDictionary(
                f => f,
                f => state.GetFactionState(f).ToString().ToLowerInvariant()),
            worldState = new
            {
                settlements = state.WorldState.Settlements,
                accessibleDungeons = state.WorldState.AccessibleDungeons,
                factionTerritory = state.WorldState.FactionTerritory
            },
            actionLog = state.ActionLog.Select(e => new
            {
                turn = e.Turn,
                category = e.Category,
                type = e.Type,
                payload = e.Payload
            }).ToArray()
        };
    }
}
