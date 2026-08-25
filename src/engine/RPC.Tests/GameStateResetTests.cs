using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Inventory;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

/// <summary>
/// Reset starts a new campaign in place. Anything it forgets to clear is inherited by the next run:
/// characters the player never recruited, components they never gathered, or a mid-flight encounter
/// belonging to a campaign that no longer exists.
/// </summary>
public class GameStateResetTests
{
    /// <summary>
    /// Build a state carrying live session state across every aggregate Reset touches.
    /// </summary>
    private static GameState UsedCampaign()
    {
        var gs = new GameState(seed: 42);
        gs.EnterDungeon(new Dungeon(3, 3, "test"), "broken_engine");

        gs.RescueExpedition = new RescueExpeditionState
        {
            IsActive = true,
            DungeonType = "broken_engine",
            RescuePartyIds = new[] { Guid.NewGuid() },
        };
        gs.RestoreDowntimeState(new[] { gs.Party.Members[0].Id });
        gs.Party.Bench.Add(gs.Party.Members[0]);
        gs.Party.ExpeditionCache = new[] { new ComponentStack("bone_dust", 5) };
        gs.Party.TownStorage = new[] { new ComponentStack("ash_salt", 3) };

        return gs;
    }

    [Fact]
    public void Reset_Clears_A_Rescue_Expedition_From_The_Previous_Campaign()
    {
        var gs = UsedCampaign();

        gs.Reset();

        Assert.Null(gs.RescueExpedition);
    }

    /// <summary>
    /// Downtime is once-per-character-per-campaign. Carrying the completed set into a new run left
    /// the fresh party unable to take downtime they had never used.
    /// </summary>
    [Fact]
    public void Reset_Clears_Completed_Downtime()
    {
        var gs = UsedCampaign();

        gs.Reset();

        Assert.Empty(gs.DowntimeCompleted);
    }

    [Fact]
    public void Reset_Clears_The_Bench_So_A_New_Run_Starts_With_The_Default_Party()
    {
        var gs = UsedCampaign();

        gs.Reset();

        Assert.Empty(gs.Party.Bench);
    }

    /// <summary>
    /// Components are campaign-scoped: carrying stored stacks into a new run hands the player
    /// materials they never gathered.
    /// </summary>
    [Fact]
    public void Reset_Clears_Stored_Components()
    {
        var gs = UsedCampaign();

        gs.Reset();

        Assert.Empty(gs.Party.ExpeditionCache);
        Assert.Empty(gs.Party.TownStorage);
    }

    [Fact]
    public void Reset_Still_Restores_The_Default_Party_And_Starting_Purse()
    {
        var gs = UsedCampaign();
        gs.PartyGold = 12;

        gs.Reset();

        Assert.Equal(500, gs.PartyGold);
        Assert.Equal(GameMode.Menu, gs.Mode);
        Assert.Equal("Kael", gs.Party.Members[0].Name);
        Assert.Equal(1, gs.Party.Members[0].Level);
    }
}
