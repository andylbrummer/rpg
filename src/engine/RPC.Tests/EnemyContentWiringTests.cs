using RPC.Engine;
using RPC.Engine.Combat;
using RPC.Host.Web;

namespace RPC.Tests;

/// <summary>
/// Authored enemy definitions must reach the combatants a real encounter spawns. The registry was
/// only ever constructed in tests: every production <c>CombatEngine.Enter</c> call passed none, so
/// <c>SpawnEnemies</c> took its whole fallback path and every enemy in the shipped game was 10 HP,
/// speed 5, zero power, named by its raw content id, with no abilities, no AI behaviour, no faction
/// modifier, and no "bloom" marker for mid-combat mutation. Ten authored enemies existed and none
/// of them was ever used.
/// </summary>
public class EnemyContentWiringTests
{
    private const string GhoulJson = """
    {
      "id": "test_ghoul",
      "name": "Test Ghoul",
      "description": "A test enemy.",
      "stats": { "strength": 7, "dexterity": 4, "constitution": 6, "intelligence": 2, "willpower": 3 },
      "hpBase": 22,
      "speed": 9,
      "ai": "aggressive",
      "abilities": ["claw"],
      "lootTable": []
    }
    """;

    [Fact]
    public void Bootstrap_LoadsShippedEnemies()
    {
        var content = ContentBootstrap.Load();

        Assert.NotEmpty(content.Enemies.All);
        var mite = content.Enemies.Get("bloom_mite");
        Assert.NotNull(mite);
        Assert.Equal("Bloom Mite", mite!.Name);
        Assert.Equal("pack_hunter", mite.Ai);
    }

    [Fact]
    public void DungeonEncounter_SpawnsEnemiesFromTheContentRegistry()
    {
        var enemies = new EnemyRegistry();
        enemies.LoadFromJson("test_ghoul", GhoulJson);
        var gs = new GameState(seed: 7, enemies: enemies);

        gs.TriggerEncounter(new EncounterDef("e1", "Test", new[] { new EnemySpawn("test_ghoul", 1) }));

        var spawned = Assert.Single(gs.Combat!.Combatants, c => !c.IsPlayer);
        Assert.StartsWith("Test Ghoul", spawned.Name);
        Assert.Equal("aggressive", spawned.AiBehavior);
        Assert.Equal(7, spawned.Power);
        Assert.Contains("claw", spawned.Abilities ?? Array.Empty<string>());
    }

    /// <summary>
    /// The overworld's travel-ambush path builds its own encounter definitions rather than rolling
    /// a table, so it reaches <c>Enter</c> through a second service and needs the same registry.
    /// This pins that the state exposes the injected content to it at all.
    /// </summary>
    [Fact]
    public void GameState_ExposesTheInjectedEnemyRegistry()
    {
        var enemies = new EnemyRegistry();
        enemies.LoadFromJson("test_ghoul", GhoulJson);

        var gs = new GameState(seed: 7, enemies: enemies);

        Assert.Same(enemies, gs.Enemies);
    }

    /// <summary>
    /// Without a registry every enemy collapses to the same anonymous fallback. This is the state
    /// the whole shipped game was in, kept as a test so the fallback stays a deliberate
    /// engine-test convenience rather than something a run can end up in unnoticed.
    /// </summary>
    [Fact]
    public void WithoutARegistry_EnemiesFallBackToUnnamedDefaults()
    {
        var gs = new GameState(seed: 7);

        gs.TriggerEncounter(new EncounterDef("e1", "Test", new[] { new EnemySpawn("test_ghoul", 1) }));

        var spawned = Assert.Single(gs.Combat!.Combatants, c => !c.IsPlayer);
        Assert.StartsWith("test_ghoul", spawned.Name);
        Assert.Null(spawned.AiBehavior);
    }

    /// <summary>
    /// Enemy files spelled their stats "str"/"dex"/"con"/"int"/"wil" while BaseStats names them in
    /// full, so every authored stat block deserialized to all zeros. Nothing caught it because the
    /// registry itself was never used in a run. Class content already used the full names; this
    /// pins the one vocabulary for both.
    /// </summary>
    [Fact]
    public void ShippedEnemies_HaveStatsThatActuallyParsed()
    {
        var registry = new EnemyRegistry();
        registry.LoadFromCatalog(new RPC.Engine.Content.FileSystemCatalog());
        Assert.NotEmpty(registry.All);

        foreach (var enemy in registry.All)
        {
            var stats = enemy.Stats;
            var total = stats.Strength + stats.Dexterity + stats.Constitution
                + stats.Intelligence + stats.Willpower;
            Assert.True(total > 0, $"Enemy '{enemy.Id}' has an all-zero stat block; check the stat key names.");
        }
    }

    [Fact]
    public void EnemyDefinition_ThatDoesNotParse_IsReported()
    {
        var registry = new EnemyRegistry();

        var ex = Assert.Throws<InvalidOperationException>(() => registry.LoadFromJson("broken", "null"));
        Assert.Contains("broken", ex.Message);
    }
}
