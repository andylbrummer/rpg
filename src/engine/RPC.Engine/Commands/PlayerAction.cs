using RPC.Engine.Combat;

namespace RPC.Engine.Commands;

public class PlayerAction
{
    public string Type { get; set; } = "";
    public CombatAction? Action { get; set; }
    public string? DungeonType { get; set; }
    public int? Slot { get; set; }
    public string? TargetId { get; set; }
    public int? Value { get; set; }
    public string? Branch { get; set; }
    public string? DowntimeAction { get; set; }
    public string? Source { get; set; }
}
