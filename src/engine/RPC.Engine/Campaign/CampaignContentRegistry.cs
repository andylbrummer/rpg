using RPC.Engine.Content;

namespace RPC.Engine.Campaign;

/// <summary>
/// Immutable campaign content (schemes + complications) handed to the engine by the host
/// composition root. Replaces the engine reaching into the filesystem via
/// <see cref="CampaignContentLoader"/> on the production path: the host loads the defs through
/// its chosen <see cref="IContentCatalog"/> and injects them here.
/// </summary>
public sealed class CampaignContentRegistry
{
    private readonly IReadOnlyList<SchemeDef> _schemes;
    private readonly IReadOnlyList<ComplicationDef> _complications;

    public CampaignContentRegistry(IReadOnlyList<SchemeDef> schemes, IReadOnlyList<ComplicationDef> complications)
    {
        _schemes = schemes;
        _complications = complications;
    }

    /// <summary>Build a registry from a content catalog (host production path).</summary>
    public static CampaignContentRegistry FromCatalog(IContentCatalog catalog)
        => new(CampaignContentLoader.LoadSchemes(catalog), CampaignContentLoader.LoadComplications(catalog));

    /// <summary>
    /// Build a registry from loose content on disk. Used only as a fallback when no registry was
    /// injected (engine tests that construct <see cref="GameState"/> directly); the host always
    /// injects a catalog-built registry.
    /// </summary>
    public static CampaignContentRegistry FromDisk()
        => new(CampaignContentLoader.LoadSchemes(), CampaignContentLoader.LoadComplications());

    public SchemeDef? GetSchemeById(string id) => _schemes.FirstOrDefault(s => s.Id == id);

    public ComplicationDef? GetComplicationById(string id) => _complications.FirstOrDefault(c => c.Id == id);
}
