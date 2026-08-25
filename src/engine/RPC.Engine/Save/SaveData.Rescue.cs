namespace RPC.Engine.Save;

/// <summary>
/// An in-flight ironman rescue expedition. Persisted because ironman autosaves on every
/// state-changing command and quitting and resuming is the ordinary way to play it: a resumed run
/// used to come back with the rescue party standing in the dungeon and no expedition, so reaching
/// the site did nothing, the fallen party's equipment was never recovered, and the rescue could
/// neither succeed nor fail for the rest of the run.
/// </summary>
public class SaveRescueExpedition
{
    public bool IsActive { get; set; }
    public string[] RescuePartyIds { get; set; } = Array.Empty<string>();
    public string DungeonType { get; set; } = "";
    public int TpkX { get; set; }
    public int TpkY { get; set; }
    public bool Success { get; set; }
    public bool Resolved { get; set; }
}
