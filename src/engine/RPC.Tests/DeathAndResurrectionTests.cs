using System.Reflection;
using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Party;
using RPC.Engine.Save;

namespace RPC.Tests;

public class DeathAndResurrectionTests
{
    private static CharacterState MakeChar(Guid id, string name, string classId, int hp = 20)
        => new(id, name, classId, 1, 0,
            new BaseStats(4, 4, 4, 4, 4),
            hp, Equipment.Empty,
            Array.Empty<string>(), 0);

    private static void InjectCombat(GameState gs, CombatState combat)
    {
        var prop = typeof(GameState).GetProperty("Combat", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        prop.SetValue(gs, combat);
        gs.GetType().GetProperty("Mode", BindingFlags.Instance | BindingFlags.Public)!.SetValue(gs, GameMode.Combat);
    }

    [Fact]
    public void CombatEnd_DownedUnstabilized_CharacterDies()
    {
        var gs = new GameState(seed: 1);
        var heroId = new Guid("11111111-1111-1111-1111-111111111111");
        gs.Party.SetMember(0, MakeChar(heroId, "Hero", "stillblade", 20));

        var combatant = new Combatant(heroId, "Hero", true, 0, 20, 5, 0, new List<StatusEffect>());
        var combat = new CombatState(
            new[] { combatant },
            1, Array.Empty<Guid>(), 0,
            new List<CombatLogEntry>(),
            null, CombatPhase.Ended, 10);

        InjectCombat(gs, combat);
        gs.SubmitCombatAction(new CombatAction(heroId, ActionType.Defend, heroId, null, null));

        Assert.Equal(GameMode.Exploration, gs.Mode);
        Assert.Equal(default, gs.Party.Members[0]);
        Assert.Single(gs.Party.DeadCharacters);
        Assert.Equal("Hero", gs.Party.DeadCharacters[0].Name);
        Assert.Equal(0, gs.Party.DeadCharacters[0].CurrentHp);

        var diedLog = gs.ActionLog.LastOrDefault(e => e.Type == "character_died");
        Assert.NotNull(diedLog);
        Assert.Equal("Hero", diedLog.Payload["characterName"]);
    }

    [Fact]
    public void CombatEnd_DownedStabilized_CharacterSurvivesWithOneHp()
    {
        var gs = new GameState(seed: 1);
        var heroId = new Guid("11111111-1111-1111-1111-111111111111");
        gs.Party.SetMember(0, MakeChar(heroId, "Hero", "stillblade", 20));

        var stabilized = new List<StatusEffect> { new("stabilized", 1, null) };
        var combatant = new Combatant(heroId, "Hero", true, 0, 20, 5, 0, stabilized);
        var combat = new CombatState(
            new[] { combatant },
            1, Array.Empty<Guid>(), 0,
            new List<CombatLogEntry>(),
            null, CombatPhase.Ended, 10);

        InjectCombat(gs, combat);
        gs.SubmitCombatAction(new CombatAction(heroId, ActionType.Defend, heroId, null, null));

        Assert.Equal(GameMode.Exploration, gs.Mode);
        Assert.Equal(1, gs.Party.Members[0].CurrentHp);
        Assert.Empty(gs.Party.DeadCharacters);

        var stabilizedLog = gs.ActionLog.LastOrDefault(e => e.Type == "character_stabilized");
        Assert.NotNull(stabilizedLog);
        Assert.Equal("Hero", stabilizedLog.Payload["characterName"]);
    }

    [Fact]
    public void CombatEnd_TotalPartyWipe_AllMovedToDead()
    {
        var gs = new GameState(seed: 1);
        var id1 = new Guid("11111111-1111-1111-1111-111111111111");
        var id2 = new Guid("22222222-2222-2222-2222-222222222222");
        gs.Party.SetMember(0, MakeChar(id1, "Hero", "stillblade", 20));
        gs.Party.SetMember(1, MakeChar(id2, "Sera", "cauterist", 20));

        var c1 = new Combatant(id1, "Hero", true, 0, 20, 5, 0, new List<StatusEffect>());
        var c2 = new Combatant(id2, "Sera", true, 0, 20, 5, 0, new List<StatusEffect>());
        var combat = new CombatState(
            new[] { c1, c2 },
            1, Array.Empty<Guid>(), 0,
            new List<CombatLogEntry>(),
            null, CombatPhase.Ended, 10);

        InjectCombat(gs, combat);
        gs.SubmitCombatAction(new CombatAction(id1, ActionType.Defend, id1, null, null));

        Assert.Equal(2, gs.Party.DeadCharacters.Count);
        Assert.Equal(default, gs.Party.Members[0]);
        Assert.Equal(default, gs.Party.Members[1]);
    }

    [Fact]
    public void Resurrect_FirstAttempt_Costs500Gold1TitheMinus1Stat()
    {
        var gs = new GameState(seed: 1);
        for (int i = 0; i < 6; i++) gs.Party.SetMember(i, default);
        var heroId = new Guid("11111111-1111-1111-1111-111111111111");
        var dead = MakeChar(heroId, "Hero", "stillblade", 0);
        gs.Party.DeadCharacters.Add(dead);
        gs.PartyGold = 1000;
        gs.TitheTokens = 5;

        var originalStats = dead.BaseStats;
        var originalSum = originalStats.Strength + originalStats.Dexterity + originalStats.Constitution
                        + originalStats.Intelligence + originalStats.Willpower;

        var result = gs.ResurrectCharacter(heroId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(500, result.GoldCost);
        Assert.Equal(1, result.TitheTokenCost);
        Assert.Equal(1, result.StatLossCount);
        Assert.False(result.BranchLocked);
        Assert.NotNull(result.Character);

        Assert.Equal(500, gs.PartyGold);
        Assert.Equal(4, gs.TitheTokens);
        Assert.Empty(gs.Party.DeadCharacters);
        Assert.Equal("Hero", gs.Party.Members[0].Name);
        Assert.True(gs.Party.Members[0].IsAlive);

        var newStats = gs.Party.Members[0].BaseStats;
        var newSum = newStats.Strength + newStats.Dexterity + newStats.Constitution
                   + newStats.Intelligence + newStats.Willpower;
        Assert.Equal(originalSum - 1, newSum);
        Assert.Equal(1, gs.Party.Members[0].ResurrectionAttempts);
    }

    [Fact]
    public void Resurrect_SecondAttempt_Costs1500Gold2TitheMinus2StatsAndLocksBranch()
    {
        var gs = new GameState(seed: 1);
        for (int i = 0; i < 6; i++) gs.Party.SetMember(i, default);
        var heroId = new Guid("11111111-1111-1111-1111-111111111111");
        var dead = MakeChar(heroId, "Hero", "stillblade", 0)
            with { ResurrectionAttempts = 1 };
        gs.Party.DeadCharacters.Add(dead);
        gs.PartyGold = 2000;
        gs.TitheTokens = 5;

        var originalSum = dead.BaseStats.Strength + dead.BaseStats.Dexterity
                        + dead.BaseStats.Constitution + dead.BaseStats.Intelligence
                        + dead.BaseStats.Willpower;

        var result = gs.ResurrectCharacter(heroId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(1500, result.GoldCost);
        Assert.Equal(2, result.TitheTokenCost);
        Assert.Equal(2, result.StatLossCount);
        Assert.True(result.BranchLocked);

        Assert.Equal(500, gs.PartyGold);
        Assert.Equal(3, gs.TitheTokens);

        CharacterState character = result!.Character!.Value;
        var newSum = character.BaseStats.Strength + character.BaseStats.Dexterity
                   + character.BaseStats.Constitution + character.BaseStats.Intelligence
                   + character.BaseStats.Willpower;
        Assert.Equal(originalSum - 2, newSum);
        Assert.True(character.BranchAdvancementLocked);
        Assert.Equal(2, character.ResurrectionAttempts);
    }

    [Fact]
    public void Resurrect_ThirdAttempt_FailsPermanentlyDead()
    {
        var gs = new GameState(seed: 1);
        var heroId = new Guid("11111111-1111-1111-1111-111111111111");
        var dead = MakeChar(heroId, "Hero", "stillblade", 0)
            with { ResurrectionAttempts = 2 };
        gs.Party.DeadCharacters.Add(dead);
        gs.PartyGold = 9999;
        gs.TitheTokens = 99;

        var result = gs.ResurrectCharacter(heroId);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Character is permanently dead.", result.Error);
        Assert.Single(gs.Party.DeadCharacters);
        Assert.Equal(9999, gs.PartyGold);
        Assert.Equal(99, gs.TitheTokens);
    }

    [Fact]
    public void Resurrect_NotEnoughGold_Fails()
    {
        var gs = new GameState(seed: 1);
        var heroId = new Guid("11111111-1111-1111-1111-111111111111");
        gs.Party.DeadCharacters.Add(MakeChar(heroId, "Hero", "stillblade", 0));
        gs.PartyGold = 100;
        gs.TitheTokens = 5;

        var result = gs.ResurrectCharacter(heroId);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Not enough gold.", result.Error);
    }

    [Fact]
    public void Resurrect_NotEnoughTitheTokens_Fails()
    {
        var gs = new GameState(seed: 1);
        var heroId = new Guid("11111111-1111-1111-1111-111111111111");
        gs.Party.DeadCharacters.Add(MakeChar(heroId, "Hero", "stillblade", 0));
        gs.PartyGold = 1000;
        gs.TitheTokens = 0;

        var result = gs.ResurrectCharacter(heroId);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Not enough tithe tokens.", result.Error);
    }

    [Fact]
    public void Resurrect_PartyFull_Fails()
    {
        var gs = new GameState(seed: 1);
        var heroId = new Guid("11111111-1111-1111-1111-111111111111");
        gs.Party.DeadCharacters.Add(MakeChar(heroId, "Hero", "stillblade", 0));
        gs.PartyGold = 1000;
        gs.TitheTokens = 5;

        // Fill all 6 slots
        for (int i = 0; i < 6; i++)
        {
            var id = new Guid($"{i + 1}{i + 1}{i + 1}{i + 1}{i + 1}{i + 1}{i + 1}{i + 1}-{i + 1}{i + 1}{i + 1}{i + 1}-{i + 1}{i + 1}{i + 1}{i + 1}-{i + 1}{i + 1}{i + 1}{i + 1}-{i + 1}{i + 1}{i + 1}{i + 1}{i + 1}{i + 1}{i + 1}{i + 1}{i + 1}{i + 1}{i + 1}{i + 1}");
            gs.Party.SetMember(i, MakeChar(id, $"Filler{i}", "stillblade", 20));
        }

        var result = gs.ResurrectCharacter(heroId);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Party is full.", result.Error);
    }

    [Fact]
    public void Resurrect_UnknownCharacter_ReturnsNull()
    {
        var gs = new GameState(seed: 1);
        var result = gs.ResurrectCharacter(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public void SaveLoad_PreservesDeadCharactersAndTitheTokens()
    {
        var gs = new GameState(seed: 1);
        var heroId = new Guid("11111111-1111-1111-1111-111111111111");
        gs.Party.DeadCharacters.Add(MakeChar(heroId, "Hero", "stillblade", 0)
            with { ResurrectionAttempts = 1, BranchAdvancementLocked = true });
        gs.TitheTokens = 7;

        var path = Path.Combine(Path.GetTempPath(), $"test_save_t65_{Guid.NewGuid()}.json");
        SaveSystem.Save(gs, path);

        var gs2 = new GameState(seed: 99);
        Assert.True(SaveSystem.Load(gs2, path));

        Assert.Equal(7, gs2.TitheTokens);
        Assert.Single(gs2.Party.DeadCharacters);
        Assert.Equal("Hero", gs2.Party.DeadCharacters[0].Name);
        Assert.Equal(1, gs2.Party.DeadCharacters[0].ResurrectionAttempts);
        Assert.True(gs2.Party.DeadCharacters[0].BranchAdvancementLocked);

        File.Delete(path);
    }

    [Fact]
    public void Reset_ClearsDeadCharactersAndTitheTokens()
    {
        var gs = new GameState(seed: 1);
        gs.Party.DeadCharacters.Add(MakeChar(Guid.NewGuid(), "Dead", "stillblade", 0));
        gs.TitheTokens = 5;

        gs.Reset();

        Assert.Empty(gs.Party.DeadCharacters);
        Assert.Equal(0, gs.TitheTokens);
    }

    [Fact]
    public void SaveSchemaVersion_IsSeven()
    {
        var gs = new GameState(seed: 1);
        var path = Path.Combine(Path.GetTempPath(), $"test_save_schema_{Guid.NewGuid()}.json");
        SaveSystem.Save(gs, path);

        var json = File.ReadAllText(path);
        Assert.Contains("\"schemaVersion\": 7", json);

        File.Delete(path);
    }
}
