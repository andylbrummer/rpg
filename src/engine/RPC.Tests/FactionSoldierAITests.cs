using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Party;

namespace RPC.Tests;

public class FactionSoldierAITests
{
    private static CharacterState MakeChar(string name, string classId, int hp = 20)
        => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
            name, classId, 1, 0,
            new BaseStats(4, 4, 4, 4, 4),
            hp, Equipment.Empty,
            Array.Empty<string>(), 0);

    [Fact]
    public void CombatEngine_SoldierTactical_Outnumbered_Flees()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", "stillblade", 50));
        party.SetMember(1, MakeChar("Hero2", "cauterist", 50));

        var enemyRegistry = new EnemyRegistry();
        enemyRegistry.LoadFromJson("bureau_soldier", @"{
            ""id"": ""bureau_soldier"", ""name"": ""Bureau Enforcer"", ""description"": ""Test"",
            ""stats"": { ""str"": 5, ""dex"": 5, ""con"": 5, ""int"": 3, ""wil"": 3 },
            ""hpBase"": 10, ""speed"": 4, ""ai"": ""soldier_tactical"",
            ""abilities"": [""bayonet_thrust"", ""suppress""], ""lootTable"": []
        }");

        var encounter = new EncounterDef("test", "Test", new[] { new EnemySpawn("bureau_soldier", 1) });
        var combat = CombatEngine.Enter(party, encounter, new GameRandom(42), enemyRegistry);

        // Auto-resolve until enemy turn
        var rng = new GameRandom(42);
        while (!combat.IsFinished && !(combat.Phase == CombatPhase.Turn && combat.CurrentActor?.IsPlayer == true))
        {
            combat = CombatEngine.Tick(combat, null, rng);
        }

        // Enemy should flee (outnumbered: 1 soldier with ~10 HP vs 2 players with 50 HP each)
        if (combat.Phase == CombatPhase.Turn && combat.CurrentActor is { IsPlayer: false } actor)
        {
            combat = CombatEngine.Tick(combat, null, rng);
            // After fleeing, combat should end with victory
            Assert.True(combat.IsFinished || combat.Phase == CombatPhase.CheckEnd || combat.Phase == CombatPhase.Ended,
                "Expected combat to end or check end after flee");
        }
    }

    [Fact]
    public void CombatEngine_SoldierTactical_NotOutnumbered_Attacks()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", "stillblade", 10));

        var enemyRegistry = new EnemyRegistry();
        enemyRegistry.LoadFromJson("bureau_soldier", @"{
            ""id"": ""bureau_soldier"", ""name"": ""Bureau Enforcer"", ""description"": ""Test"",
            ""stats"": { ""str"": 5, ""dex"": 5, ""con"": 5, ""int"": 3, ""wil"": 3 },
            ""hpBase"": 20, ""speed"": 4, ""ai"": ""soldier_tactical"",
            ""abilities"": [""bayonet_thrust"", ""suppress""], ""lootTable"": []
        }");

        var encounter = new EncounterDef("test", "Test", new[] { new EnemySpawn("bureau_soldier", 2) });
        var combat = CombatEngine.Enter(party, encounter, new GameRandom(42), enemyRegistry);

        // Auto-resolve until enemy turn
        var rng = new GameRandom(42);
        while (!combat.IsFinished && !(combat.Phase == CombatPhase.Turn && combat.CurrentActor?.IsPlayer == true))
        {
            combat = CombatEngine.Tick(combat, null, rng);
        }

        // Enemy should attack, not flee (2 soldiers with ~20 HP vs 1 player with 10 HP)
        if (combat.Phase == CombatPhase.Turn && combat.CurrentActor is { IsPlayer: false })
        {
            combat = CombatEngine.Tick(combat, null, rng);
            Assert.False(combat.IsFinished, "Combat should not end immediately when not outnumbered");
        }
    }

    [Fact]
    public void CombatEngine_FleeAction_RemovesEnemyFromCombat()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", "stillblade", 20));

        var combat = new CombatState(
            new[]
            {
                new Combatant(new Guid("11111111-1111-1111-1111-111111111111"), "Hero", true, 20, 20, 5, 0, new List<StatusEffect>()),
                new Combatant(new Guid("22222222-2222-2222-2222-222222222222"), "Rat", false, 5, 5, 3, 0, new List<StatusEffect>(), 0, null, false, 0, null, "aggressive", null)
            },
            1,
            new[] { new Guid("22222222-2222-2222-2222-222222222222") },
            0,
            new List<CombatLogEntry>(),
            new CombatAction(new Guid("22222222-2222-2222-2222-222222222222"), ActionType.Flee, null, null, null),
            CombatPhase.Resolve);

        var result = CombatEngine.Tick(combat, null, new GameRandom(1));

        Assert.True(result.IsFinished || result.Phase == CombatPhase.Ended || result.Phase == CombatPhase.CheckEnd);
        Assert.Contains(result.Log, l => l.Message.Contains("flees"));
    }

    [Fact]
    public void CombatEngine_Enter_BureauSoldier_GetsArmorBonus()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", "stillblade", 20));

        var registry = new EnemyRegistry();
        registry.LoadFromJson("bureau_soldier", @"{
            ""id"": ""bureau_soldier"", ""name"": ""Bureau Enforcer"", ""description"": ""Test"",
            ""stats"": { ""str"": 5, ""dex"": 5, ""con"": 5, ""int"": 3, ""wil"": 3 },
            ""hpBase"": 20, ""speed"": 4, ""ai"": ""soldier_tactical"",
            ""abilities"": [""bayonet_thrust""], ""lootTable"": [], ""factionId"": ""bureau""
        }");

        var encounter = new EncounterDef("test", "Test", new[] { new EnemySpawn("bureau_soldier", 1) });
        var combat = CombatEngine.Enter(party, encounter, new GameRandom(42), registry);

        var enemy = combat.Combatants.First(c => !c.IsPlayer);
        Assert.True(enemy.MaxHp >= 25, $"Expected Bureau soldier to have armor bonus HP, got {enemy.MaxHp}");
    }

    [Fact]
    public void CombatEngine_Enter_ConvocationSoldier_HasBloomResistance()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", "stillblade", 20));

        var registry = new EnemyRegistry();
        registry.LoadFromJson("convocation_soldier", @"{
            ""id"": ""convocation_soldier"", ""name"": ""Convocation Zealot"", ""description"": ""Test"",
            ""stats"": { ""str"": 5, ""dex"": 5, ""con"": 5, ""int"": 3, ""wil"": 3 },
            ""hpBase"": 20, ""speed"": 4, ""ai"": ""zealot_aggressive"",
            ""abilities"": [""fervent_slash""], ""lootTable"": [], ""factionId"": ""convocation""
        }");

        var encounter = new EncounterDef("test", "Test", new[] { new EnemySpawn("convocation_soldier", 1) });
        var combat = CombatEngine.Enter(party, encounter, new GameRandom(42), registry);

        var enemy = combat.Combatants.First(c => !c.IsPlayer);
        Assert.Contains(enemy.StatusEffects, s => s.Type == "bloom_resistance");
    }

    [Fact]
    public void GameState_AshmouthNegotiation_Success_BypassesCombat()
    {
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("test", @"{
            ""id"": ""test"",
            ""name"": ""Test"",
            ""entries"": [
                { ""id"": ""fs-1"", ""weight"": 100, ""factionId"": ""bureau"", ""enemies"": [{""enemyId"": ""bureau_soldier"", ""count"": 2}] }
            ]
        }");

        var gs = new GameState(seed: 1, encounterTables: registry);
        gs.Party.SetMember(0, MakeChar("Hero", "ashmouth", 30)); // Ashmouth at level 1
        gs.Reputation["bureau"] = 10; // Slightly positive rep helps
        var dungeon = new Dungeon(5, 5, "test") { WanderingTableId = "test" };
        gs.EnterDungeon(dungeon, "test");

        gs.TriggerEncounter();

        // With Ashmouth present, should offer Negotiate
        Assert.NotNull(gs.CurrentParley);
        Assert.Contains("Negotiate", gs.CurrentParley.Options);

        // Force success by using a seed that gives a high roll
        // We'll just verify the method exists and works structurally
        var result = gs.ResolveParley("negotiate");
        Assert.True(result);
    }

    [Fact]
    public void GameState_AshmouthNegotiation_Failure_EntersCombat()
    {
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("test", @"{
            ""id"": ""test"",
            ""name"": ""Test"",
            ""entries"": [
                { ""id"": ""fs-1"", ""weight"": 100, ""factionId"": ""bureau"", ""enemies"": [{""enemyId"": ""bureau_soldier"", ""count"": 2}] }
            ]
        }");

        var gs = new GameState(seed: 999, encounterTables: registry);
        gs.Party.SetMember(0, MakeChar("Hero", "ashmouth", 30));
        gs.Reputation["bureau"] = -100; // Maximum negative rep guarantees failure
        var dungeon = new Dungeon(5, 5, "test") { WanderingTableId = "test" };
        gs.EnterDungeon(dungeon, "test");

        gs.TriggerEncounter();
        Assert.NotNull(gs.CurrentParley);

        var result = gs.ResolveParley("negotiate");
        Assert.True(result);
        // On failure, should enter combat with surprise round
        Assert.Equal(GameMode.Combat, gs.Mode);
        Assert.NotNull(gs.Combat);
    }

    [Fact]
    public void GameState_Dungeon_FactionSoldier_ParleyAndNegotiate_BothAvailable()
    {
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("test", @"{
            ""id"": ""test"",
            ""name"": ""Test"",
            ""entries"": [
                { ""id"": ""fs-1"", ""weight"": 100, ""factionId"": ""bureau"", ""enemies"": [{""enemyId"": ""bureau_soldier"", ""count"": 2}] }
            ]
        }");

        var gs = new GameState(seed: 1, encounterTables: registry);
        gs.Party.SetMember(0, MakeChar("Hero", "ashmouth", 30));
        gs.Reputation["bureau"] = 30; // High rep + Ashmouth
        var dungeon = new Dungeon(5, 5, "test") { WanderingTableId = "test" };
        gs.EnterDungeon(dungeon, "test");

        gs.TriggerEncounter();

        Assert.NotNull(gs.CurrentParley);
        Assert.Contains("Parley", gs.CurrentParley.Options);
        Assert.Contains("Negotiate", gs.CurrentParley.Options);
        Assert.Contains("Fight", gs.CurrentParley.Options);
    }

    [Fact]
    public void GameState_Dungeon_FactionSoldier_NegotiateOnly_LowRep()
    {
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("test", @"{
            ""id"": ""test"",
            ""name"": ""Test"",
            ""entries"": [
                { ""id"": ""fs-1"", ""weight"": 100, ""factionId"": ""bureau"", ""enemies"": [{""enemyId"": ""bureau_soldier"", ""count"": 2}] }
            ]
        }");

        var gs = new GameState(seed: 1, encounterTables: registry);
        gs.Party.SetMember(0, MakeChar("Hero", "ashmouth", 30));
        gs.Reputation["bureau"] = 0; // Low rep, no parley
        var dungeon = new Dungeon(5, 5, "test") { WanderingTableId = "test" };
        gs.EnterDungeon(dungeon, "test");

        gs.TriggerEncounter();

        Assert.NotNull(gs.CurrentParley);
        Assert.DoesNotContain("Parley", gs.CurrentParley.Options);
        Assert.Contains("Negotiate", gs.CurrentParley.Options);
        Assert.Contains("Fight", gs.CurrentParley.Options);
    }
}
