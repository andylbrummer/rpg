using RPC.Engine.Campaign;
using RPC.Engine.Combat;
using RPC.Engine.Dungeons;

namespace RPC.Tests;

/// <summary>
/// A content definition that cannot be used must say so at load time. These loaders used to
/// <c>return</c> on anything they could not make sense of, so a mis-authored synergy, secret, or
/// archive vanished between the pack and the game with no message anywhere — the failure only
/// showed up as a pair that never triggered or a wall that never opened, which is indistinguishable
/// from a design decision. Every drop is now a thrown error naming the file.
/// </summary>
public class ContentLoaderFailFastTests
{
    // ---- Synergies ----

    [Fact]
    public void Synergy_WithWrongAbilityCount_IsReported()
    {
        var registry = new SynergyRegistry();
        var json = """
            {"id":"three_way","abilities":["a","b","c"],"effect":{"type":"bonus_damage","value":5}}
            """;

        var ex = Assert.Throws<InvalidOperationException>(() => registry.LoadFromJson(json, "three_way.json"));
        Assert.Contains("three_way.json", ex.Message);
    }

    [Fact]
    public void Synergy_WithoutAbilities_IsReported()
    {
        var registry = new SynergyRegistry();
        var json = """{"id":"no_abilities","effect":{"type":"bonus_damage","value":5}}""";

        Assert.Throws<InvalidOperationException>(() => registry.LoadFromJson(json, "no_abilities.json"));
    }

    /// <summary>
    /// A pair of identical (or empty) ability ids has no lookup key. Registering one used to store
    /// the effect under the empty string, where nothing could ever find it and a second bad
    /// definition would silently overwrite the first.
    /// </summary>
    [Fact]
    public void Synergy_PairingAnAbilityWithItself_IsReported()
    {
        var registry = new SynergyRegistry();

        Assert.Throws<ArgumentException>(() =>
            registry.Register("same", "same", new SynergyEffect("bonus_damage", 5), "self_pair"));
        Assert.Empty(registry.GetAll());
    }

    [Fact]
    public void Synergy_MarkedAnti_IsSkippedWithoutError()
    {
        var registry = new SynergyRegistry();
        var json = """
            {"id":"anti","abilities":["a","b"],"anti":true,"effect":{"type":"bonus_damage","value":0}}
            """;

        registry.LoadFromJson(json, "anti.json");

        Assert.Empty(registry.GetAll());
    }

    // ---- Secrets ----

    [Fact]
    public void Secret_WithoutId_IsReported()
    {
        var registry = new SecretRegistry();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            registry.LoadFromJson("""{"type":"breakable_wall"}""", "nameless.json"));
        Assert.Contains("nameless.json", ex.Message);
    }

    [Fact]
    public void Secret_RegisteredWithoutId_IsReported()
    {
        var registry = new SecretRegistry();

        Assert.Throws<ArgumentException>(() => registry.Register(new SecretDef("", "breakable_wall")));
    }

    // ---- Archives ----

    [Fact]
    public void Archive_WithoutId_IsReported()
    {
        var registry = new ArchiveRegistry();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            registry.LoadFromJson("""{"factionId":"inkblood"}""", "nameless.json"));
        Assert.Contains("nameless.json", ex.Message);
    }

    [Fact]
    public void Archive_RegisteredWithoutId_IsReported()
    {
        var registry = new ArchiveRegistry();

        Assert.Throws<ArgumentException>(() => registry.Register(new FamilyArchiveDef("", "inkblood")));
    }
}
