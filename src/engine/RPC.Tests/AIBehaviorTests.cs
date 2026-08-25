using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Party;

namespace RPC.Tests;

public class AIBehaviorTests
{
    private static CharacterState MakeChar(string name, int hp, int speed, int row = 0, int strength = 4)
        => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
            name, "test", 1, 0,
            new BaseStats(strength, speed, 4, 4, 4),
            hp, Equipment.Empty,
            Array.Empty<string>(), row);

    private static CombatState RunEnemyTurn(string ai, int enemySpeed, CharacterState[] players, string[]? abilities = null, int? enemyRow = null, int seed = 1)
    {
        var party = new PartyState();
        for (int i = 0; i < players.Length; i++)
            party.SetMember(i, players[i]);

        var registry = new EnemyRegistry();
        var abilitiesJson = abilities != null && abilities.Length > 0
            ? "[" + string.Join(", ", abilities.Select(a => $"\"{a}\"")) + "]"
            : "[]";
        var json = $"{{\"id\":\"test_enemy\",\"name\":\"Test Enemy\",\"description\":\"Test\",\"stats\":{{\"str\":4,\"dex\":4,\"con\":4,\"int\":4,\"wil\":4}},\"hpBase\":10,\"speed\":{enemySpeed},\"ai\":\"{ai}\",\"abilities\":{abilitiesJson},\"lootTable\":[]}}";
        registry.LoadFromJson("test_enemy", json);

        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("test_enemy", 1, enemyRow) });
        var rng = new GameRandom(seed);

        var state = CombatEngine.Enter(party, encounter, rng, registry);
        state = CombatEngine.Tick(state, null, rng);
        state = CombatEngine.Tick(state, null, rng);
        return state;
    }

    [Fact]
    public void AI_Aggressive_TargetsLowestHp()
    {
        var players = new[]
        {
            MakeChar("HighHp", 50, 1),
            MakeChar("LowHp", 5, 1)
        };
        var state = RunEnemyTurn("aggressive", 100, players);
        var action = state.PendingAction!;
        Assert.Equal(ActionType.Attack, action.Type);
        Assert.Equal(players[1].Id, action.TargetId);
    }

    [Fact]
    public void AI_Aggressive_WithMeleeAbility_UsesAbility()
    {
        var players = new[]
        {
            MakeChar("Target", 20, 1)
        };
        var state = RunEnemyTurn("aggressive", 100, players, abilities: new[] { "rusty_slash" });
        var action = state.PendingAction!;
        Assert.Equal(ActionType.UseAbility, action.Type);
        Assert.Equal("rusty_slash", action.AbilityId);
    }

    [Fact]
    public void AI_PackHunter_TargetsPlayerWithMostAdjacentEnemies()
    {
        var players = new[]
        {
            MakeChar("FrontRow", 20, 1, row: 0),
            MakeChar("BackRow", 20, 1, row: 1)
        };
        var state = RunEnemyTurn("pack_hunter", 100, players, enemyRow: 0);
        var action = state.PendingAction!;
        Assert.Equal(ActionType.Attack, action.Type);
        Assert.Equal(players[0].Id, action.TargetId);
    }

    [Fact]
    public void AI_RangedPriority_TargetsBackRow()
    {
        var players = new[]
        {
            MakeChar("Front", 20, 1, row: 0),
            MakeChar("Back", 20, 1, row: 1)
        };
        var state = RunEnemyTurn("ranged_priority", 100, players);
        var action = state.PendingAction!;
        Assert.Equal(ActionType.Attack, action.Type);
        Assert.Equal(players[1].Id, action.TargetId);
    }

    [Fact]
    public void AI_RangedPriority_WithRangedAbility_UsesAbility()
    {
        var players = new[]
        {
            MakeChar("Front", 20, 1, row: 0),
            MakeChar("Back", 20, 1, row: 1)
        };
        var state = RunEnemyTurn("ranged_priority", 100, players, abilities: new[] { "bone_arrow" });
        var action = state.PendingAction!;
        Assert.Equal(ActionType.UseAbility, action.Type);
        Assert.Equal("bone_arrow", action.AbilityId);
        Assert.Equal(players[1].Id, action.TargetId);
    }

    [Fact]
    public void AI_Defensive_TargetsHighestThreat()
    {
        var players = new[]
        {
            MakeChar("Weak", 20, 1, strength: 2),
            MakeChar("Strong", 20, 1, strength: 10)
        };
        var state = RunEnemyTurn("defensive", 100, players);
        var action = state.PendingAction!;
        Assert.Equal(ActionType.Defend, action.Type);
        Assert.Equal(players[1].Id, action.TargetId);
    }

    [Fact]
    public void AI_Defensive_WithDefensiveAbility_UsesAbility()
    {
        var players = new[]
        {
            MakeChar("Target", 20, 1)
        };
        var state = RunEnemyTurn("defensive", 100, players, abilities: new[] { "warding_stance" });
        var action = state.PendingAction!;
        Assert.Equal(ActionType.UseAbility, action.Type);
        Assert.Equal("warding_stance", action.AbilityId);
    }

    [Fact]
    public void AI_UnknownBehavior_FallsBackToDefault()
    {
        var players = new[]
        {
            MakeChar("Target", 20, 1)
        };
        var state = RunEnemyTurn("basic_melee", 100, players);
        var action = state.PendingAction!;
        Assert.Equal(ActionType.Attack, action.Type);
        Assert.NotNull(action.TargetId);
    }

    [Fact]
    public void AI_NoBehavior_FallsBackToDefault()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Target", 20, 1));

        var registry = new EnemyRegistry();
        var json = "{\"id\":\"test_enemy\",\"name\":\"Test Enemy\",\"description\":\"Test\",\"stats\":{\"str\":4,\"dex\":4,\"con\":4,\"int\":4,\"wil\":4},\"hpBase\":10,\"speed\":100,\"ai\":null,\"abilities\":[],\"lootTable\":[]}";
        registry.LoadFromJson("test_enemy", json);

        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("test_enemy", 1) });
        var rng = new GameRandom(1);

        var state = CombatEngine.Enter(party, encounter, rng, registry);
        state = CombatEngine.Tick(state, null, rng);
        state = CombatEngine.Tick(state, null, rng);

        var action = state.PendingAction!;
        Assert.Equal(ActionType.Attack, action.Type);
        Assert.NotNull(action.TargetId);
    }
}
