using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Commands;
using RPC.Engine.Dungeons;
using RPC.Engine.Party;

namespace RPC.Tests;

/// <summary>
/// Roster/bench system (T56): active&lt;-&gt;bench swap, dismissal, roster cap on recruit, bench/field-
/// promotion XP rules, and save/load round-trip of the full roster. Reuses the existing
/// <see cref="PartyState"/> aggregate (Members + Bench + DeadCharacters) rather than a parallel model.
/// </summary>
public class RosterBenchSystemTests
{
    private static CharacterState MakeChar(string name, int level, string classId = "bonewarden", int con = 20)
    {
        var stats = new BaseStats(4, 4, con, 4, 4);
        var maxHp = EffectiveStats.FromBase(stats, level).MaxHp;
        return new CharacterState(
            Guid.NewGuid(), name, classId, level, 0,
            stats, maxHp, Equipment.Empty, Array.Empty<string>(), 0);
    }

    private static void ResolveCombatByAttacking(GameState gs)
    {
        for (var i = 0; i < 50 && gs.Mode == GameMode.Combat; i++)
        {
            var combat = gs.Combat!;
            var actor = combat.CurrentActor;
            if (actor?.IsPlayer != true) continue;
            var target = combat.Combatants.FirstOrDefault(c => !c.IsPlayer && c.IsAlive);
            if (!target.IsAlive) break;
            gs.SubmitCombatAction(new CombatAction(actor.Value.Id, ActionType.Attack, target.Id, null, null));
        }
    }

    [Fact]
    public void SwapActiveBench_MovesBenchedToActive_AndActiveToBench()
    {
        var gs = new GameState(seed: 42);
        var bench = MakeChar("Benched", 2);
        gs.Party.Bench.Add(bench);
        var originalActive = gs.Party.Members[0];

        var ok = gs.SwapActiveBench(0, bench.Id);

        Assert.True(ok);
        Assert.Equal(bench.Id, gs.Party.Members[0].Id);
        Assert.Contains(gs.Party.Bench, c => c.Id == originalActive.Id);
        Assert.DoesNotContain(gs.Party.Bench, c => c.Id == bench.Id);
    }

    [Fact]
    public void SwapActiveBench_RejectedMidCombat()
    {
        var gs = new GameState(seed: 42);
        var bench = MakeChar("Benched", 2);
        gs.Party.Bench.Add(bench);
        gs.TriggerEncounter(new EncounterDef("test", "Test", new[] { new EnemySpawn("rat", 1, 0) }));
        Assert.Equal(GameMode.Combat, gs.Mode);

        var ok = gs.SwapActiveBench(0, bench.Id);

        Assert.False(ok);
        Assert.Contains(gs.Party.Bench, c => c.Id == bench.Id);
    }

    [Fact]
    public void SwapActiveBench_BenchOut_CannotEmptyActiveBelowOne()
    {
        var gs = new GameState(seed: 42);
        // Leave a single living active member in slot 0.
        for (int i = 1; i < 6; i++) gs.Party.SetMember(i, default);

        var ok = gs.SwapActiveBench(0, null);

        Assert.False(ok);
        Assert.NotEqual(Guid.Empty, gs.Party.Members[0].Id);
        Assert.Empty(gs.Party.Bench);
    }

    [Fact]
    public void BenchedCharacter_GainsNoXp_WhileActiveDoes()
    {
        var gs = new GameState(seed: 42);
        // Bench the member in slot 5 (5 active remain — bench-out is allowed).
        var toBench = gs.Party.Members[5];
        Assert.True(gs.SwapActiveBench(5, null));
        var benchedXpBefore = gs.Party.Bench.Single(c => c.Id == toBench.Id).Xp;
        var activeXpBefore = gs.Party.Members[0].Xp;

        gs.TriggerEncounter(new EncounterDef("test", "Test", new[] { new EnemySpawn("rat", 1, 0) }));
        ResolveCombatByAttacking(gs);

        var benchedXpAfter = gs.Party.Bench.Single(c => c.Id == toBench.Id).Xp;
        Assert.Equal(benchedXpBefore, benchedXpAfter);
        Assert.True(gs.Party.Members[0].Xp > activeXpBefore, "Active member should gain combat XP");
    }

    [Fact]
    public void FieldPromotion_BelowAverageMember_GainsFiftyPercentBonusXp()
    {
        var gs = new GameState(seed: 42);
        for (int i = 0; i < 6; i++) gs.Party.SetMember(i, default);
        var high = MakeChar("High", 6);
        var low = MakeChar("Low", 1);
        gs.Party.SetMember(0, high);
        gs.Party.SetMember(1, low);

        var highBefore = gs.Party.Members[0].Xp;
        var lowBefore = gs.Party.Members[1].Xp;

        gs.TriggerEncounter(new EncounterDef("test", "Test", new[] { new EnemySpawn("rat", 1, 0) }, XpReward: 10));
        ResolveCombatByAttacking(gs);

        Assert.Equal(GameMode.Exploration, gs.Mode);
        var highDelta = gs.Party.Members[0].Xp - highBefore;
        var lowDelta = gs.Party.Members[1].Xp - lowBefore;

        Assert.True(highDelta > 0 && lowDelta > 0, "Both active members should gain XP");
        // Average level is 3.5 — High (6) is at/above, Low (1) is below and gets +50%.
        Assert.Equal((int)System.Math.Round(highDelta * 1.5), lowDelta);
    }

    [Fact]
    public void RecruitFromTavern_AtRosterCap_IsRejected()
    {
        var gs = new GameState(seed: 42);
        var recruit = gs.Town.TavernRoster[0];

        // 5 active + 7 bench = 12 (cap), but slot 5 is empty so only the cap blocks recruiting.
        gs.Party.SetMember(5, default);
        for (int i = 0; i < 7; i++) gs.Party.Bench.Add(MakeChar($"B{i}", 1));
        Assert.Equal(PartyState.MaxRosterSize, gs.Party.RosterCount);

        var ok = gs.RecruitFromTavern(recruit.Id);

        Assert.False(ok);
        Assert.Contains(gs.Town.TavernRoster, r => r.Id == recruit.Id);
    }

    [Fact]
    public void Dismiss_FreesRosterSlot_AndDoesNotMoveToDead_AllowingRecruit()
    {
        var gs = new GameState(seed: 42);
        var recruit = gs.Town.TavernRoster[0];
        gs.Party.SetMember(5, default);
        var benchMembers = new List<CharacterState>();
        for (int i = 0; i < 7; i++) { var b = MakeChar($"B{i}", 1); benchMembers.Add(b); gs.Party.Bench.Add(b); }
        Assert.False(gs.RecruitFromTavern(recruit.Id)); // at cap

        var dismissed = benchMembers[0];
        Assert.True(gs.DismissCharacter(dismissed.Id));
        Assert.DoesNotContain(gs.Party.Bench, c => c.Id == dismissed.Id);
        Assert.DoesNotContain(gs.Party.DeadCharacters, c => c.Id == dismissed.Id);

        Assert.True(gs.RecruitFromTavern(recruit.Id)); // slot freed, now under cap
        Assert.Contains(gs.Party.Members, m => m.Name == recruit.Name);
    }

    [Fact]
    public void Dismiss_CannotRemoveLastLivingActiveMember()
    {
        var gs = new GameState(seed: 42);
        for (int i = 1; i < 6; i++) gs.Party.SetMember(i, default);
        var last = gs.Party.Members[0];

        var ok = gs.DismissCharacter(last.Id);

        Assert.False(ok);
        Assert.Equal(last.Id, gs.Party.Members[0].Id);
    }

    [Fact]
    public void SaveLoad_RoundTripsBench()
    {
        var path = Path.Combine(Path.GetTempPath(), $"roster_save_{Guid.NewGuid()}.json");
        try
        {
            var gs = new GameState(seed: 42);
            var bench = MakeChar("Persisted", 4, "stillblade");
            bench = bench with { Xp = 350 };
            gs.Party.Bench.Add(bench);
            gs.SaveGame(path);

            var gs2 = new GameState(seed: 99);
            Assert.True(gs2.LoadGame(path));

            var restored = Assert.Single(gs2.Party.Bench, c => c.Name == "Persisted");
            Assert.Equal("stillblade", restored.ClassId);
            Assert.Equal(4, restored.Level);
            Assert.Equal(350, restored.Xp);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void CommandPipeline_SwapActiveBench_RoutesThroughDispatcherAndHandler()
    {
        var gs = new GameState(seed: 42);
        var bench = MakeChar("Benched", 2);
        gs.Party.Bench.Add(bench);
        var handler = new GameCommandHandler(gs, new StubDungeonGenerator());

        var cmd = CommandDispatcher.Parse(new PlayerAction
        {
            Type = "swap_active_bench",
            Slot = 0,
            TargetId = bench.Id.ToString()
        });
        var result = handler.Execute(cmd);

        Assert.True(result.StateChanged);
        Assert.Equal(bench.Id, gs.Party.Members[0].Id);
    }

    [Fact]
    public void CommandPipeline_DismissCharacter_RoutesThroughDispatcherAndHandler()
    {
        var gs = new GameState(seed: 42);
        var bench = MakeChar("Benched", 2);
        gs.Party.Bench.Add(bench);
        var handler = new GameCommandHandler(gs, new StubDungeonGenerator());

        var cmd = CommandDispatcher.Parse(new PlayerAction
        {
            Type = "dismiss_character",
            TargetId = bench.Id.ToString()
        });
        var result = handler.Execute(cmd);

        Assert.True(result.StateChanged);
        Assert.DoesNotContain(gs.Party.Bench, c => c.Id == bench.Id);
    }
}
