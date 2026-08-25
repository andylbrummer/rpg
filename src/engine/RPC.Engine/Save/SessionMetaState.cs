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

    /// <summary>
    /// Clear the fields that belong to a single run, so a new campaign does not inherit them.
    /// <para>
    /// Only <see cref="IsIronman"/> is run-scoped. The rest deliberately survive: <see cref="Meta"/>
    /// is cross-campaign progression by definition, and <see cref="SavePath"/>,
    /// <see cref="MetaPath"/>, <see cref="MetaPersistenceEnabled"/> and <see cref="SettingsHash"/>
    /// describe the session and the build rather than the campaign being played. Resetting those
    /// would point the next campaign's saves somewhere else.
    /// </para>
    /// </summary>
    public void Reset()
    {
        IsIronman = false;
    }
}
