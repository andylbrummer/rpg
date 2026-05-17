using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Combat;

/// <summary>
/// Tracks an active rescue expedition in ironman mode.
/// When the active party TPKs, bench characters can attempt rescue.
/// </summary>
public class RescueExpeditionState
{
    public bool IsActive { get; set; } = false;
    public Guid[] RescuePartyIds { get; set; } = Array.Empty<Guid>();
    public string DungeonType { get; set; } = "";
    public Position TpkLocation { get; set; } = new(0, 0);
    public bool Success { get; set; } = false;
    public bool Resolved { get; set; } = false;
}
