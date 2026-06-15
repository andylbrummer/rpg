namespace RPC.Engine.Party;

/// <summary>
/// Feature-owned aggregate for the loose party-economy fields that previously lived directly on
/// <see cref="GameState"/>. <see cref="GameState"/> exposes thin facade properties that delegate
/// here, mirroring the ExplorationState / CampaignState / CombatSessionState aggregate pattern.
/// </summary>
public class EconomyState
{
    public int Gold { get; set; } = 500;
    public int TitheTokens { get; set; } = 0;
    public List<string> Inventory { get; set; } = new();
}
