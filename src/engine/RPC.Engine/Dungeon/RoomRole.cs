namespace RPC.Engine.Dungeons;

/// <summary>
/// Structural role of a room within an assembled dungeon, derived from the critical
/// path (entrance → boss). Drives loot bias and (later) interactable/secret placement.
/// </summary>
public enum RoomRole
{
    Entrance,
    Boss,
    Critical,
    SideBranch,
    DeadEnd
}
