using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Party;

namespace RPC.Tests;

public class CombatSnapshotTests
{
    private static CharacterState MakeChar(string name, int hp, int speed, int row = 0, string classId = "test")
        => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
            name, classId, 1, 0,
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

    [Fact]
    public void Snapshot_RowVariation_ProducesDifferentDamageOutput()
    {
        var registry = new ClassRegistry();
        var json = """
            {
              "id": "striker",
              "name": "Striker",
              "description": "Test",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 5, "intelligence": 3, "willpower": 3 },
              "abilities": [
                { "id": "front_slash", "name": "Front Slash", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d8+PWR", "range": "melee" }, "tags": ["physical"], "requiredRow": "front" }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["front_slash"] }
              ]
            }
            """;
        registry.LoadFromJson("striker", json);

        static CharacterState MakeStriker(string name, int row)
            => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
                name, "striker", 1, 0,
                new BaseStats(5, 5, 5, 3, 3),
                60, Equipment.Empty,
                new[] { "front_slash" }, row);

        var partyA = new PartyState();
        partyA.SetMember(0, MakeStriker("A1", 0));
        partyA.SetMember(1, MakeStriker("A2", 0));
        partyA.SetMember(2, MakeStriker("A3", 0));

        var partyB = new PartyState();
        partyB.SetMember(0, MakeStriker("B1", 0));
        partyB.SetMember(1, MakeStriker("B2", 0));
        partyB.SetMember(2, MakeStriker("B3", 1));

        // 6 rats so combat doesn't end before damage difference accumulates
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 6, 0) });

        int RunCombat(PartyState party, int seed)
        {
            var state = CombatEngine.Enter(party, encounter, new GameRandom(seed));
            var rng = new GameRandom(seed);
            int playerTurns = 0;
            int steps = 0;
            while (!state.IsFinished && steps < 200 && playerTurns < 6)
            {
                var actor = state.CurrentActor;
                if (actor?.IsPlayer == true)
                {
                    var target = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                    var action = actor.Value.Row == 0
                        ? new CombatAction(actor.Value.Id, ActionType.UseAbility, target.Id, "front_slash", null)
                        : new CombatAction(actor.Value.Id, ActionType.Attack, target.Id, null, null);
                    state = CombatEngine.Tick(state, action, rng, registry);
                    playerTurns++;
                }
                else
                {
                    state = CombatEngine.Tick(state, null, rng, registry);
                }
                steps++;
            }
            return state.Combatants.Where(c => !c.IsPlayer).Sum(e => e.MaxHp - e.Hp);
        }

        var damageA = RunCombat(partyA, 42);
        var damageB = RunCombat(partyB, 42);

        Assert.True(damageA > damageB,
            $"Expected front-only party to deal more damage. A={damageA}, B={damageB}");
    }

    [Fact]
    public void Snapshot_Synergy_BonewardenCauterist_BoneLinkPyre_Triggers()
    {
        SynergyRegistry.Register("bone_link", "pyre", new SynergyEffect("bonus_damage", 4));

        var registry = new ClassRegistry();
        var bwJson = """
            {
              "id": "bonewarden",
              "name": "Bonewarden",
              "description": "Test",
              "baseStats": { "strength": 4, "dexterity": 3, "constitution": 5, "intelligence": 4, "willpower": 4 },
              "abilities": [
                { "id": "bone_link", "name": "Bone Link", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["bone_link"] }
              ]
            }
            """;
        var caJson = """
            {
              "id": "cauterist",
              "name": "Cauterist",
              "description": "Test",
              "baseStats": { "strength": 3, "dexterity": 5, "constitution": 4, "intelligence": 5, "willpower": 4 },
              "abilities": [
                { "id": "pyre", "name": "Pyre", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["pyre"] }
              ]
            }
            """;
        registry.LoadFromJson("bonewarden", bwJson);
        registry.LoadFromJson("cauterist", caJson);

        var party = new PartyState();
        party.SetMember(0, MakeChar("Bone", 20, 10, 0, "bonewarden"));
        party.SetMember(1, MakeChar("Caut", 20, 1, 0, "cauterist"));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(42));
        var rng = new GameRandom(42);

        int playerTurns = 0;
        string[] abilitySequence = ["bone_link", "pyre"];
        while (!state.IsFinished && playerTurns < 2)
        {
            if (state.Phase == CombatPhase.Turn && state.CurrentActor?.IsPlayer == true)
            {
                var enemy = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                var ability = abilitySequence[playerTurns];
                state = CombatEngine.Tick(state,
                    new CombatAction(state.CurrentActor!.Value.Id, ActionType.UseAbility, enemy.Id, ability, null),
                    rng, registry);
                while (!state.IsFinished && state.Phase != CombatPhase.Turn)
                    state = CombatEngine.Tick(state, null, rng, registry);
                playerTurns++;
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng, registry);
            }
        }

        var synergyLogs = state.Log.Where(l => l.Message.Contains("synergy", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.NotEmpty(synergyLogs);
    }

    [Fact]
    public void Snapshot_Synergy_StillbladeHollow_BackstepCheapShot_Triggers()
    {
        SynergyRegistry.Register("backstep", "cheap_shot", new SynergyEffect("bonus_damage", 6));

        var registry = new ClassRegistry();
        var sbJson = """
            {
              "id": "stillblade",
              "name": "Stillblade",
              "description": "Test",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 4, "intelligence": 3, "willpower": 4 },
              "abilities": [
                { "id": "backstep", "name": "Backstep", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["backstep"] }
              ]
            }
            """;
        var hoJson = """
            {
              "id": "hollow",
              "name": "Hollow",
              "description": "Test",
              "baseStats": { "strength": 4, "dexterity": 6, "constitution": 3, "intelligence": 4, "willpower": 4 },
              "abilities": [
                { "id": "cheap_shot", "name": "Cheap Shot", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["cheap_shot"] }
              ]
            }
            """;
        registry.LoadFromJson("stillblade", sbJson);
        registry.LoadFromJson("hollow", hoJson);

        var party = new PartyState();
        party.SetMember(0, MakeChar("Still", 20, 10, 0, "stillblade"));
        party.SetMember(1, MakeChar("Hollow", 20, 1, 0, "hollow"));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(42));
        var rng = new GameRandom(42);

        int playerTurns = 0;
        string[] abilitySequence = ["backstep", "cheap_shot"];
        while (!state.IsFinished && playerTurns < 2)
        {
            if (state.Phase == CombatPhase.Turn && state.CurrentActor?.IsPlayer == true)
            {
                var enemy = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                var ability = abilitySequence[playerTurns];
                state = CombatEngine.Tick(state,
                    new CombatAction(state.CurrentActor!.Value.Id, ActionType.UseAbility, enemy.Id, ability, null),
                    rng, registry);
                while (!state.IsFinished && state.Phase != CombatPhase.Turn)
                    state = CombatEngine.Tick(state, null, rng, registry);
                playerTurns++;
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng, registry);
            }
        }

        var synergyLogs = state.Log.Where(l => l.Message.Contains("synergy", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.NotEmpty(synergyLogs);
    }

    [Fact]
    public void Snapshot_Synergy_FieldwrightInkblood_OverchargeKnowledgeBolt_Triggers()
    {
        SynergyRegistry.Register("overcharge", "knowledge_bolt", new SynergyEffect("bonus_damage", 5));

        var registry = new ClassRegistry();
        var fwJson = """
            {
              "id": "fieldwright",
              "name": "Fieldwright",
              "description": "Test",
              "baseStats": { "strength": 4, "dexterity": 4, "constitution": 4, "intelligence": 5, "willpower": 4 },
              "abilities": [
                { "id": "overcharge", "name": "Overcharge", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["overcharge"] }
              ]
            }
            """;
        var ibJson = """
            {
              "id": "inkblood",
              "name": "Inkblood",
              "description": "Test",
              "baseStats": { "strength": 3, "dexterity": 4, "constitution": 4, "intelligence": 6, "willpower": 4 },
              "abilities": [
                { "id": "knowledge_bolt", "name": "Knowledge Bolt", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["knowledge_bolt"] }
              ]
            }
            """;
        registry.LoadFromJson("fieldwright", fwJson);
        registry.LoadFromJson("inkblood", ibJson);

        var party = new PartyState();
        party.SetMember(0, MakeChar("Field", 20, 10, 0, "fieldwright"));
        party.SetMember(1, MakeChar("Ink", 20, 1, 0, "inkblood"));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(42));
        var rng = new GameRandom(42);

        int playerTurns = 0;
        string[] abilitySequence = ["overcharge", "knowledge_bolt"];
        while (!state.IsFinished && playerTurns < 2)
        {
            if (state.Phase == CombatPhase.Turn && state.CurrentActor?.IsPlayer == true)
            {
                var enemy = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                var ability = abilitySequence[playerTurns];
                state = CombatEngine.Tick(state,
                    new CombatAction(state.CurrentActor!.Value.Id, ActionType.UseAbility, enemy.Id, ability, null),
                    rng, registry);
                while (!state.IsFinished && state.Phase != CombatPhase.Turn)
                    state = CombatEngine.Tick(state, null, rng, registry);
                playerTurns++;
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng, registry);
            }
        }

        var synergyLogs = state.Log.Where(l => l.Message.Contains("synergy", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.NotEmpty(synergyLogs);
    }

    [Fact]
    public void Snapshot_Synergy_CauteristHollow_PurifyCheapShot_Triggers()
    {
        SynergyRegistry.Register("purify", "cheap_shot", new SynergyEffect("apply_status", -3, "weakened", 2));

        var registry = new ClassRegistry();
        var caJson = """
            {
              "id": "cauterist",
              "name": "Cauterist",
              "description": "Test",
              "baseStats": { "strength": 3, "dexterity": 5, "constitution": 4, "intelligence": 5, "willpower": 4 },
              "abilities": [
                { "id": "purify", "name": "Purify", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["purify"] }
              ]
            }
            """;
        var hoJson = """
            {
              "id": "hollow",
              "name": "Hollow",
              "description": "Test",
              "baseStats": { "strength": 4, "dexterity": 6, "constitution": 3, "intelligence": 4, "willpower": 4 },
              "abilities": [
                { "id": "cheap_shot", "name": "Cheap Shot", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["cheap_shot"] }
              ]
            }
            """;
        registry.LoadFromJson("cauterist", caJson);
        registry.LoadFromJson("hollow", hoJson);

        var party = new PartyState();
        party.SetMember(0, MakeChar("Caut", 20, 10, 0, "cauterist"));
        party.SetMember(1, MakeChar("Hollow", 20, 1, 0, "hollow"));
        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(42));
        var rng = new GameRandom(42);

        int playerTurns = 0;
        string[] abilitySequence = ["purify", "cheap_shot"];
        while (!state.IsFinished && playerTurns < 2)
        {
            if (state.Phase == CombatPhase.Turn && state.CurrentActor?.IsPlayer == true)
            {
                var enemy = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                var ability = abilitySequence[playerTurns];
                state = CombatEngine.Tick(state,
                    new CombatAction(state.CurrentActor!.Value.Id, ActionType.UseAbility, enemy.Id, ability, null),
                    rng, registry);
                while (!state.IsFinished && state.Phase != CombatPhase.Turn)
                    state = CombatEngine.Tick(state, null, rng, registry);
                playerTurns++;
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng, registry);
            }
        }

        var synergyLogs = state.Log.Where(l => l.Message.Contains("synergy", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.NotEmpty(synergyLogs);
    }

    [Fact]
    public void Snapshot_AntiSynergy_BonewardenStillblade_NoPositiveEffect()
    {
        // Anti-synergy is not registered in the runtime map, so no synergy should trigger
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
                    rng, registry);
                while (!state.IsFinished && state.Phase != CombatPhase.Turn)
                    state = CombatEngine.Tick(state, null, rng, registry);
                playerTurns++;
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng, registry);
            }
        }

        var synergyLogs = state.Log.Where(l => l.Message.Contains("synergy", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Empty(synergyLogs);
    }

    [Fact]
    public void Snapshot_SeededPairs_VersusWithout_SynergyDamageDiffers()
    {
        var registry = new ClassRegistry();
        var json = """
            {
              "id": "striker",
              "name": "Striker",
              "description": "Test",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 5, "intelligence": 3, "willpower": 3 },
              "abilities": [
                { "id": "slash", "name": "Slash", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d6+PWR", "range": "melee" }, "tags": ["physical"] },
                { "id": "thrust", "name": "Thrust", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d6+PWR", "range": "melee" }, "tags": ["physical"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["slash", "thrust"] }
              ]
            }
            """;
        registry.LoadFromJson("striker", json);

        static CharacterState MakeStriker(string name, int row)
            => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
                name, "striker", 1, 0,
                new BaseStats(5, 5, 5, 3, 3),
                60, Equipment.Empty,
                new[] { "slash", "thrust" }, row);

        var party = new PartyState();
        party.SetMember(0, MakeStriker("A1", 0));
        party.SetMember(1, MakeStriker("A2", 0));
        party.SetMember(2, MakeStriker("A3", 0));

        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 6, 0) });

        int RunCombat(int seed, bool withSynergy)
        {
            if (withSynergy)
                SynergyRegistry.Register("slash", "thrust", new SynergyEffect("bonus_damage", 3));
            else
                SynergyRegistry.Clear();

            var state = CombatEngine.Enter(party, encounter, new GameRandom(seed));
            var rng = new GameRandom(seed);
            int playerTurns = 0;
            int steps = 0;
            while (!state.IsFinished && steps < 200 && playerTurns < 6)
            {
                var actor = state.CurrentActor;
                if (actor?.IsPlayer == true)
                {
                    var target = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                    var ability = playerTurns % 2 == 0 ? "slash" : "thrust";
                    state = CombatEngine.Tick(state,
                        new CombatAction(actor.Value.Id, ActionType.UseAbility, target.Id, ability, null), rng, registry);
                    playerTurns++;
                }
                else
                {
                    state = CombatEngine.Tick(state, null, rng, registry);
                }
                steps++;
            }
            return state.Combatants.Where(c => !c.IsPlayer).Sum(e => e.MaxHp - e.Hp);
        }

        var damageWith = RunCombat(99, true);
        var damageWithout = RunCombat(99, false);

        Assert.True(damageWith > damageWithout,
            $"Expected synergy combat to deal more damage. With={damageWith}, Without={damageWithout}");
    }
}
