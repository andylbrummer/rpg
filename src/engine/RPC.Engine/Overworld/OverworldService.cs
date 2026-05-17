using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Overworld;
using RPC.Engine.Travel;

namespace RPC.Engine.Overworld;

public class OverworldService
{
    private readonly GameRandom _encounterRng;
    private readonly ClassRegistry? _classRegistry;
    private readonly SynergyRegistry? _synergies;

    public OverworldService(GameRandom encounterRng, ClassRegistry? classRegistry, SynergyRegistry? synergies = null)
    {
        _encounterRng = encounterRng;
        _classRegistry = classRegistry;
        _synergies = synergies;
    }

    public void GenerateOverworld(GameState state, CampaignConfig config)
    {
        state.CampaignConfig = config;
        state.CurrentScheme = CampaignContentLoader.GetSchemeById(config.Scheme.ToString());
        state.CurrentComplication = CampaignContentLoader.GetComplicationById(config.Complication.ToString());
        state.Overworld.GenerateFromConfig(config, _encounterRng, state.CurrentComplication);
        SyncWorldStateFromOverworld(state);

        var partyClasses = state.Party.Members
            .Where(m => m.Id != Guid.Empty)
            .Select(m => m.ClassId)
            .ToArray();
        state.Analytics.RecordCampaignStart(config.Scheme.ToString().ToLowerInvariant(), config.Scheme.ToString(), partyClasses);
    }

    private void SyncWorldStateFromOverworld(GameState state)
    {
        state.WorldState.Settlements = state.Overworld.Nodes.Values
            .Where(n => n.Type == NodeType.Town)
            .ToDictionary(n => n.Id, _ => "pending");

        state.WorldState.AccessibleDungeons = state.Overworld.Nodes.Values
            .Where(n => n.Type == NodeType.Dungeon)
            .Select(n => n.Id)
            .ToList();

        state.WorldState.FactionTerritory.Clear();
        foreach (var node in state.Overworld.Nodes.Values)
        {
            foreach (var faction in node.FactionPresence)
            {
                if (!state.WorldState.FactionTerritory.ContainsKey(faction))
                    state.WorldState.FactionTerritory[faction] = new List<string>();
                if (!state.WorldState.FactionTerritory[faction].Contains(node.Id))
                    state.WorldState.FactionTerritory[faction].Add(node.Id);
            }
        }
    }

    public bool Travel(GameState state, string targetId)
    {
        if (state.CampaignEnded) return false;
        if (state.HasPendingBranchChoices) return false;
        if (state.Heat.IsLockdown)
        {
            state.EmitActionLog("heat", "travel_blocked_lockdown", new Dictionary<string, string>
            {
                { "heat", state.Heat.Value.ToString() }
            });
            return false;
        }
        var fromNodeId = state.Overworld.CurrentNodeId;
        var route = state.Overworld.GetRoute(fromNodeId, targetId);
        if (route != null && !RouteStatusSystem.CanTravel(route))
        {
            state.EmitActionLog("overworld", "travel_blocked", new Dictionary<string, string>
            {
                { "from", fromNodeId },
                { "to", targetId },
                { "reason", route.Status.ToString() }
            });
            return false;
        }
        var changed = state.Overworld.Travel(targetId);
        if (!changed) return false;

        state.EmitActionLog("overworld", "travel_started", new Dictionary<string, string>
        {
            { "from", fromNodeId },
            { "to", targetId },
            { "distance", route?.Distance.ToString() ?? "0" },
            { "routeStatus", route?.Status.ToString() ?? "Open" }
        });

        if (route != null)
        {
            var effectiveDanger = RouteStatusSystem.GetEffectiveDangerRating(route);
            IncrementTurns(state, route.Distance, effectiveDanger);
        }

        ClearTravelEncounters(state);

        if (route != null)
        {
            var effectiveDanger = RouteStatusSystem.GetEffectiveDangerRating(route);
            RollTravelEncounters(state, effectiveDanger, route.Terrain);
        }

        if (state.RolledTravelEncounterCount == 0)
        {
            if (state.Overworld.Nodes.TryGetValue(targetId, out var node) && node.Type == NodeType.Town)
            {
                state.EmitActionLog("overworld", "town_reached", new Dictionary<string, string>
                {
                    { "townId", targetId }
                });
                state._downtimeCompleted.Clear();
                state.CheckWildCardTrigger();
            }
        }

        state.LastUpdate = DateTime.UtcNow;
        return true;
    }

    public void IncrementTurns(GameState state, int amount, int? dangerOverride = null)
    {
        if (state.CampaignEnded || amount <= 0) return;
        var oldTurn = state.Overworld.Turns;
        state.Overworld.Turns = Math.Min(35, state.Overworld.Turns + amount);
        var newTurn = state.Overworld.Turns;

        if (oldTurn < 12 && newTurn >= 12)
        {
            state.EmitActionLog("campaign", "faction_progression", new Dictionary<string, string> { { "milestone", "12" } });
        }
        if (oldTurn < 22 && newTurn >= 22)
        {
            state.EmitActionLog("campaign", "faction_progression", new Dictionary<string, string> { { "milestone", "22" } });
        }

        // Apply route status transitions at turn milestones
        RouteStatusSystem.ApplyTurnMilestoneTransitions(state.Overworld, state.CampaignConfig, _encounterRng);

        // Natural heat decay: -5 per turn
        if (amount > 0 && state.Heat.Value > 0)
        {
            var oldHeat = state.Heat.Value;
            state.Heat.Add(-5 * amount);
            if (state.Heat.Value != oldHeat)
            {
                state.EmitActionLog("heat", "natural_decay", new Dictionary<string, string>
                {
                    { "oldValue", oldHeat.ToString() },
                    { "newValue", state.Heat.Value.ToString() }
                });
            }
        }

        if (state.Overworld.Turns >= 35)
        {
            state.CampaignEnded = true;
            state.CurrentDungeon = null;
            state.Mode = GameMode.Menu;
        }
    }

    private static void ClearTravelEncounters(GameState state)
    {
        state.CurrentTravelEncounter = null;
        state.RolledTravelEncounterCount = 0;
        state.ResolvedTravelEncounterCount = 0;
    }

    private void RollTravelEncounters(GameState state, int dangerRating, string terrain)
    {
        var count = TravelEncounterTable.RollEncounterCount(_encounterRng, dangerRating);
        state.RolledTravelEncounterCount = count;
        if (count == 0) return;

        var encounter = TravelEncounterTable.RollEncounter(_encounterRng, dangerRating, terrain);
        if (encounter == null) return;

        ActivateTravelEncounter(state, encounter);
    }

    private static bool IsPatrolEncounter(string id) => id == "faction_patrol" || id.EndsWith("_patrol");

    private void ActivateTravelEncounter(GameState state, TravelEncounterDef encounter)
    {
        bool hasMarcher = state.Party.Members.Any(m => m.ClassId == "marcher");
        bool hasAshmouth = state.Party.Members.Any(m => m.ClassId == "ashmouth");
        bool hasHollow = state.Party.Members.Any(m => m.ClassId == "hollow");
        bool hasCauterist = state.Party.Members.Any(m => m.ClassId == "cauterist");

        bool surpriseRound = encounter.ResolutionType == TravelResolutionType.Combat
            && encounter.Id == "ambush"
            && !hasMarcher;

        int priceTier = encounter.Id == "merchant" && hasAshmouth ? 1 : 0;

        int repValue = 0;
        string? factionId = encounter.FactionId;
        if (factionId != null)
        {
            repValue = state.Reputation[factionId];
        }

        bool isHostilePatrol = IsPatrolEncounter(encounter.Id) && factionId != null && repValue < 0;

        string[]? options = null;
        string resolutionType = encounter.ResolutionType switch
        {
            TravelResolutionType.Combat => "combat",
            TravelResolutionType.StatTest => "stat_test",
            TravelResolutionType.Dialogue => "dialogue",
            _ => "unknown"
        };

        // Class-specific encounter alternatives
        string? classAlternative = encounter.ClassAlternative;

        // Hollow Liar can talk through smuggler caches
        if (classAlternative == "hollow_liar" && hasHollow && encounter.Id == "smuggler_cache")
        {
            resolutionType = "dialogue";
        }

        // Cauterist Scorcher can burn through environmental hazards
        if (classAlternative == "cauterist_scorcher" && hasCauterist &&
            (encounter.Id == "poison_thicket" || encounter.Id == "fungal_spores" || encounter.Id == "will_o_wisp"))
        {
            resolutionType = "dialogue";
            options = ["Burn through", "Navigate normally"];
        }

        // Marcher Pathfinder can bypass terrain obstacles
        if (classAlternative == "marcher_pathfinder" && hasMarcher &&
            (encounter.Id == "rockslide" || encounter.Id == "narrow_pass" || encounter.Id == "cave_in" || encounter.Id == "sinking_mire"))
        {
            resolutionType = "dialogue";
            options = ["Find alternate route", "Push through"];
        }

        // Ashmouth Broker gets better deals with bog merchants
        if (classAlternative == "ashmouth_broker" && hasAshmouth && encounter.Id == "bog_merchant")
        {
            priceTier = 1;
        }

        if (isHostilePatrol)
        {
            resolutionType = "combat";
        }
        else if (resolutionType == "dialogue" || encounter.ResolutionType == TravelResolutionType.Dialogue)
        {
            resolutionType = "dialogue";
            if (options == null)
            {
                if (IsPatrolEncounter(encounter.Id))
                {
                    options = repValue >= 25
                        ? ["Request intel", "Trade supplies", "Pass safely"]
                        : ["Show papers", "Bribe", "Attack"];
                }
                else
                {
                    options = encounter.Id == "merchant"
                        ? ["Trade", "Ignore", "Rob"]
                        : ["Trade", "Ignore", "Help"];
                }
            }
        }
        else if (resolutionType == "unknown")
        {
            resolutionType = encounter.ResolutionType switch
            {
                TravelResolutionType.Combat => "combat",
                TravelResolutionType.StatTest => "stat_test",
                _ => "unknown"
            };
        }

        state.CurrentTravelEncounter = new TravelEncounterState(
            encounter.Id,
            encounter.Name,
            resolutionType,
            encounter.StatName,
            factionId,
            repValue,
            surpriseRound,
            priceTier,
            options);

        if (isHostilePatrol)
        {
            var patrolEnemies = new[] { new EnemySpawn("faction_soldier", 2) };
            var encounterDef = new EncounterDef(encounter.Id, encounter.Name, patrolEnemies, 15);
            state.Combat = CombatEngine.Enter(state.Party, encounterDef, new GameRandom(_encounterRng.Roll(1, 10000)));
            state.CurrentTravelEncounter = null;

            if (state.Combat.IsFinished)
            {
                state.Mode = GameMode.Menu;
                state.Combat = null;
                ResolveTravelEncounter(state, "auto");
            }
            else
            {
                state.Mode = GameMode.Combat;
                var rng = new GameRandom(_encounterRng.Roll(1, 10000));
                state.Combat = CombatEngine.AutoResolveToPlayerTurn(
                    CombatEngine.Tick(state.Combat, null, rng, _classRegistry, null, _synergies),
                    rng, _classRegistry, null, _synergies);
            }
        }
        else if (encounter.ResolutionType == TravelResolutionType.Combat && encounter.Enemies != null)
        {
            var encounterDef = new EncounterDef(encounter.Id, encounter.Name, encounter.Enemies, 15);
            state.Combat = CombatEngine.Enter(state.Party, encounterDef, new GameRandom(_encounterRng.Roll(1, 10000)));
            state.CurrentTravelEncounter = null;

            if (state.Combat.IsFinished)
            {
                state.Mode = GameMode.Menu;
                state.Combat = null;
                ResolveTravelEncounter(state, "auto");
            }
            else
            {
                state.Mode = GameMode.Combat;
                var rng = new GameRandom(_encounterRng.Roll(1, 10000));
                state.Combat = CombatEngine.AutoResolveToPlayerTurn(
                    CombatEngine.Tick(state.Combat, null, rng, _classRegistry, null, _synergies),
                    rng, _classRegistry, null, _synergies);
            }
        }
    }

    public bool ResolveTravelEncounter(GameState state, string choice)
    {
        if (state.CurrentTravelEncounter == null) return false;

        var encounter = state.CurrentTravelEncounter;
        if (encounter.ResolutionType == "stat_test" && encounter.StatName != null)
        {
            var highestStat = state.Party.Members
                .Where(m => m.IsAlive)
                .Max(m => encounter.StatName switch
                {
                    "strength" => m.BaseStats.Strength,
                    "dexterity" => m.BaseStats.Dexterity,
                    "constitution" => m.BaseStats.Constitution,
                    "intelligence" => m.BaseStats.Intelligence,
                    "willpower" => m.BaseStats.Willpower,
                    _ => 0
                });

            var roll = _encounterRng.Roll(1, 20);
            var success = roll + highestStat >= 15;

            state.EmitActionLog("travel", "stat_test", new Dictionary<string, string>
            {
                { "encounterId", encounter.Id },
                { "stat", encounter.StatName },
                { "highest", highestStat.ToString() },
                { "roll", roll.ToString() },
                { "success", success.ToString() }
            });
        }
        else if (encounter.ResolutionType == "dialogue")
        {
            state.EmitActionLog("travel", "dialogue", new Dictionary<string, string>
            {
                { "encounterId", encounter.Id },
                { "choice", choice },
                { "factionId", encounter.FactionId ?? "none" }
            });

            // Apply reputation effects from travel dialogue choices
            if (encounter.FactionId != null && IsPatrolEncounter(encounter.Id))
            {
                switch (choice)
                {
                    case "Attack":
                        state.Reputation.ApplyDelta(encounter.FactionId, -5, "travel_encounter:attack_patrol");
                        break;
                    case "Request intel":
                    case "Pass safely":
                        state.Reputation.ApplyDelta(encounter.FactionId, 2, "travel_encounter:cooperate");
                        break;
                }
            }
            else if (encounter.Id == "refugees" && choice == "Help")
            {
                state.Reputation.ApplyDelta("bureau", 2, "travel_encounter:help_refugees");
            }
        }

        state.EmitActionLog("overworld", "travel_encounter_resolved", new Dictionary<string, string>
        {
            { "encounterId", encounter.Id },
            { "resolutionType", encounter.ResolutionType },
            { "choice", choice }
        });

        state.ResolvedTravelEncounterCount++;
        state.CurrentTravelEncounter = null;

        if (state.ResolvedTravelEncounterCount < state.RolledTravelEncounterCount)
        {
            var route = state.Overworld.Routes.FirstOrDefault(r =>
                r.From == state.Overworld.CurrentNodeId || r.To == state.Overworld.CurrentNodeId);
            var next = TravelEncounterTable.RollEncounter(_encounterRng, route?.DangerRating ?? 0, route?.Terrain ?? "");
            if (next != null)
            {
                ActivateTravelEncounter(state, next);
            }
        }

        if (state.ResolvedTravelEncounterCount >= state.RolledTravelEncounterCount)
        {
            var node = state.Overworld.Nodes.GetValueOrDefault(state.Overworld.CurrentNodeId);
            if (node?.Type == NodeType.Town)
            {
                state.EmitActionLog("overworld", "town_reached", new Dictionary<string, string>
                {
                    { "townId", state.Overworld.CurrentNodeId }
                });
                state._downtimeCompleted.Clear();
            }
        }

        state.LastUpdate = DateTime.UtcNow;
        return true;
    }
}
