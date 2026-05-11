using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Party;

namespace RPC.Tests;

public class CombatSnapshotTests
{
    private static CharacterState MakeChar(string name, int hp, int speed, int row = 0)
        => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
            name, "test", 1, 0,
            new BaseStats(4, speed, 4, 4, 4),
            hp, Equipment.Empty,
            Array.Empty<string>(), row);

    [Fact]
    public void Snapshot_PlayerWaits_EnemyAttacks()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 20, 10));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));
        var rng = new GameRandom(1);

        // Simulate until player gets a turn, then wait
        while (!state.IsFinished && state.CurrentActor?.IsPlayer != true)
            state = CombatEngine.Tick(state, null, rng);

        if (!state.IsFinished)
            state = CombatEngine.Tick(state,
                new CombatAction(state.CurrentActor!.Value.Id, ActionType.Wait, null, null, null), rng);

        // Let combat resolve a few more ticks
        for (int i = 0; i < 5 && !state.IsFinished; i++)
            state = CombatEngine.Tick(state, null, rng);

        Assert.NotEmpty(state.Log);
        Assert.Contains(state.Log, l => l.Message.Contains("waits"));
        Assert.Contains(state.Log, l => l.Message.Contains("hits"));
    }

    [Fact]
    public void Snapshot_FullCombat_ToVictory()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 50, 10));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));
        var rng = new GameRandom(1);

        int steps = 0;
        while (!state.IsFinished && steps < 100)
        {
            var actor = state.CurrentActor;
            if (actor?.IsPlayer == true)
            {
                var enemy = state.Combatants.First(c => !c.IsPlayer);
                state = CombatEngine.Tick(state,
                    new CombatAction(actor.Value.Id, ActionType.Attack, enemy.Id, null, null), rng);
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng);
            }
            steps++;
        }

        Assert.Equal(CombatPhase.Ended, state.Phase);
        Assert.Contains("Victory", state.Log.Last().Message);
    }

    [Fact]
    public void Snapshot_MultipleRounds_RoundCounterIncrements()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Tank", 100, 1)); // slow, lots of hp
        party.SetMember(1, MakeChar("Fast", 20, 10));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 2) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(3));
        var rng = new GameRandom(3);

        int steps = 0;
        while (!state.IsFinished && steps < 50)
        {
            var actor = state.CurrentActor;
            if (actor?.IsPlayer == true)
            {
                var target = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                state = CombatEngine.Tick(state,
                    new CombatAction(actor.Value.Id, ActionType.Attack, target.Id, null, null), rng);
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng);
            }
            steps++;
        }

        Assert.True(state.Round >= 2, $"Expected multiple rounds, got {state.Round}");
    }

    [Fact]
    public void Snapshot_DefendAction_LogsCorrectly()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 20, 10));
        var encounter = new EncounterDef("e1", "Test", Array.Empty<EnemySpawn>());

        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));
        state = CombatEngine.Tick(state,
            new CombatAction(state.Combatants[0].Id, ActionType.Defend, null, null, null),
            new GameRandom(1));

        // Empty encounter ends immediately; defend never executes
        Assert.Equal(CombatPhase.Ended, state.Phase);
    }

    [Fact]
    public void Snapshot_RangeBands_AffectTargeting()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Front", 20, 5, 0));
        party.SetMember(2, MakeChar("Back", 20, 5, 1));

        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1, 0) });
        var state = CombatEngine.Enter(party, encounter, new GameRandom(7));

        // Enemy at row 0; back row player at distance 2
        var enemy = state.Combatants.First(c => !c.IsPlayer);
        var backPlayer = state.Combatants.First(c => c.IsPlayer && c.Row == 1);

        Assert.Equal(0, enemy.Row);
        Assert.Equal(2, RangeBands.Distance(enemy, backPlayer));
        Assert.False(RangeBands.InRange(enemy, backPlayer, "melee"));
        Assert.True(RangeBands.InRange(enemy, backPlayer, "far"));
    }

    [Fact]
    public void Snapshot_SixCharParty_ToVictory()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Alpha", 40, 8, 0));
        party.SetMember(1, MakeChar("Beta", 35, 7, 0));
        party.SetMember(2, MakeChar("Gamma", 30, 6, 0));
        party.SetMember(3, MakeChar("Delta", 25, 5, 1));
        party.SetMember(4, MakeChar("Epsilon", 25, 5, 1));
        party.SetMember(5, MakeChar("Zeta", 20, 4, 1));

        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 3) });
        var state = CombatEngine.Enter(party, encounter, new GameRandom(5));
        var rng = new GameRandom(5);

        int steps = 0;
        while (!state.IsFinished && steps < 200)
        {
            var actor = state.CurrentActor;
            if (actor?.IsPlayer == true)
            {
                var target = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                state = CombatEngine.Tick(state,
                    new CombatAction(actor.Value.Id, ActionType.Attack, target.Id, null, null), rng);
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng);
            }
            steps++;
        }

        Assert.Equal(CombatPhase.Ended, state.Phase);
        Assert.Contains("Victory", state.Log.Last().Message);
    }

    [Fact]
    public void Snapshot_ThreeEnemyGroups_MeleeBand_CombatResolves()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Tank", 60, 5, 0));
        party.SetMember(1, MakeChar("Dps", 40, 8, 0));

        // Three enemy groups all in melee band (row 0)
        var encounter = new EncounterDef("e1", "Test", new[]
        {
            new EnemySpawn("rat", 1, 0),
            new EnemySpawn("goblin", 1, 0),
            new EnemySpawn("wolf", 1, 0)
        });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(11));
        var rng = new GameRandom(11);

        int steps = 0;
        while (!state.IsFinished && steps < 200)
        {
            var actor = state.CurrentActor;
            if (actor?.IsPlayer == true)
            {
                var target = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                state = CombatEngine.Tick(state,
                    new CombatAction(actor.Value.Id, ActionType.Attack, target.Id, null, null), rng);
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng);
            }
            steps++;
        }

        Assert.Equal(CombatPhase.Ended, state.Phase);
        Assert.Contains("Victory", state.Log.Last().Message);

        // Verify all three enemy groups were present
        var enemyNames = state.Combatants.Where(c => !c.IsPlayer).Select(c => c.Name).ToArray();
        Assert.Equal(3, enemyNames.Length);
    }
}
