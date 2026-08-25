using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Content;
using RPC.Host.Web.Presenters;

namespace RPC.Host.Web;

public class StatePresenter
{
    /// <summary>
    /// Maximum number of trailing ActionLog entries serialized into a single state snapshot.
    /// Bounds per-frame payload + serialization cost regardless of how long the (reused) host
    /// has been accumulating history. The client only reads entries newer than the last turn
    /// it observed, so a recent-tail window of this size is more than sufficient.
    /// </summary>
    private const int SnapshotActionLogLimit = 200;

    private readonly PartyPresenter _partyPresenter;
    private readonly CombatPresenter _combatPresenter;
    private readonly ItemRegistry _itemRegistry;

    public StatePresenter(ClassRegistry classRegistry, ItemRegistry itemRegistry)
    {
        _partyPresenter = new PartyPresenter(classRegistry, itemRegistry);
        _combatPresenter = new CombatPresenter(classRegistry);
        _itemRegistry = itemRegistry;
    }

    public object CreateStateMessage(GameState state)
    {
        var exploration = ExplorationPresenter.Present(state);
        var party = _partyPresenter.Present(state);
        var combat = _combatPresenter.PresentCombat(state);
        var combatResult = _combatPresenter.PresentCombatResult(state);
        var town = TownPresenter.Present(state);
        var overworld = OverworldPresenter.Present(state);
        var travelEncounter = OverworldPresenter.PresentTravelEncounter(state);

        return new
        {
            type = "state",
            mode = state.Mode.ToString(),
            player = exploration.Player,
            tiles = exploration.Tiles,
            explored = exploration.Explored,
            hasDungeon = exploration.HasDungeon,
            dungeonType = exploration.DungeonType,
            detectedSecrets = exploration.DetectedSecrets,
            breakableWalls = exploration.BreakableWalls,
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
            reputation = CampaignPresenter.PresentReputation(state),
            heat = CampaignPresenter.PresentHeat(state),
            evidence = CampaignPresenter.PresentEvidence(state),
            partyGold = state.PartyGold,
            partyInventory = state.PartyInventory.ToArray(),
            expeditionCache = state.Party.ExpeditionCache.Select(c => Presenters.ItemStackPresenter.Present(c, _itemRegistry)).ToArray(),
            townStorage = state.Party.TownStorage.Select(c => Presenters.ItemStackPresenter.Present(c, _itemRegistry)).ToArray(),
            downtimeCompleted = state.DowntimeCompleted.Select(id => id.ToString()).ToArray(),
            wildCardAlliance = CampaignPresenter.PresentWildCardAlliance(state),
            deadCharacters = _partyPresenter.PresentDeadCharacters(state),
            bench = _partyPresenter.PresentBench(state),
            rosterInfo = _partyPresenter.PresentRosterInfo(state),
            titheTokens = state.TitheTokens,
            tithe = new
            {
                debt = state.Tithe.Debt,
                due = state.Tithe.HasDebt,
                contactsRefuse = state.Tithe.ContactsRefuse,
                componentCostMultiplier = state.Tithe.ComponentCostMultiplier,
                outstandingSinceTurn = state.Tithe.OutstandingSinceTurn,
                late = state.Tithe.OutstandingSinceTurn.HasValue && state.Overworld.Turns > state.Tithe.OutstandingSinceTurn.Value,
                goldSurcharge = (state.Tithe.OutstandingSinceTurn.HasValue && state.Overworld.Turns > state.Tithe.OutstandingSinceTurn.Value)
                    ? (int)System.Math.Ceiling(state.Tithe.Debt * RPC.Engine.Town.TownService.TitheGoldValuePerToken * RPC.Engine.Town.TownService.TitheLateGoldSurchargeRate)
                    : 0
            },
            campaignEnded = state.CampaignEnded,
            isFragileState = state.IsFragileState,
            rescueExpedition = state.RescueExpedition != null ? new
            {
                isActive = state.RescueExpedition.IsActive,
                dungeonType = state.RescueExpedition.DungeonType,
                tpkLocation = new { x = state.RescueExpedition.TpkLocation.X, y = state.RescueExpedition.TpkLocation.Y }
            } : null,
            epilogue = state.CampaignEnded ? state.ResolveEpilogue() : null,
            factionStates = CampaignPresenter.PresentFactionStates(state),
            worldState = CampaignPresenter.PresentWorldState(state),
            // Authoritative synergy-journal state from the campaign Journal (part of
            // the save file). The client reconciles its localStorage journal mirror
            // against this so discovered synergies survive save/load without relying
            // solely on the browser.
            journal = new
            {
                discoveredSynergies = state.Journal.DiscoveryOrder.ToArray()
            },
            // Only the most recent entries are serialized into the snapshot. The stored
            // ActionLog grows for the lifetime of a campaign (cleared on reset), and the
            // host is reused across many e2e runs locally; serializing the full log into
            // every state frame made each snapshot O(total-commands) and ballooned per-test
            // time as the host accumulated history. The client only consumes entries newer
            // than the last turn it saw, so a recent-tail window is sufficient.
            actionLog = state.ActionLog
                .Skip(Math.Max(0, state.ActionLog.Count - SnapshotActionLogLimit))
                .Select(e => new
                {
                    turn = e.Turn,
                    act = e.Act,
                    category = e.Category,
                    type = e.Type,
                    payload = e.Payload
                }).ToArray()
        };
    }
}
