using RPC.Engine.Travel;

namespace RPC.Engine.Combat;

/// <summary>
/// Feature-owned aggregate for the loose combat + encounter session fields that previously lived
/// directly on <see cref="GameState"/>. <see cref="GameState"/> exposes thin facade properties that
/// delegate here, mirroring the ExplorationState / CampaignState aggregate pattern.
/// </summary>
public class CombatSessionState
{
    public CombatState? Combat { get; internal set; }
    public CombatResult? LastCombatResult { get; internal set; }
    public ParleyOffer? CurrentParley { get; internal set; }
    public TravelEncounterState? CurrentTravelEncounter { get; internal set; }
    public int RolledTravelEncounterCount { get; internal set; }
    public int ResolvedTravelEncounterCount { get; internal set; }
    public RescueExpeditionState? RescueExpedition { get; set; }
}
