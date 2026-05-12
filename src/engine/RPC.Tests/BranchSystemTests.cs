using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Party;

namespace RPC.Tests;

public class BranchSystemTests
{
    private static ClassRegistry MakeBranchRegistry()
    {
        var registry = new ClassRegistry();
        var json = """
            {
              "id": "test_class",
              "name": "Test Class",
              "description": "Test",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 5, "intelligence": 3, "willpower": 3 },
              "abilities": [
                { "id": "base_attack", "name": "Base Attack", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["physical"] },
                { "id": "branch_a_ult", "name": "Branch A Ult", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "2d6+PWR", "range": "melee" }, "tags": ["physical"], "branch": "branch_a" },
                { "id": "branch_b_ult", "name": "Branch B Ult", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d8+PWR", "range": "far" }, "tags": ["physical"], "branch": "branch_b" }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["base_attack"] },
                { "level": 2, "hpGain": 4, "statGain": { "strength": 1, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": [] },
                { "level": 3, "hpGain": 4, "statGain": { "strength": 1, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": [] }
              ],
              "availableBranches": ["branch_a", "branch_b"]
            }
            """;
        registry.LoadFromJson("test_class", json);
        return registry;
    }

    private static CharacterState MakeChar(int level = 1, int xp = 0, string? branchChoice = null)
        => new(new Guid("11111111-1111-1111-1111-111111111111"), "Hero", "test_class",
            level, xp, new BaseStats(5, 5, 5, 3, 3), 30, Equipment.Empty,
            new[] { "base_attack" }, 0, branchChoice);

    [Fact]
    public void CharacterState_Level3_NoBranchChoice_AwaitingBranchChoiceIsTrue()
    {
        var c = MakeChar(level: 3);
        Assert.True(c.AwaitingBranchChoice);
    }

    [Fact]
    public void CharacterState_Level2_NoBranchChoice_AwaitingBranchChoiceIsFalse()
    {
        var c = MakeChar(level: 2);
        Assert.False(c.AwaitingBranchChoice);
    }

    [Fact]
    public void CharacterState_Level3_WithBranchChoice_AwaitingBranchChoiceIsFalse()
    {
        var c = MakeChar(level: 3, branchChoice: "branch_a");
        Assert.False(c.AwaitingBranchChoice);
    }

    [Fact]
    public void ChooseBranch_ValidBranch_AddsBranchAbilities()
    {
        var registry = MakeBranchRegistry();
        var gs = new GameState(seed: 1, classRegistry: registry);
        gs.Party.SetMember(0, MakeChar(level: 3));

        var result = gs.ChooseBranch(new Guid("11111111-1111-1111-1111-111111111111"), "branch_a");

        Assert.True(result);
        var member = gs.Party.Members[0];
        Assert.Equal("branch_a", member.BranchChoice);
        Assert.Contains("branch_a_ult", member.KnownAbilities);
        Assert.DoesNotContain("branch_b_ult", member.KnownAbilities);
    }

    [Fact]
    public void ChooseBranch_InvalidBranch_ReturnsFalse()
    {
        var registry = MakeBranchRegistry();
        var gs = new GameState(seed: 1, classRegistry: registry);
        gs.Party.SetMember(0, MakeChar(level: 3));

        var result = gs.ChooseBranch(new Guid("11111111-1111-1111-1111-111111111111"), "branch_c");

        Assert.False(result);
        Assert.Null(gs.Party.Members[0].BranchChoice);
    }

    [Fact]
    public void ChooseBranch_UnknownCharacter_ReturnsFalse()
    {
        var registry = MakeBranchRegistry();
        var gs = new GameState(seed: 1, classRegistry: registry);

        var result = gs.ChooseBranch(Guid.NewGuid(), "branch_a");
        Assert.False(result);
    }

    [Fact]
    public void ChooseBranch_OtherBranch_AddsDifferentAbilities()
    {
        var registry = MakeBranchRegistry();
        var gs = new GameState(seed: 1, classRegistry: registry);
        gs.Party.SetMember(0, MakeChar(level: 3));

        gs.ChooseBranch(new Guid("11111111-1111-1111-1111-111111111111"), "branch_b");

        var member = gs.Party.Members[0];
        Assert.Equal("branch_b", member.BranchChoice);
        Assert.Contains("branch_b_ult", member.KnownAbilities);
        Assert.DoesNotContain("branch_a_ult", member.KnownAbilities);
    }
}

public class TownExitBlockTests
{
    private static ClassRegistry MakeBranchRegistry()
    {
        var registry = new ClassRegistry();
        var json = """
            {
              "id": "test_class",
              "name": "Test Class",
              "description": "Test",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 5, "intelligence": 3, "willpower": 3 },
              "abilities": [
                { "id": "base_attack", "name": "Base Attack", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["physical"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["base_attack"] },
                { "level": 2, "hpGain": 4, "statGain": { "strength": 1, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": [] },
                { "level": 3, "hpGain": 4, "statGain": { "strength": 1, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": [] }
              ],
              "availableBranches": ["branch_a", "branch_b"]
            }
            """;
        registry.LoadFromJson("test_class", json);
        return registry;
    }

    [Fact]
    public void EnterDungeon_Blocked_WhenPendingBranchChoice()
    {
        var registry = MakeBranchRegistry();
        var gs = new GameState(seed: 1, classRegistry: registry);
        gs.Party.SetMember(0, new CharacterState(
            new Guid("11111111-1111-1111-1111-111111111111"), "Hero", "test_class",
            3, 120, new BaseStats(5, 5, 5, 3, 3), 30, Equipment.Empty,
            new[] { "base_attack" }, 0));

        Assert.True(gs.HasPendingBranchChoices);

        var dungeon = new RPC.Engine.Models.Dungeons.Dungeon(3, 3, "test");
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                dungeon.Tiles[x, y] = new RPC.Engine.Models.Dungeons.Tile(RPC.Engine.Models.Dungeons.TileType.Floor);

        gs.EnterDungeon(dungeon, "test");
        Assert.Null(gs.CurrentDungeon);
    }

    [Fact]
    public void Travel_Blocked_WhenPendingBranchChoice()
    {
        var registry = MakeBranchRegistry();
        var gs = new GameState(seed: 1, classRegistry: registry);
        gs.Party.SetMember(0, new CharacterState(
            new Guid("11111111-1111-1111-1111-111111111111"), "Hero", "test_class",
            3, 120, new BaseStats(5, 5, 5, 3, 3), 30, Equipment.Empty,
            new[] { "base_attack" }, 0));

        Assert.True(gs.HasPendingBranchChoices);

        var result = gs.Travel("some_node");
        Assert.False(result);
    }

    [Fact]
    public void EnterDungeon_Allowed_AfterBranchChoiceResolved()
    {
        var registry = MakeBranchRegistry();
        var gs = new GameState(seed: 1, classRegistry: registry);
        gs.Party.SetMember(0, new CharacterState(
            new Guid("11111111-1111-1111-1111-111111111111"), "Hero", "test_class",
            3, 120, new BaseStats(5, 5, 5, 3, 3), 30, Equipment.Empty,
            new[] { "base_attack" }, 0));

        gs.ChooseBranch(new Guid("11111111-1111-1111-1111-111111111111"), "branch_a");
        Assert.False(gs.HasPendingBranchChoices);

        var dungeon = new RPC.Engine.Models.Dungeons.Dungeon(3, 3, "test");
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                dungeon.Tiles[x, y] = new RPC.Engine.Models.Dungeons.Tile(RPC.Engine.Models.Dungeons.TileType.Floor);

        gs.EnterDungeon(dungeon, "test");
        Assert.NotNull(gs.CurrentDungeon);
    }

    [Fact]
    public void Travel_Allowed_WhenNoPendingBranchChoices()
    {
        var registry = MakeBranchRegistry();
        var gs = new GameState(seed: 1, classRegistry: registry);
        gs.Party.SetMember(0, new CharacterState(
            new Guid("11111111-1111-1111-1111-111111111111"), "Hero", "test_class",
            1, 0, new BaseStats(5, 5, 5, 3, 3), 30, Equipment.Empty,
            new[] { "base_attack" }, 0));

        Assert.False(gs.HasPendingBranchChoices);
    }
}

public class BranchCombatSnapshotTests
{
    private static ClassRegistry MakeBranchRegistry()
    {
        var registry = new ClassRegistry();
        var json = """
            {
              "id": "test_class",
              "name": "Test Class",
              "description": "Test",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 5, "intelligence": 3, "willpower": 3 },
              "abilities": [
                { "id": "base_attack", "name": "Base Attack", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["physical"] },
                { "id": "branch_a_ult", "name": "Branch A Ult", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "2d6+PWR", "range": "melee" }, "tags": ["physical"], "branch": "branch_a" },
                { "id": "branch_b_ult", "name": "Branch B Ult", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d6+PWR", "range": "melee" }, "tags": ["physical"], "branch": "branch_b" }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["base_attack"] },
                { "level": 2, "hpGain": 4, "statGain": { "strength": 1, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": [] },
                { "level": 3, "hpGain": 4, "statGain": { "strength": 1, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": [] }
              ],
              "availableBranches": ["branch_a", "branch_b"]
            }
            """;
        registry.LoadFromJson("test_class", json);
        return registry;
    }

    private static CharacterState MakeChar(string name, string? branchChoice = null)
        => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
            name, "test_class", 3, 120,
            new BaseStats(5, 5, 5, 3, 3), 60, Equipment.Empty,
            new[] { "base_attack" }, 0, branchChoice);

    [Fact]
    public void Snapshot_SameCharacter_DifferentBranches_DifferentCombatOutput()
    {
        var registry = MakeBranchRegistry();

        var partyA = new PartyState();
        partyA.SetMember(0, MakeChar("HeroA", "branch_a"));

        var partyB = new PartyState();
        partyB.SetMember(0, MakeChar("HeroB", "branch_b"));

        // Apply branch abilities
        var charA = partyA.Members[0];
        var classDef = registry.Get("test_class")!;
        var abilitiesA = charA.KnownAbilities
            .Concat(classDef.Abilities.Where(a => a.Branch == "branch_a").Select(a => a.Id))
            .Distinct().ToArray();
        partyA.SetMember(0, charA with { KnownAbilities = abilitiesA });

        var charB = partyB.Members[0];
        var abilitiesB = charB.KnownAbilities
            .Concat(classDef.Abilities.Where(a => a.Branch == "branch_b").Select(a => a.Id))
            .Distinct().ToArray();
        partyB.SetMember(0, charB with { KnownAbilities = abilitiesB });

        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 4, 0) });

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
                    var ability = party.Members[0].KnownAbilities.Contains("branch_a_ult")
                        ? "branch_a_ult"
                        : "branch_b_ult";
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

        var damageA = RunCombat(partyA, 42);
        var damageB = RunCombat(partyB, 42);

        Assert.True(damageA > damageB,
            $"Expected branch_a (2d6+PWR) to deal more damage than branch_b (1d6+PWR). A={damageA}, B={damageB}");
    }
}
