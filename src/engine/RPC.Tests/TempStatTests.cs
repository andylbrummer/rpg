using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Party;

namespace RPC.Tests;

public class TempStatTests
{
    private static CharacterState MakeChar(string name, int hp, BaseStats stats, int row = 0, string classId = "test")
        => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
            name, classId, 1, 0,
            stats, hp, Equipment.Empty,
            Array.Empty<string>(), row);

    [Fact]
    public void TempModifier_Stacking_Additive()
    {
        var mod1 = new TempStatModifier("strength", -2, 3, "ability1");
        var mod2 = new TempStatModifier("strength", -1, 2, "ability2");

        var character = MakeChar("Hero", 20, new BaseStats(5, 5, 5, 5, 5))
            with { TempModifiers = new[] { mod1, mod2 } };

        var effective = character.GetEffectiveStats();
        Assert.Equal(2, effective.Power); // 5 - 2 - 1 = 2
    }

    [Fact]
    public void TempModifier_BaseStat_AffectsEffective()
    {
        var mod = new TempStatModifier("constitution", -2, 2, "test");
        var character = MakeChar("Hero", 20, new BaseStats(5, 5, 5, 5, 5))
            with { TempModifiers = new[] { mod } };

        var effective = character.GetEffectiveStats();
        Assert.Equal(11, effective.MaxHp); // (5-2)*3 + 1*2 = 11
    }

    [Fact]
    public void TempModifier_EffectiveStat_Direct()
    {
        var mod = new TempStatModifier("power", -3, 2, "test");
        var character = MakeChar("Hero", 20, new BaseStats(5, 5, 5, 5, 5))
            with { TempModifiers = new[] { mod } };

        var effective = character.GetEffectiveStats();
        Assert.Equal(2, effective.Power); // 5 - 3 = 2
    }

    [Fact]
    public void TempModifier_MaxHp_Capped()
    {
        var mod = new TempStatModifier("maxHp", -20, 2, "test");
        var character = MakeChar("Hero", 20, new BaseStats(5, 5, 5, 5, 5))
            with { TempModifiers = new[] { mod } };

        var effective = character.GetEffectiveStats();
        Assert.Equal(1, effective.MaxHp); // clamped to 1
    }

    [Fact]
    public void CombatEngine_MemoryCost_AppliesModifier()
    {
        var registry = new ClassRegistry();
        var json = """
            {
              "id": "test_mage",
              "name": "Test Mage",
              "description": "Test",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 5, "intelligence": 5, "willpower": 5 },
              "abilities": [
                { "id": "mind_blast", "name": "Mind Blast", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "short" }, "tags": ["magic"], "memoryCost": { "stat": "intelligence", "amount": 2, "duration": 2 } }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["mind_blast"] }
              ]
            }
            """;
        registry.LoadFromJson("test_mage", json);

        var party = new PartyState();
        var character = new CharacterState(
            new Guid("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"), "Mage", "test_mage", 1, 0,
            new BaseStats(5, 5, 5, 5, 5), 20, Equipment.Empty,
            new[] { "mind_blast" }, 0);
        party.SetMember(0, character);

        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });
        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));

        var player = state.Combatants.First(c => c.IsPlayer);
        var enemy = state.Combatants.First(c => !c.IsPlayer);

        // Advance to player turn
        while (state.CurrentActor?.IsPlayer != true && !state.IsFinished)
            state = CombatEngine.Tick(state, null, new GameRandom(1), registry);

        state = CombatEngine.Tick(state,
            new CombatAction(player.Id, ActionType.UseAbility, enemy.Id, "mind_blast", null),
            new GameRandom(1), registry);
        state = CombatEngine.Tick(state, null, new GameRandom(1), registry);
        state = CombatEngine.Tick(state, null, new GameRandom(1), registry);

        var updatedPlayer = state.Combatants.First(c => c.Id == player.Id);
        Assert.Single(updatedPlayer.TempModifiers);
        Assert.Equal("intelligence", updatedPlayer.TempModifiers[0].Stat);
        Assert.Equal(-2, updatedPlayer.TempModifiers[0].Delta);
        Assert.Equal(2, updatedPlayer.TempModifiers[0].Duration);
    }

    [Fact]
    public void CombatEngine_ModifierExpiry_AfterDuration()
    {
        var registry = new ClassRegistry();
        var json = """
            {
              "id": "test_mage",
              "name": "Test Mage",
              "description": "Test",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 5, "intelligence": 5, "willpower": 5 },
              "abilities": [
                { "id": "mind_blast", "name": "Mind Blast", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "short" }, "tags": ["magic"], "memoryCost": { "stat": "power", "amount": 2, "duration": 1 } }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["mind_blast"] }
              ]
            }
            """;
        registry.LoadFromJson("test_mage", json);

        var party = new PartyState();
        var character = new CharacterState(
            new Guid("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"), "Mage", "test_mage", 1, 0,
            new BaseStats(5, 5, 5, 5, 5), 20, Equipment.Empty,
            new[] { "mind_blast" }, 0);
        party.SetMember(0, character);

        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });
        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));

        var player = state.Combatants.First(c => c.IsPlayer);
        var enemy = state.Combatants.First(c => !c.IsPlayer);
        var originalPower = player.Power;

        // Advance to player turn
        while (state.CurrentActor?.IsPlayer != true && !state.IsFinished)
            state = CombatEngine.Tick(state, null, new GameRandom(1), registry);

        state = CombatEngine.Tick(state,
            new CombatAction(player.Id, ActionType.UseAbility, enemy.Id, "mind_blast", null),
            new GameRandom(1), registry);
        state = CombatEngine.Tick(state, null, new GameRandom(1), registry);
        state = CombatEngine.Tick(state, null, new GameRandom(1), registry);

        var afterUse = state.Combatants.First(c => c.Id == player.Id);
        Assert.Equal(originalPower - 2, afterUse.Power);

        // Pass the round by having all combatants take turns
        int steps = 0;
        while (state.Round < 2 && steps < 20)
        {
            var actor = state.CurrentActor;
            state = CombatEngine.Tick(state,
                actor != null ? new CombatAction(actor.Value.Id, ActionType.Wait, null, null, null) : null,
                new GameRandom(1), registry);
            steps++;
        }

        var afterRound = state.Combatants.First(c => c.Id == player.Id);
        Assert.Empty(afterRound.TempModifiers);
        Assert.Equal(originalPower, afterRound.Power);
    }

    [Fact]
    public void CombatEngine_EffectiveStats_UsedForDamage()
    {
        var registry = new ClassRegistry();
        var json = """
            {
              "id": "test_striker",
              "name": "Test Striker",
              "description": "Test",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 5, "intelligence": 5, "willpower": 5 },
              "abilities": [
                { "id": "power_strike", "name": "Power Strike", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4+PWR", "range": "melee" }, "tags": ["physical"], "memoryCost": { "stat": "strength", "amount": 2, "duration": 2 } }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["power_strike"] }
              ]
            }
            """;
        registry.LoadFromJson("test_striker", json);

        var party = new PartyState();
        var character = new CharacterState(
            new Guid("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"), "Striker", "test_striker", 1, 0,
            new BaseStats(5, 5, 5, 5, 5), 20, Equipment.Empty,
            new[] { "power_strike" }, 0);
        party.SetMember(0, character);

        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });
        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));

        var player = state.Combatants.First(c => c.IsPlayer);
        var enemy = state.Combatants.First(c => !c.IsPlayer);

        // Advance to player turn
        while (state.CurrentActor?.IsPlayer != true && !state.IsFinished)
            state = CombatEngine.Tick(state, null, new GameRandom(1), registry);

        state = CombatEngine.Tick(state,
            new CombatAction(player.Id, ActionType.UseAbility, enemy.Id, "power_strike", null),
            new GameRandom(1), registry);
        state = CombatEngine.Tick(state, null, new GameRandom(1), registry);
        state = CombatEngine.Tick(state, null, new GameRandom(1), registry);

        var updatedPlayer = state.Combatants.First(c => c.Id == player.Id);
        Assert.Equal(3, updatedPlayer.Power); // 5 - 2 = 3
    }

    [Fact]
    public void GameState_SyncsModifiers_AfterCombat()
    {
        var registry = new ClassRegistry();
        var json = """
            {
              "id": "test_mage",
              "name": "Test Mage",
              "description": "Test",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 5, "intelligence": 5, "willpower": 5 },
              "abilities": [
                { "id": "mind_blast", "name": "Mind Blast", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "short" }, "tags": ["magic"], "memoryCost": { "stat": "intelligence", "amount": 2, "duration": 3 } }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["mind_blast"] }
              ]
            }
            """;
        registry.LoadFromJson("test_mage", json);

        var gs = new GameState(seed: 1, classRegistry: registry);
        var character = new CharacterState(
            new Guid("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"), "Mage", "test_mage", 1, 0,
            new BaseStats(5, 5, 5, 5, 5), 20, Equipment.Empty,
            new[] { "mind_blast" }, 0);
        gs.Party.SetMember(0, character);
        for (int i = 1; i < 6; i++)
            gs.Party.SetMember(i, default);

        // Use a 0-enemy encounter so combat ends immediately and syncs back
        gs.TriggerEncounter(new EncounterDef("test", "Test", Array.Empty<EnemySpawn>()));

        // Combat ends immediately, no modifiers applied
        var member = gs.Party.Members.First(m => m.Id == character.Id);
        Assert.Empty(member.TempModifiers);

        // Now trigger a real encounter and use ability
        gs.TriggerEncounter(new EncounterDef("test2", "Test2", new[] { new EnemySpawn("rat", 1, 0) }));
        var combat = gs.Combat!;
        var player = combat.Combatants.First(c => c.IsPlayer);
        var enemy = combat.Combatants.First(c => !c.IsPlayer);

        gs.SubmitCombatAction(
            new CombatAction(player.Id, ActionType.UseAbility, enemy.Id, "mind_blast", null));

        // Check combat state directly since combat may not be finished
        member = gs.Party.Members.First(m => m.Id == player.Id);
        if (gs.Combat != null)
        {
            var combatant = gs.Combat.Combatants.First(c => c.Id == player.Id);
            Assert.Single(combatant.TempModifiers);
            Assert.Equal("intelligence", combatant.TempModifiers[0].Stat);
            Assert.Equal(-2, combatant.TempModifiers[0].Delta);
        }
        else
        {
            // Combat finished, check synced member
            Assert.Single(member.TempModifiers);
            Assert.Equal("intelligence", member.TempModifiers[0].Stat);
            Assert.Equal(-2, member.TempModifiers[0].Delta);
        }
    }

    [Fact]
    public void GameState_RestAtInn_ClearsModifiers()
    {
        var gs = new GameState(seed: 1);
        var mod = new TempStatModifier("strength", -2, 3, "test");
        var member = gs.Party.Members.First(m => m.Id != Guid.Empty);
        var index = Array.IndexOf(gs.Party.Members, member);
        gs.Party.SetMember(index, member with { TempModifiers = new[] { mod } });

        gs.RestAtInn();

        var afterRest = gs.Party.Members[index];
        Assert.Empty(afterRest.TempModifiers);
    }

    [Fact]
    public void CombatEngine_NoMemoryCost_NoModifier()
    {
        var registry = new ClassRegistry();
        var json = """
            {
              "id": "test_warrior",
              "name": "Test Warrior",
              "description": "Test",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 5, "intelligence": 5, "willpower": 5 },
              "abilities": [
                { "id": "slash", "name": "Slash", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["physical"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["slash"] }
              ]
            }
            """;
        registry.LoadFromJson("test_warrior", json);

        var party = new PartyState();
        var character = new CharacterState(
            new Guid("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"), "Warrior", "test_warrior", 1, 0,
            new BaseStats(5, 5, 5, 5, 5), 20, Equipment.Empty,
            new[] { "slash" }, 0);
        party.SetMember(0, character);

        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });
        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));

        var player = state.Combatants.First(c => c.IsPlayer);
        var enemy = state.Combatants.First(c => !c.IsPlayer);

        state = CombatEngine.Tick(state,
            new CombatAction(player.Id, ActionType.UseAbility, enemy.Id, "slash", null),
            new GameRandom(1), registry);

        var updated = state.Combatants.First(c => c.Id == player.Id);
        Assert.Empty(updated.TempModifiers);
    }
}
