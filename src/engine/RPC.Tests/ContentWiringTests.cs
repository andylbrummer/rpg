using RPC.Content;
using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Content;
using RPC.Engine.Dungeons;
using RPC.Host.Web;

namespace RPC.Tests;

/// <summary>
/// Content that ships in the pack must reach a running game. Secrets and Family Archives were
/// authored, packed by the content-pack tool, and covered by loader tests that read them straight
/// off disk — but nothing on the host path ever populated <see cref="GameState.Secrets"/> or
/// <see cref="GameState.Archives"/>, so in a real run every secret was undiscoverable and every
/// archive unreadable. These tests pin the wiring end to end: the bootstrap loads the directories,
/// and a game state built from that content answers for the definitions it was given.
/// </summary>
public class ContentWiringTests
{
    private static IContentCatalog LooseCatalog() => new FileSystemCatalog();

    [Fact]
    public void Bootstrap_LoadsShippedSecrets()
    {
        var content = ContentBootstrap.Load();

        Assert.NotEmpty(content.Secrets.All);
        var wall = content.Secrets.Get("cartographer_cracked_wall");
        Assert.NotNull(wall);
        Assert.Equal("breakable_wall", wall!.Type);
    }

    [Fact]
    public void Bootstrap_LoadsShippedArchives()
    {
        var content = ContentBootstrap.Load();

        Assert.NotEmpty(content.Archives.All);
        var ledger = content.Archives.Get("compact_ancestral_ledger");
        Assert.NotNull(ledger);
        Assert.Equal("inkblood", ledger!.FactionId);
    }

    [Fact]
    public void SecretRegistry_LoadsFromCatalog()
    {
        var registry = new SecretRegistry();
        registry.LoadFromCatalog(LooseCatalog());

        Assert.NotNull(registry.Get("cartographer_cracked_wall"));
        Assert.NotNull(registry.Get("thornwick_family_vault"));
    }

    [Fact]
    public void ArchiveRegistry_LoadsFromCatalog()
    {
        var registry = new ArchiveRegistry();
        registry.LoadFromCatalog(LooseCatalog());

        Assert.NotNull(registry.Get("compact_ancestral_ledger"));
    }

    [Fact]
    public void GameState_SeededWithContentSecrets_CanDiscoverThem()
    {
        var secrets = new SecretRegistry();
        secrets.Register(new SecretDef("shipped-secret", "breakable_wall"));

        var gs = new GameState(seed: 1, secrets: secrets);

        Assert.NotNull(gs.Secrets.Get("shipped-secret"));
    }

    [Fact]
    public void GameState_SeededWithContentArchives_CanReadThem()
    {
        var archives = new ArchiveRegistry();
        archives.Register(new FamilyArchiveDef("shipped-archive", "inkblood", RepReward: 5));

        var gs = new GameState(seed: 1, archives: archives);

        Assert.NotNull(gs.Archives.Get("shipped-archive"));
    }

    /// <summary>
    /// Starting a new campaign clears the run's discovery state, not the content it discovers
    /// from. Reset used to Clear() both registries outright, which — once they were content-fed —
    /// would leave every campaign after the first with no secrets and no archives at all.
    /// </summary>
    [Fact]
    public void Reset_KeepsContentDefinitions_AndDropsRunRegistrations()
    {
        var secrets = new SecretRegistry();
        secrets.Register(new SecretDef("shipped-secret", "breakable_wall"));
        var archives = new ArchiveRegistry();
        archives.Register(new FamilyArchiveDef("shipped-archive", "inkblood"));

        var gs = new GameState(seed: 1, secrets: secrets, archives: archives);
        gs.Secrets.Register(new SecretDef("run-only-secret", "cache"));

        gs.Reset();

        Assert.NotNull(gs.Secrets.Get("shipped-secret"));
        Assert.NotNull(gs.Archives.Get("shipped-archive"));
        Assert.Null(gs.Secrets.Get("run-only-secret"));
    }

    /// <summary>
    /// The seed is a copy: registering into one run's state must not write back into the shared
    /// content registry the host hands to every game state.
    /// </summary>
    [Fact]
    public void GameState_DoesNotWriteBackIntoTheContentRegistry()
    {
        var secrets = new SecretRegistry();
        secrets.Register(new SecretDef("shipped-secret", "breakable_wall"));

        var gs = new GameState(seed: 1, secrets: secrets);
        gs.Secrets.Register(new SecretDef("run-only-secret", "cache"));

        Assert.Null(secrets.Get("run-only-secret"));
    }
}
