namespace RPC.Engine.Save;

/// <summary>
/// Feature-owned aggregate for the loose save / meta-progression / settings session fields that
/// previously lived directly on <see cref="GameState"/>. <see cref="GameState"/> exposes thin facade
/// properties that delegate here, mirroring the ExplorationState / CampaignState / CombatSessionState
/// / EconomyState aggregate pattern.
/// </summary>
public class SessionMetaState
{
    /// <summary>Content/settings hash captured in the save payload.</summary>
    public string? SettingsHash { get; set; }

    /// <summary>True when the run is in ironman mode (autosave + permadeath rescue flow).</summary>
    public bool IsIronman { get; set; } = false;

    /// <summary>Disk path for the run's save file. Defaults to the shared <see cref="SaveSystem.SavePath"/>.</summary>
    public string SavePath { get; set; } = SaveSystem.SavePath;

    /// <summary>
    /// Cross-campaign meta-progression for the current session. Defaults to an empty instance so
    /// applying it to a new run is a no-op until populated. Disk persistence is opt-in via
    /// <see cref="MetaPersistenceEnabled"/> so headless tests never read or write the shared meta file.
    /// </summary>
    public MetaProgression Meta { get; set; } = new();

    /// <summary>When true, campaign start loads and campaign end saves <see cref="Meta"/> to disk.</summary>
    public bool MetaPersistenceEnabled { get; set; }

    /// <summary>Override for the meta-save file path; null uses <see cref="MetaProgressionStore.DefaultPath"/>.</summary>
    public string? MetaPath { get; set; }
}
