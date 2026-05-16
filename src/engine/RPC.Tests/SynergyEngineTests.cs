using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Party;

namespace RPC.Tests;

[Collection("SynergyTests")]
public class SynergyEngineTests
{
    private static CharacterState MakeChar(string name, int hp, int speed, int row = 0, string classId = "test")
        => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
            name, classId, 1, 0,
            new BaseStats(4, speed, 4, 4, 4),
            hp, Equipment.Empty,
            Array.Empty<string>(), row);

    private readonly SynergyRegistry _synergies = new();

    public SynergyEngineTests()
    {
        _synergies.Clear();
    }

    [Fact]
    public void PairLookup_OrderIndependent()
    {
        _synergies.Register("ability_a", "ability_b", new SynergyEffect("bonus_damage", 5));

        var effect1 = _synergies.Lookup("ability_a", "ability_b");
        var effect2 = _synergies.Lookup("ability_b", "ability_a");

        Assert.NotNull(effect1);
        Assert.NotNull(effect2);
        Assert.Equal(effect1, effect2);
    }

    [Fact]
    public void PairLookup_SameAbility_NoTrigger()
    {
        _synergies.Register("ability_a", "ability_b", new SynergyEffect("bonus_damage", 5));

        var effect = _synergies.Lookup("ability_a", "ability_a");

        Assert.Null(effect);
    }

    [Fact]
    public void PairLookup_UnregisteredPair_ReturnsNull()
    {
        _synergies.Register("ability_a", "ability_b", new SynergyEffect("bonus_damage", 5));

        var effect = _synergies.Lookup("ability_a", "ability_c");

        Assert.Null(effect);
    }

    [Fact]
    public void RoundReset_ClearsAbilitiesUsed()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 20, 10));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(1));
        var rng = new GameRandom(1);

        // Tick to player turn
        while (!state.IsFinished && state.CurrentActor?.IsPlayer != true)
            state = CombatEngine.Tick(state, null, rng);

        // Use an ability and resolve fully
        var enemy = state.Combatants.First(c => !c.IsPlayer);
        state = CombatEngine.Tick(state,
            new CombatAction(state.CurrentActor!.Value.Id, ActionType.UseAbility, enemy.Id, "ability_a", null), rng, null);
        while (!state.IsFinished && state.Phase != CombatPhase.Turn)
            state = CombatEngine.Tick(state, null, rng);

        // Advance through remaining turns to next RoundStart
        while (!state.IsFinished && state.Phase != CombatPhase.RoundStart)
            state = CombatEngine.Tick(state, null, rng);

        // After round start, abilities used should be cleared
        Assert.Empty(state.AbilitiesUsedThisRound);
    }

    [Fact]
    public void AbilityResolution_AThenB_TriggersOnB()
    {
        _synergies.Register("ability_a", "ability_b", new SynergyEffect("bonus_damage", 5));

        var registry = new ClassRegistry();
        var json = """
            {
              "id": "synergy_test",
              "name": "Synergy Test",
              "description": "Test",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 5, "intelligence": 3, "willpower": 3 },
              "abilities": [
                { "id": "ability_a", "name": "A", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] },
                { "id": "ability_b", "name": "B", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["ability_a", "ability_b"] }
              ]
            }
            """;
        registry.LoadFromJson("synergy_test", json);

        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 20, 10, 0, "synergy_test"));
        party.SetMember(1, MakeChar("Ally", 20, 1, 0, "synergy_test"));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(42));
        var rng = new GameRandom(42);

        // Find two player turns in order
        int playerTurns = 0;
        string[] abilitySequence = ["ability_a", "ability_b"];
        while (!state.IsFinished && playerTurns < 2)
        {
            if (state.Phase == CombatPhase.Turn && state.CurrentActor?.IsPlayer == true)
            {
                var enemy = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                state = CombatEngine.Tick(state,
                    new CombatAction(state.CurrentActor!.Value.Id, ActionType.UseAbility, enemy.Id, abilitySequence[playerTurns], null),
                    rng, registry, synergies: _synergies);
                while (!state.IsFinished && state.Phase != CombatPhase.Turn)
                    state = CombatEngine.Tick(state, null, rng, registry, synergies: _synergies);
                playerTurns++;
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng, registry, synergies: _synergies);
            }
        }

        // Synergy should have triggered on the second ability
        var synergyLogs = state.Log.Where(l => l.Message.Contains("synergy", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.NotEmpty(synergyLogs);
    }

    [Fact]
    public void AbilityResolution_BThenA_TriggersOnA()
    {
        _synergies.Register("ability_a", "ability_b", new SynergyEffect("bonus_damage", 5));

        var registry = new ClassRegistry();
        var json = """
            {
              "id": "synergy_test",
              "name": "Synergy Test",
              "description": "Test",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 5, "intelligence": 3, "willpower": 3 },
              "abilities": [
                { "id": "ability_a", "name": "A", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] },
                { "id": "ability_b", "name": "B", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["ability_a", "ability_b"] }
              ]
            }
            """;
        registry.LoadFromJson("synergy_test", json);

        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 20, 10, 0, "synergy_test"));
        party.SetMember(1, MakeChar("Ally", 20, 1, 0, "synergy_test"));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(42));
        var rng = new GameRandom(42);

        int playerTurns = 0;
        string[] abilitySequence = ["ability_b", "ability_a"];
        while (!state.IsFinished && playerTurns < 2)
        {
            if (state.Phase == CombatPhase.Turn && state.CurrentActor?.IsPlayer == true)
            {
                var enemy = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                state = CombatEngine.Tick(state,
                    new CombatAction(state.CurrentActor!.Value.Id, ActionType.UseAbility, enemy.Id, abilitySequence[playerTurns], null),
                    rng, registry, synergies: _synergies);
                while (!state.IsFinished && state.Phase != CombatPhase.Turn)
                    state = CombatEngine.Tick(state, null, rng, registry, synergies: _synergies);
                playerTurns++;
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng, registry, synergies: _synergies);
            }
        }

        var synergyLogs = state.Log.Where(l => l.Message.Contains("synergy", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.NotEmpty(synergyLogs);
    }

    [Fact]
    public void AbilityResolution_SameAbilityTwice_NoTrigger()
    {
        _synergies.Register("ability_a", "ability_b", new SynergyEffect("bonus_damage", 5));

        var registry = new ClassRegistry();
        var json = """
            {
              "id": "synergy_test",
              "name": "Synergy Test",
              "description": "Test",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 5, "intelligence": 3, "willpower": 3 },
              "abilities": [
                { "id": "ability_a", "name": "A", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["ability_a"] }
              ]
            }
            """;
        registry.LoadFromJson("synergy_test", json);

        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 20, 10, 0, "synergy_test"));
        party.SetMember(1, MakeChar("Ally", 20, 1, 0, "synergy_test"));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(42));
        var rng = new GameRandom(42);

        int playerTurns = 0;
        while (!state.IsFinished && playerTurns < 2)
        {
            if (state.Phase == CombatPhase.Turn && state.CurrentActor?.IsPlayer == true)
            {
                var enemy = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                state = CombatEngine.Tick(state,
                    new CombatAction(state.CurrentActor!.Value.Id, ActionType.UseAbility, enemy.Id, "ability_a", null),
                    rng, registry, synergies: _synergies);
                while (!state.IsFinished && state.Phase != CombatPhase.Turn)
                    state = CombatEngine.Tick(state, null, rng, registry, synergies: _synergies);
                playerTurns++;
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng, registry, synergies: _synergies);
            }
        }

        var synergyLogs = state.Log.Where(l => l.Message.Contains("synergy", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Empty(synergyLogs);
    }

    [Fact]
    public void AntiSynergy_BonewardenStillblade_NoFire()
    {
        // Anti-synergy: Bonewarden + Stillblade — no positive cross-class effects registered.
        // Bonewarden abilities: bone_spear, tithe_touch, raise_minion, death_pact
        // Stillblade abilities: rend, silence_strike, warding_stance, break_magic

        var registry = new ClassRegistry();
        var bwJson = """
            {
              "id": "bonewarden",
              "name": "Bonewarden",
              "description": "Test",
              "baseStats": { "strength": 4, "dexterity": 3, "constitution": 5, "intelligence": 4, "willpower": 4 },
              "abilities": [
                { "id": "bone_spear", "name": "Bone Spear", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["bone_spear"] }
              ]
            }
            """;
        var sbJson = """
            {
              "id": "stillblade",
              "name": "Stillblade",
              "description": "Test",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 4, "intelligence": 3, "willpower": 4 },
              "abilities": [
                { "id": "rend", "name": "Rend", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["rend"] }
              ]
            }
            """;
        registry.LoadFromJson("bonewarden", bwJson);
        registry.LoadFromJson("stillblade", sbJson);

        var party = new PartyState();
        party.SetMember(0, MakeChar("Bone", 20, 10, 0, "bonewarden"));
        party.SetMember(1, MakeChar("Still", 20, 1, 0, "stillblade"));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(42));
        var rng = new GameRandom(42);

        int playerTurns = 0;
        string[] abilitySequence = ["bone_spear", "rend"];
        while (!state.IsFinished && playerTurns < 2)
        {
            if (state.Phase == CombatPhase.Turn && state.CurrentActor?.IsPlayer == true)
            {
                var enemy = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                var ability = abilitySequence[playerTurns];
                state = CombatEngine.Tick(state,
                    new CombatAction(state.CurrentActor!.Value.Id, ActionType.UseAbility, enemy.Id, ability, null),
                    rng, registry, synergies: _synergies);
                while (!state.IsFinished && state.Phase != CombatPhase.Turn)
                    state = CombatEngine.Tick(state, null, rng, registry, synergies: _synergies);
                playerTurns++;
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng, registry, synergies: _synergies);
            }
        }

        var synergyLogs = state.Log.Where(l => l.Message.Contains("synergy", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Empty(synergyLogs);
    }
}
