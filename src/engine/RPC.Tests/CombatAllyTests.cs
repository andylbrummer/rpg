using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Party;

namespace RPC.Tests;

public class CombatAllyTests
{
    private static CharacterState MakeChar(string name, int hp, int speed, int row = 0)
        => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
            name, "test", 1, 0,
            new BaseStats(4, speed, 4, 4, 4),
            hp, Equipment.Empty,
            Array.Empty<string>(), row);

    [Fact]
    public void SummonAlly_EmptyFrontSlot_OccupiesFrontRow()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 20, 5));
        var encounter = new EncounterDef("e1", "Test", Array.Empty<EnemySpawn>());

        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));
        state = CombatEngine.SummonAlly(state, party, new SummonDef("wolf", "Wolf", 10, 5, 3, 3), new GameRandom(1));

        var summon = state.Combatants.First(c => c.IsSummoned);
        Assert.Equal(0, summon.Row);
        Assert.Contains(state.SummonSlotAssignments, kv => kv.Value == summon.Id);
        Assert.Contains(state.Log, l => l.Message.Contains("Wolf summoned"));
    }

    [Fact]
    public void SummonAlly_EmptyBackSlot_OccupiesBackRow()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("A", 20, 5));
        party.SetMember(1, MakeChar("B", 20, 5));
        party.SetMember(2, MakeChar("C", 20, 5));
        var encounter = new EncounterDef("e1", "Test", Array.Empty<EnemySpawn>());

        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));
        state = CombatEngine.SummonAlly(state, party, new SummonDef("wolf", "Wolf", 10, 5, 3, 3), new GameRandom(1));

        var summon = state.Combatants.First(c => c.IsSummoned);
        Assert.Equal(1, summon.Row);
    }

    [Fact]
    public void SummonAlly_FullParty_UsesDefaultRow()
    {
        var party = new PartyState();
        for (int i = 0; i < 6; i++)
            party.SetMember(i, MakeChar($"M{i}", 20, 5, i < 3 ? 0 : 1));
        var encounter = new EncounterDef("e1", "Test", Array.Empty<EnemySpawn>());

        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));
        state = CombatEngine.SummonAlly(state, party, new SummonDef("wolf", "Wolf", 10, 5, 3, 3, 1), new GameRandom(1));

        var summon = state.Combatants.First(c => c.IsSummoned);
        Assert.Equal(1, summon.Row);
        Assert.Empty(state.SummonSlotAssignments);
    }

    [Fact]
    public void SummonAlly_ExpiresAfterDuration()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 20, 5));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));
        state = CombatEngine.SummonAlly(state, party, new SummonDef("wolf", "Wolf", 10, 5, 3, 1), new GameRandom(1));

        var rng = new GameRandom(1);
        int steps = 0;
        while (!state.IsFinished && steps < 50)
        {
            var actor = state.CurrentActor;
            state = CombatEngine.Tick(state,
                actor != null ? new CombatAction(actor.Value.Id, ActionType.Wait, null, null, null) : null,
                rng);
            steps++;
        }

        Assert.Contains(state.Log, l => l.Message.Contains("Wolf expired"));
        var summon = state.Combatants.FirstOrDefault(c => c.IsSummoned);
        Assert.True(summon.Id == Guid.Empty || !summon.IsAlive);
    }

    [Fact]
    public void SummonAlly_DiesOnDamage()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 100, 1));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));
        state = CombatEngine.SummonAlly(state, party, new SummonDef("wolf", "Wolf", 1, 5, 3, 5), new GameRandom(1));
        var summon = state.Combatants.First(c => c.IsSummoned);

        var rng = new GameRandom(1);
        int steps = 0;
        while (!state.IsFinished && steps < 200)
        {
            var actor = state.CurrentActor;
            state = CombatEngine.Tick(state,
                actor != null ? new CombatAction(actor.Value.Id, ActionType.Wait, null, null, null) : null,
                rng);
            steps++;
        }

        var updatedSummon = state.Combatants.FirstOrDefault(c => c.Id == summon.Id);
        Assert.True(updatedSummon.Id == Guid.Empty || !updatedSummon.IsAlive);
        Assert.Contains(state.Log, l => l.Message.Contains("Wolf died"));
    }

    [Fact]
    public void SummonAlly_DoesNotPreventDefeat()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 1, 5));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));
        state = CombatEngine.SummonAlly(state, party, new SummonDef("wolf", "Wolf", 50, 5, 3, 5), new GameRandom(1));

        var rng = new GameRandom(1);
        int steps = 0;
        while (!state.IsFinished && steps < 50)
        {
            var actor = state.CurrentActor;
            if (actor != null && actor.Value.IsPlayer)
            {
                var enemy = state.Combatants.First(c => !c.IsPlayer);
                state = CombatEngine.Tick(state,
                    new CombatAction(actor.Value.Id, ActionType.Attack, enemy.Id, null, null),
                    rng);
            }
            else
            {
                state = CombatEngine.Tick(state,
                    actor != null ? new CombatAction(actor.Value.Id, ActionType.Wait, null, null, null) : null,
                    rng);
            }
            steps++;
        }

        Assert.True(state.AllPlayersDead || state.AllEnemiesDead);
        var summon = state.Combatants.FirstOrDefault(c => c.IsSummoned);
        if (state.AllPlayersDead)
            Assert.True(summon.IsAlive);
    }

    [Fact]
    public void SummonAlly_TwoSummons_OccupyDifferentSlots()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 20, 5));
        var encounter = new EncounterDef("e1", "Test", Array.Empty<EnemySpawn>());

        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));
        state = CombatEngine.SummonAlly(state, party, new SummonDef("a", "WolfA", 10, 5, 3, 3), new GameRandom(1));
        state = CombatEngine.SummonAlly(state, party, new SummonDef("b", "WolfB", 10, 5, 3, 3), new GameRandom(1));

        Assert.Equal(2, state.SummonSlotAssignments.Count);
        Assert.Equal(2, state.SummonSlotAssignments.Values.Distinct().Count());
    }
}
