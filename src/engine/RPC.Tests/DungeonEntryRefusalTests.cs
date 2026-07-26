using RPC.Engine;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

/// <summary>
/// The engine refuses dungeon entry under several rules. Each refusal has to be observable: a
/// silent return leaves the party in town with nothing to distinguish a rule from a dropped input.
/// </summary>
public class DungeonEntryRefusalTests
{
    /// <summary>
    /// A refused dungeon entry used to return silently for a pending branch choice and for an ended
    /// campaign: the party stayed in town with nothing in the log to say why, which is
    /// indistinguishable from the input never arriving. The client disables the button from a state
    /// snapshot, so a click racing the level-up that created the choice still reaches the engine.
    /// </summary>
    [Fact]
    public void Entering_A_Dungeon_With_A_Pending_Branch_Choice_Says_Why_It_Was_Refused()
    {
        var gs = new GameState(seed: 42);
        gs.Party.SetMember(0, gs.Party.Members[0] with { Level = 3, BranchChoice = null });
        Assert.True(gs.HasPendingBranchChoices);

        gs.EnterDungeon(new Dungeon(3, 3, "test"), "broken_engine");

        Assert.Equal(GameMode.Menu, gs.Mode);
        Assert.Contains(gs.ActionLog, e => e.Type == "dungeon_blocked_pending_branches");
    }

    [Fact]
    public void Entering_A_Dungeon_After_The_Campaign_Ended_Says_Why_It_Was_Refused()
    {
        var gs = new GameState(seed: 42);
        gs.CampaignEnded = true;

        gs.EnterDungeon(new Dungeon(3, 3, "test"), "broken_engine");

        Assert.Equal(GameMode.Menu, gs.Mode);
        Assert.Contains(gs.ActionLog, e => e.Type == "dungeon_blocked_campaign_ended");
    }

    [Fact]
    public void Entering_A_Dungeon_Under_Lockdown_Says_Why_It_Was_Refused()
    {
        var gs = new GameState(seed: 42);
        gs.Heat.Value = 100;
        Assert.True(gs.Heat.IsLockdown);

        gs.EnterDungeon(new Dungeon(3, 3, "test"), "broken_engine");

        Assert.Equal(GameMode.Menu, gs.Mode);
        Assert.Contains(gs.ActionLog, e => e.Type == "dungeon_blocked_lockdown");
    }

    [Fact]
    public void An_Unblocked_Entry_Still_Enters_The_Dungeon()
    {
        var gs = new GameState(seed: 42);

        gs.EnterDungeon(new Dungeon(3, 3, "test"), "broken_engine");

        Assert.Equal(GameMode.Exploration, gs.Mode);
        Assert.Contains(gs.ActionLog, e => e.Type == "dungeon_entered");
        Assert.DoesNotContain(gs.ActionLog, e => e.Type.StartsWith("dungeon_blocked"));
    }
}
