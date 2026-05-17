using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Content;
using RPC.Host.Web.Presenters;

namespace RPC.Host.Web;

public class StatePresenter
{
    private readonly PartyPresenter _partyPresenter;
    private readonly CombatPresenter _combatPresenter;

    public StatePresenter(ClassRegistry classRegistry, ItemRegistry itemRegistry)
    {
        _partyPresenter = new PartyPresenter(classRegistry, itemRegistry);
        _combatPresenter = new CombatPresenter(classRegistry);
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
            expeditionCache = state.Party.ExpeditionCache.Select(c => new { itemId = c.ItemId, count = c.Count, maxStack = c.MaxStack }).ToArray(),
            downtimeCompleted = state.DowntimeCompleted.Select(id => id.ToString()).ToArray(),
            wildCardAlliance = CampaignPresenter.PresentWildCardAlliance(state),
            deadCharacters = _partyPresenter.PresentDeadCharacters(state),
            titheTokens = state.TitheTokens,
            campaignEnded = state.CampaignEnded,
            isFragileState = state.IsFragileState,
            epilogue = state.CampaignEnded ? EpilogueGenerator.Generate(state) : null,
            factionStates = CampaignPresenter.PresentFactionStates(state),
            worldState = CampaignPresenter.PresentWorldState(state),
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
