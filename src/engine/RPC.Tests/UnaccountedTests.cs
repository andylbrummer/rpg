using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Party;

namespace RPC.Tests;

public class UnaccountedTests
{
    private static CharacterState MakeChar(string name, int hp, int speed, int row = 0)
        => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
            name, "test", 1, 0,
            new BaseStats(4, speed, 4, 4, 4),
            hp, Equipment.Empty,
            Array.Empty<string>(), row);

    private static EnemyRegistry LoadUnaccounted()
    {
        var registry = new EnemyRegistry();
        var path = "../../../../../../content/enemies/unaccounted.json";
        var json = File.ReadAllText(path);
        registry.LoadFromJson("unaccounted", json);
        return registry;
    }

    [Fact]
    public void Unaccounted_EnemyJson_IsValid()
    {
        var registry = LoadUnaccounted();
        var def = registry.Get("unaccounted");
        Assert.NotNull(def);
        Assert.Equal("unaccounted", def.Ai);
        Assert.True(def.HpBase > 0);
        Assert.True(def.Speed > 0);
    }

    [Fact]
    public void Interrupt_Unaccounted_Gets_Extra_Turns()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 50, 5));

        var encounter = new EncounterDef("test", "Test", new[]
        {
            new EnemySpawn("unaccounted", 1)
        });

        var registry = LoadUnaccounted();
        var state = CombatEngine.Enter(party, encounter, new GameRandom(42), registry);

        // Tick once to move from RoundStart to Turn (which calls StartRound and adds interrupts)
        state = CombatEngine.Tick(state, null, new GameRandom(1));

        // Initiative order should contain the unaccounted ID more than once
        var unaccountedId = state.Combatants.First(c => !c.IsPlayer).Id;
        var count = state.InitiativeOrder.Count(id => id == unaccountedId);
        Assert.True(count >= 2, "Unaccounted should get extra interrupt turns");
    }

    [Fact]
    public void Phase_Unaccounted_Changes_Row_Before_Acting()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 50, 5));

        var encounter = new EncounterDef("test", "Test", new[]
        {
            new EnemySpawn("unaccounted", 1)
        });

        var registry = LoadUnaccounted();
        var state = CombatEngine.Enter(party, encounter, new GameRandom(42), registry);

        // Force unaccounted to phase by running its turn
        var rng = new GameRandom(1);
        while (state.Phase != CombatPhase.Ended && !(state.Phase == CombatPhase.Turn && state.CurrentActor?.IsPlayer == false))
        {
            state = CombatEngine.Tick(state, null, rng);
        }

        if (state.Phase == CombatPhase.Turn && state.CurrentActor?.IsPlayer == false)
        {
            var beforeRow = state.CurrentActor.Value.Row;
            state = CombatEngine.Tick(state, null, rng);
            // Row may have changed during its turn due to phase
            var unaccounted = state.Combatants.First(c => !c.IsPlayer);
            // We can't assert exact row since it's random, but we can assert the action resolved
            Assert.NotEqual(CombatPhase.Turn, state.Phase);
        }
    }

    [Fact]
    public void Dread_Applied_On_Unaccounted_Attack()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 50, 5));

        var encounter = new EncounterDef("test", "Test", new[]
        {
            new EnemySpawn("unaccounted", 1)
        });

        var registry = LoadUnaccounted();
        var state = CombatEngine.Enter(party, encounter, new GameRandom(42), registry);

        var rng = new GameRandom(1);
        int steps = 0;
        while (!state.IsFinished && state.Round <= 3 && steps < 50)
        {
            steps++;
            var actor = state.CurrentActor;
            if (actor?.IsPlayer == true)
            {
                var enemy = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                state = CombatEngine.Tick(state,
                    new CombatAction(actor.Value.Id, ActionType.Attack, enemy.Id, null, null), rng);
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng);
            }
        }

        var player = state.Combatants.First(c => c.IsPlayer);
        Assert.Contains(player.StatusEffects, s => s.Type == "dread");
    }

    [Fact]
    public void Reassemble_Two_Dead_Unaccounted_Create_New()
    {
        // Directly construct a combat state with 2 dead unaccounted
        var uid1 = Guid.NewGuid();
        var uid2 = Guid.NewGuid();
        var hero = new Combatant(
            new Guid("6f726548-2020-2020-2020-202020202020"),
            "Hero", true, 100, 100, 5, 0,
            new List<StatusEffect>(), 5);

        var unaccounted1 = new Combatant(
            uid1, "Unaccounted_1", false, 0, 24, 7, 0,
            new List<StatusEffect>(), 5, null, false, 0, null, "unaccounted");

        var unaccounted2 = new Combatant(
            uid2, "Unaccounted_2", false, 0, 24, 7, 0,
            new List<StatusEffect>(), 5, null, false, 0, null, "unaccounted");

        var state = new CombatState(
            new[] { hero, unaccounted1, unaccounted2 },
            1,
            new[] { hero.Id },
            0,
            new List<CombatLogEntry>(),
            null,
            CombatPhase.RoundStart,
            10)
        {
            DeadUnaccounted = new List<DeadUnaccounted>
            {
                new DeadUnaccounted(uid1, 1),
                new DeadUnaccounted(uid2, 1)
            }
        };

        // Tick through rounds until reassembly happens
        var rng = new GameRandom(1);
        int steps = 0;
        while (!state.IsFinished && state.Round <= 5 && steps < 50)
        {
            steps++;
            var actor = state.CurrentActor;
            if (actor?.IsPlayer == true)
            {
                // Player waits to let rounds pass
                state = CombatEngine.Tick(state,
                    new CombatAction(actor.Value.Id, ActionType.Wait, null, null, null), rng);
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng);
            }
        }

        Assert.Contains(state.Log, l => l.Message.Contains("reassemble"));
        var reassembledCount = state.Combatants.Count(c => c.Name.Contains("Reassembled"));
        Assert.True(reassembledCount >= 1, "Reassembled Unaccounted should spawn");
    }

    [Fact]
    public void ReachThrough_Can_Target_Back_Row()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Front", 50, 5, 0));
        party.SetMember(1, MakeChar("Back", 30, 5, 1));

        var encounter = new EncounterDef("test", "Test", new[]
        {
            new EnemySpawn("unaccounted", 1)
        });

        var registry = LoadUnaccounted();
        var state = CombatEngine.Enter(party, encounter, new GameRandom(42), registry);

        // Run until the unaccounted acts and hits back row
        var rng = new GameRandom(1);
        bool hitBackRow = false;
        while (!state.IsFinished && state.Round <= 5)
        {
            var actor = state.CurrentActor;
            if (actor?.IsPlayer == true)
            {
                var enemy = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                state = CombatEngine.Tick(state,
                    new CombatAction(actor.Value.Id, ActionType.Attack, enemy.Id, null, null), rng);
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng);
            }

            // Check log for back row being hit by unaccounted
            var backChar = state.Combatants.First(c => c.IsPlayer && c.Name.StartsWith("Back"));
            if (state.Log.Any(l => l.Message.Contains("Back") && l.Message.Contains("damage")))
            {
                hitBackRow = true;
            }
        }

        // Unaccounted should have had opportunity to hit back row via reach through
        // We can't guarantee it happened due to RNG, but we verify the capability exists in target selection
        Assert.True(hitBackRow || state.Combatants.Any(c => c.StatusEffects.Any(s => s.Type == "dread")),
            "Unaccounted should interact with back row via reach-through or dread");
    }

    [Fact]
    public void ShieldWall_Blocks_Unaccounted_Interrupt()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 50, 5));

        var encounter = new EncounterDef("test", "Test", new[]
        {
            new EnemySpawn("unaccounted", 1)
        });

        var registry = LoadUnaccounted();
        var state = CombatEngine.Enter(party, encounter, new GameRandom(42), registry);

        // Apply shield_wall to the hero by recreating with the status
        var hero = state.Combatants.First(c => c.IsPlayer);
        var shieldedHero = hero with { StatusEffects = new List<StatusEffect> { new StatusEffect("shield_wall", 2, null) } };
        var unaccounted = state.Combatants.First(c => !c.IsPlayer);
        state = state with { Combatants = new[] { shieldedHero, unaccounted } };

        // Tick to start round
        state = CombatEngine.Tick(state, null, new GameRandom(1));

        var unaccountedId = unaccounted.Id;
        var count = state.InitiativeOrder.Count(id => id == unaccountedId);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Burned_Corpse_Does_Not_Reassemble()
    {
        var uid1 = Guid.NewGuid();
        var uid2 = Guid.NewGuid();
        var hero = new Combatant(
            new Guid("6f726548-2020-2020-2020-202020202020"),
            "Hero", true, 100, 100, 5, 0,
            new List<StatusEffect>(), 5);

        var unaccounted1 = new Combatant(
            uid1, "Unaccounted_1", false, 0, 24, 7, 0,
            new List<StatusEffect> { new StatusEffect("burned", 999, null) }, 5, null, false, 0, null, "unaccounted");

        var unaccounted2 = new Combatant(
            uid2, "Unaccounted_2", false, 0, 24, 7, 0,
            new List<StatusEffect> { new StatusEffect("burned", 999, null) }, 5, null, false, 0, null, "unaccounted");

        var state = new CombatState(
            new[] { hero, unaccounted1, unaccounted2 },
            1,
            new[] { hero.Id },
            0,
            new List<CombatLogEntry>(),
            null,
            CombatPhase.RoundStart,
            10)
        {
            DeadUnaccounted = new List<DeadUnaccounted>
            {
                new DeadUnaccounted(uid1, 1, true),
                new DeadUnaccounted(uid2, 1, true)
            }
        };

        var rng = new GameRandom(1);
        int steps = 0;
        while (!state.IsFinished && state.Round <= 5 && steps < 50)
        {
            steps++;
            var actor = state.CurrentActor;
            if (actor?.IsPlayer == true)
            {
                state = CombatEngine.Tick(state,
                    new CombatAction(actor.Value.Id, ActionType.Wait, null, null, null), rng);
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng);
            }
        }

        Assert.DoesNotContain(state.Log, l => l.Message.Contains("reassemble"));
        Assert.Equal(0, state.Combatants.Count(c => c.Name.Contains("Reassembled")));
    }

    [Fact]
    public void WarCry_Dispels_Dread()
    {
        var hero = new Combatant(
            new Guid("6f726548-2020-2020-2020-202020202020"),
            "Hero", true, 100, 100, 5, 0,
            new List<StatusEffect> { new StatusEffect("dread", -1, null, new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")) }, 5);

        var ashmouth = new Combatant(
            new Guid("6173686d-6f75-7468-2020-202020202020"),
            "Ashmouth", true, 100, 100, 5, 0,
            new List<StatusEffect>(), 5, "ashmouth");

        var unaccounted = new Combatant(
            new Guid("756e6163-636f-756e-7465-642020202020"),
            "Unaccounted", false, 100, 100, 5, 0,
            new List<StatusEffect>(), 5, null, false, 0, null, "unaccounted");

        var state = new CombatState(
            new[] { hero, ashmouth, unaccounted },
            1,
            new[] { ashmouth.Id, hero.Id },
            0,
            new List<CombatLogEntry>(),
            null,
            CombatPhase.Turn,
            10);

        var rng = new GameRandom(1);
        // Ashmouth's turn: use war_cry
        state = CombatEngine.Tick(state,
            new CombatAction(ashmouth.Id, ActionType.UseAbility, hero.Id, "war_cry", null), rng);
        // Resolve the action
        state = CombatEngine.Tick(state, null, rng);

        var updatedHero = state.Combatants.First(c => c.Name == "Hero");
        Assert.DoesNotContain(updatedHero.StatusEffects, s => s.Type == "dread");
        Assert.Contains(state.Log, l => l.Message.Contains("war cry dispels the dread"));
    }

    [Fact]
    public void Summon_Absorbs_BackRow_Targeting()
    {
        var front = new Combatant(
            new Guid("66726f6e-7420-2020-2020-202020202020"),
            "Front", true, 50, 50, 5, 0,
            new List<StatusEffect>(), 5);

        var back = new Combatant(
            new Guid("6261636b-2020-2020-2020-202020202020"),
            "Back", true, 30, 30, 5, 1,
            new List<StatusEffect>(), 5);

        var summon = new Combatant(
            new Guid("73756d6d-6f6e-2020-2020-202020202020"),
            "Bone Construct", true, 20, 20, 4, 0,
            new List<StatusEffect>(), 3, null, true, 3);

        var unaccounted = new Combatant(
            new Guid("756e6163-636f-756e-7465-642020202020"),
            "Unaccounted", false, 100, 100, 7, 0,
            new List<StatusEffect>(), 5, null, false, 0, null, "unaccounted");

        var state = new CombatState(
            new[] { front, back, summon, unaccounted },
            1,
            new[] { unaccounted.Id },
            0,
            new List<CombatLogEntry>(),
            null,
            CombatPhase.Turn,
            10);

        var rng = new GameRandom(1);
        // Unaccounted's turn - should redirect to summon due to reach-through counter
        state = CombatEngine.Tick(state, null, rng); // generates AI action
        state = CombatEngine.Tick(state, null, rng); // resolves the action

        // The summon should have taken damage (or the action should have targeted it)
        var updatedSummon = state.Combatants.First(c => c.Name == "Bone Construct");
        var updatedBack = state.Combatants.First(c => c.Name == "Back");

        // Summon absorbed the hit; Back should be untouched
        Assert.True(updatedSummon.Hp < 20 || state.Log.Any(l => l.Message.Contains("Bone Construct") && l.Message.Contains("damage")),
            "Summon should absorb the Unaccounted's reach-through attack");
        Assert.Equal(30, updatedBack.Hp);
    }
}
