using RPC.Engine.Combat;
using RPC.Engine.Character;
using RPC.Engine.Party;

namespace RPC.Tests;

public class EnemyRegistryTests
{
    private const string RatJson = """
    {
      "id": "rat",
      "name": "Sewer Rat",
      "description": "A bloated rodent.",
      "stats": { "str": 3, "dex": 5, "con": 3, "int": 1, "wil": 2 },
      "hpBase": 8,
      "speed": 4,
      "ai": "basic_melee",
      "abilities": ["bite"],
      "lootTable": [{ "itemId": "rat_tail", "chance": 0.3 }]
    }
    """;

    [Fact]
    public void EnemyRegistry_LoadFromJson_ParsesCorrectly()
    {
        var registry = new EnemyRegistry();
        registry.LoadFromJson("rat", RatJson);

        var rat = registry.Get("rat");
        Assert.NotNull(rat);
        Assert.Equal("Sewer Rat", rat.Name);
        Assert.Equal(8, rat.HpBase);
        Assert.Equal(4, rat.Speed);
        Assert.Equal("basic_melee", rat.Ai);
        Assert.Single(rat.Abilities);
        Assert.Single(rat.LootTable);
    }

    [Fact]
    public void EnemyRegistry_GetUnknown_ReturnsNull()
    {
        var registry = new EnemyRegistry();
        Assert.Null(registry.Get("missing"));
    }

    [Fact]
    public void CombatEngine_Spawn_WithRegistry_UsesEnemyStats()
    {
        var registry = new EnemyRegistry();
        registry.LoadFromJson("rat", RatJson);

        var party = new PartyState();
        party.SetMember(0, new CharacterState(
            new Guid("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"), "Hero", "test", 1, 0,
            new BaseStats(4, 5, 4, 4, 4), 20, Equipment.Empty,
            Array.Empty<string>(), 0));

        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 2) });
        var state = CombatEngine.Enter(party, encounter, new GameRandom(42), registry);

        var enemies = state.Combatants.Where(c => !c.IsPlayer).ToArray();
        Assert.Equal(2, enemies.Length);
        Assert.All(enemies, e => Assert.Contains("Sewer Rat", e.Name));
        Assert.All(enemies, e => Assert.True(e.Hp >= 8 && e.Hp <= 11));
    }
}
