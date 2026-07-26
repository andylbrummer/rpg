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

    /// <summary>
    /// Drop everything belonging to the campaign that just ended. Owning this here — rather than
    /// letting <see cref="GameState.Reset"/> poke the fields one by one — is what keeps a newly
    /// added field from being silently inherited by the next run, which is how an open parley and
    /// an in-flight rescue expedition were surviving a reset.
    /// </summary>
    public void Reset()
    {
        Combat = null;
        LastCombatResult = null;
        CurrentParley = null;
        CurrentTravelEncounter = null;
        RolledTravelEncounterCount = 0;
        ResolvedTravelEncounterCount = 0;
        RescueExpedition = null;
    }
}
