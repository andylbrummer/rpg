using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Party;

namespace RPC.Tests;

public class CombatEngineTests
{
    private static CharacterState MakeChar(string name, int hp, int speed, int row = 0)
        => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
            name, "test", 1, 0,
            new BaseStats(4, speed, 4, 4, 4),
            hp, Equipment.Empty,
            Array.Empty<string>(), row);

    [Fact]
    public void CombatEngine_Enter_CreatesCombatants()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 20, 5));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("goblin", 2) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(42));

        Assert.Equal(CombatPhase.RoundStart, state.Phase);
        Assert.Equal(3, state.Combatants.Length); // 1 player + 2 enemies
        Assert.Single(state.Combatants, c => c.IsPlayer);
    }

    [Fact]
    public void CombatEngine_Initiative_HigherSpeedTendsFirst()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Fast", 20, 10));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("slow", 1) });

        // Run many times to check tendency
        int fastFirst = 0;
        for (int seed = 0; seed < 100; seed++)
        {
            var state = CombatEngine.Enter(party, encounter, new GameRandom(seed));
            if (state.InitiativeOrder[0] == state.Combatants[0].Id)
                fastFirst++;
        }

        Assert.True(fastFirst > 70, $"Fast went first {fastFirst}/100 times");
    }

    [Fact]
    public void CombatEngine_SameSeed_SameOrder()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("A", 20, 5));
        party.SetMember(1, MakeChar("B", 20, 6));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("x", 1) });

        var state1 = CombatEngine.Enter(party, encounter, new GameRandom(7));
        var state2 = CombatEngine.Enter(party, encounter, new GameRandom(7));

        Assert.Equal(state1.InitiativeOrder, state2.InitiativeOrder);
    }

    [Fact]
    public void CombatEngine_FullRound_AttackKillsEnemy()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 20, 10));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));
        var enemy = state.Combatants.First(c => !c.IsPlayer);

        // Tick through phases until our turn
        while (state.CurrentActor?.IsPlayer != true && !state.IsFinished)
            state = CombatEngine.Tick(state, null, new GameRandom(1));

        // Attack the enemy
        state = CombatEngine.Tick(state,
            new CombatAction(state.CurrentActor!.Value.Id, ActionType.Attack, enemy.Id, null, null),
            new GameRandom(1));

        // Tick through resolve and check end
        state = CombatEngine.Tick(state, null, new GameRandom(1));
        state = CombatEngine.Tick(state, null, new GameRandom(1));

        var updatedEnemy = state.Combatants.First(c => c.Id == enemy.Id);
        Assert.True(updatedEnemy.Hp < enemy.Hp);
    }

    [Fact]
    public void CombatEngine_AllEnemiesDead_Victory()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 20, 10));
        var encounter = new EncounterDef("e1", "Test", Array.Empty<EnemySpawn>());

        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));
        state = CombatEngine.Tick(state, null, new GameRandom(1));

        Assert.Equal(CombatPhase.Ended, state.Phase);
        Assert.Contains("Victory", state.Log.Last().Message);
    }

    [Fact]
    public void CombatEngine_RoundIncrements_AfterAllTurns()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("A", 20, 5));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("x", 1) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));
        Assert.Equal(1, state.Round);

        // Simulate passing turns until round increments
        int steps = 0;
        while (state.Round == 1 && steps < 20)
        {
            var actor = state.CurrentActor;
            state = CombatEngine.Tick(state,
                actor != null ? new CombatAction(actor.Value.Id, ActionType.Wait, null, null, null) : null,
                new GameRandom(1));
            steps++;
        }

        Assert.True(state.Round >= 2 || state.IsFinished);
    }
}
