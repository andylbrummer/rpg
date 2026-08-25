using RPC.Engine;
using RPC.Engine.Combat;
using RPC.Engine.Dungeons;

namespace RPC.Tests;

/// <summary>
/// Confirms content registries are instance-scoped per session (closes QQD6GY2UOVlH): two
/// independently-constructed engine instances must not share mutable content state through a
/// hidden static singleton.
/// </summary>
public class ContentIsolationTests
{
    [Fact]
    public void TwoGameStates_DoNotShareSecretRegistry()
    {
        var stateA = new GameState(seed: 1);
        var stateB = new GameState(seed: 1);

        Assert.NotSame(stateA.Secrets, stateB.Secrets);

        stateA.Secrets.Register(new SecretDef("secret-a", "cache"));

        // Mutating one session's content registry must not leak into another session.
        Assert.NotNull(stateA.Secrets.Get("secret-a"));
        Assert.Null(stateB.Secrets.Get("secret-a"));
        Assert.Empty(stateB.Secrets.All);
    }

    [Fact]
    public void TwoSynergyRegistries_AreIndependent()
    {
        var registryA = new SynergyRegistry();
        var registryB = new SynergyRegistry();

        registryA.Register("flame", "oil", new SynergyEffect("damage", 5), id: "ignite");

        Assert.NotNull(registryA.Lookup("flame", "oil"));
        Assert.Null(registryB.Lookup("flame", "oil"));
        Assert.Empty(registryB.GetAll());
    }
}
