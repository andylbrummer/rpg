using RPC.Engine.Combat;
using RPC.Engine.Overworld;

namespace RPC.Engine.Campaign;

public class EventScheduler
{
    private readonly CampaignService _campaignService;

    public EventScheduler(CampaignService campaignService)
    {
        _campaignService = campaignService;
    }

    public void Tick(GameState state)
    {
        // Campaign events fire during active play, not while in town/menu
        if (state.Mode == GameMode.Menu) return;

        var scheme = state.CurrentScheme;
        var complication = state.CurrentComplication;
        var config = state.CampaignConfig;
        if (config == null) return;

        var turn = state.Overworld.Turns;

        // Check scheme events
        if (scheme?.Events != null)
        {
            foreach (var evt in scheme.Events)
            {
                if (state.Campaign.FiredEvents.Contains(evt.Id)) continue;
                if (turn < evt.TurnThreshold) continue;

                if (ShouldFireEvent(evt, state))
                {
                    ExecuteEvent(state, evt);
                    state.Campaign.FiredEvents.Add(evt.Id);
                }
            }
        }

        // Check complication events
        if (complication?.Events != null)
        {
            foreach (var evt in complication.Events)
            {
                if (state.Campaign.FiredEvents.Contains(evt.Id)) continue;
                if (turn < evt.TurnThreshold) continue;

                if (ShouldFireEvent(evt, state))
                {
                    ExecuteEvent(state, evt);
                    state.Campaign.FiredEvents.Add(evt.Id);
                }
            }
        }
    }

    private bool ShouldFireEvent(CampaignEventDef evt, GameState state)
    {
        if (state.CampaignConfig == null) return false;
        // Scheme/complication events fire unconditionally at their turn threshold.
        // Faction-state-transition events would be handled separately by transition triggers.
        return state.Overworld.Turns >= evt.TurnThreshold;
    }

    private static string? ResolveFactionRole(string? role, CampaignConfig? config)
    {
        if (config == null || string.IsNullOrEmpty(role)) return null;
        return role.ToLowerInvariant() switch
        {
            "patron" => config.Patron,
            "threat" => config.Threat,
            "mastermind" => config.Mastermind,
            "wildcard" => config.WildCard,
            _ => role
        };
    }

    private void ExecuteEvent(GameState state, CampaignEventDef evt)
    {
        var factionId = ResolveFactionRole(evt.FactionInvolved, state.CampaignConfig);
        var targetFaction = evt.TargetFaction != null
            ? ResolveFactionRole(evt.TargetFaction, state.CampaignConfig)
            : null;

        switch (evt.Effect.ToLowerInvariant())
        {
            case "reputation_change":
                if (targetFaction != null && evt.ReputationDelta != 0)
                {
                    _campaignService.ApplyReputationDelta(state, targetFaction, evt.ReputationDelta, $"event:{evt.Id}");
                }
                break;

            case "reveal_node":
                if (evt.TargetNodeType != null)
                {
                    RevealNodeOfType(state, evt.TargetNodeType);
                }
                break;

            case "unlock_dungeon":
                UnlockDungeon(state, evt.DungeonType);
                break;

            case "world_state_change":
                if (evt.WorldStateKey != null)
                {
                    state.WorldState.Settlements[evt.WorldStateKey] = evt.WorldStateValue ?? "changed";
                }
                break;

            case "final_dungeon_unlocked":
                if (state.AccusedFaction == state.CampaignConfig?.Mastermind)
                {
                    _campaignService.UnlockFinalDungeon(state);
                }
                break;

            case "spawn_encounter":
                // Travel encounters are rolled separately during travel;
                // this just emits a log that an encounter is available.
                state.EmitActionLog("event", "encounter_spawned", new Dictionary<string, string>
                {
                    { "eventId", evt.Id },
                    { "encounterType", evt.EncounterType ?? "generic" }
                });
                break;

            case "route_contest":
                if (factionId != null)
                {
                    RouteStatusSystem.ApplyFactionActionTransition(state.Overworld, factionId, new GameRandom(state.Overworld.Turns));
                }
                break;

            default:
                state.EmitActionLog("event", "unknown_effect", new Dictionary<string, string>
                {
                    { "eventId", evt.Id },
                    { "effect", evt.Effect }
                });
                break;
        }

        state.EmitActionLog("campaign", "event_fired", new Dictionary<string, string>
        {
            { "eventId", evt.Id },
            { "eventName", evt.Name },
            { "factionId", factionId ?? "none" },
            { "turn", state.Overworld.Turns.ToString() }
        });
    }

    private static void RevealNodeOfType(GameState state, string nodeType)
    {
        var targetType = nodeType.ToLowerInvariant() switch
        {
            "dungeon_entrance" => NodeType.Dungeon,
            "town" => NodeType.Town,
            _ => NodeType.Pass
        };

        var hidden = state.Overworld.Nodes.Values
            .Where(n => n.Type == targetType)
            .FirstOrDefault();

        if (hidden != null)
        {
            state.EmitActionLog("event", "node_revealed", new Dictionary<string, string>
            {
                { "nodeId", hidden.Id },
                { "nodeType", hidden.Type.ToString() }
            });
        }
    }

    private static void UnlockDungeon(GameState state, string? dungeonType)
    {
        if (string.IsNullOrEmpty(dungeonType)) return;

        var dungeon = state.Overworld.Nodes.Values
            .FirstOrDefault(n => n.Type == NodeType.Dungeon && n.DungeonTemplateId == dungeonType);

        if (dungeon != null && !state.WorldState.AccessibleDungeons.Contains(dungeon.Id))
        {
            state.WorldState.AccessibleDungeons.Add(dungeon.Id);
            state.EmitActionLog("event", "dungeon_unlocked", new Dictionary<string, string>
            {
                { "dungeonId", dungeon.Id },
                { "template", dungeonType }
            });
        }
    }
}
