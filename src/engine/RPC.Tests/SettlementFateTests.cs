using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Combat;

namespace RPC.Tests;

public class SettlementFateTests
{
    private static (GameState state, CampaignService svc) Make(int seed = 42)
        => (new GameState(seed: seed), new CampaignService(null));

    [Fact]
    public void Normalize_MapsLegacyAndUnknownValues()
    {
        Assert.Equal(SettlementFate.Lost, SettlementFate.Normalize("destroyed"));
        Assert.Equal(SettlementFate.Contested, SettlementFate.Normalize("changed"));
        Assert.Equal(SettlementFate.Contested, SettlementFate.Normalize(null));
        Assert.Equal(SettlementFate.Contested, SettlementFate.Normalize("nonsense"));
        Assert.Equal(SettlementFate.Saved, SettlementFate.Normalize("SAVED"));
    }

    [Fact]
    public void RegisterSettlement_AddsContested_AndIsIdempotent()
    {
        var (state, svc) = Make();
        svc.RegisterSettlement(state, "the_reach");
        Assert.Equal(SettlementFate.Contested, state.WorldState.Settlements["the_reach"]);

        // Re-registering must not overwrite an existing fate.
        svc.ChooseSettlementFate(state, "the_reach", SettlementFate.Saved);
        svc.RegisterSettlement(state, "the_reach");
        Assert.Equal(SettlementFate.Saved, state.WorldState.Settlements["the_reach"]);
    }

    [Fact]
    public void ChooseSettlementFate_SetsFate_AndLogsPreviousAndSource()
    {
        var (state, svc) = Make();
        svc.RegisterSettlement(state, "the_reach");
        svc.ChooseSettlementFate(state, "the_reach", SettlementFate.Saved);

        Assert.Equal(SettlementFate.Saved, state.WorldState.Settlements["the_reach"]);
        var entry = state.ActionLog.Last(e => e.Type == "settlement_fate_chosen");
        Assert.Equal("the_reach", entry.Payload["settlementId"]);
        Assert.Equal(SettlementFate.Saved, entry.Payload["fate"]);
        Assert.Equal(SettlementFate.Contested, entry.Payload["previousFate"]);
        Assert.Equal("player_choice", entry.Payload["source"]);
    }

    [Fact]
    public void RollSettlementFate_ZeroPressure_AlwaysSaved()
    {
        var (state, svc) = Make();
        state.Heat.Value = 0; // pressure 0 -> roll never <= 0 -> Saved
        svc.RegisterSettlement(state, "the_reach");

        var fate = svc.RollSettlementFate(state, "the_reach", new GameRandom(1));
        Assert.Equal(SettlementFate.Saved, fate);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(99)]
    public void RollSettlementFate_MaxPressure_NeverSaved(int seed)
    {
        var (state, svc) = Make();
        state.Heat.Value = 100; // pressure 100 -> roll always <= 100 -> Lost or Abandoned
        svc.RegisterSettlement(state, "town");

        var fate = svc.RollSettlementFate(state, "town", new GameRandom(seed));
        Assert.NotEqual(SettlementFate.Saved, fate);
        Assert.True(SettlementFate.IsTerminal(fate));
    }

    [Fact]
    public void RollSettlementFate_DoesNotOverwriteTerminal()
    {
        var (state, svc) = Make();
        state.Heat.Value = 100;
        svc.ChooseSettlementFate(state, "the_reach", SettlementFate.Saved);

        var fate = svc.RollSettlementFate(state, "the_reach", new GameRandom(1));
        Assert.Equal(SettlementFate.Saved, fate);
        Assert.Equal(SettlementFate.Saved, state.WorldState.Settlements["the_reach"]);
    }

    [Fact]
    public void RollPendingSettlementFates_SeedsTownsAndResolvesContested()
    {
        var (state, svc) = Make();
        state.Heat.Value = 0; // contested towns resolve to Saved
        // A player-locked settlement that must survive the roll untouched.
        svc.ChooseSettlementFate(state, "haven", SettlementFate.Abandoned);

        var rolled = svc.RollPendingSettlementFates(state, new GameRandom(3));

        // Default overworld has one Town node (the_reach); haven is already terminal.
        Assert.Equal(SettlementFate.Saved, state.WorldState.Settlements["the_reach"]);
        Assert.Equal(SettlementFate.Abandoned, state.WorldState.Settlements["haven"]);
        Assert.Equal(1, rolled);
        // Dungeon nodes are not tracked as settlements.
        Assert.False(state.WorldState.Settlements.ContainsKey("broken_engine"));
    }

    [Fact]
    public void GetSettlementFate_NormalizesStoredValue()
    {
        var (state, svc) = Make();
        state.WorldState.Settlements["ruin"] = "destroyed"; // legacy value
        Assert.Equal(SettlementFate.Lost, svc.GetSettlementFate(state, "ruin"));
        Assert.Equal(SettlementFate.Contested, svc.GetSettlementFate(state, "unknown_place"));
    }

    [Fact]
    public void GetSettlementFateCounts_AndByFate_BucketCorrectly()
    {
        var (state, svc) = Make();
        svc.ChooseSettlementFate(state, "a", SettlementFate.Saved);
        svc.ChooseSettlementFate(state, "b", SettlementFate.Saved);
        svc.ChooseSettlementFate(state, "c", SettlementFate.Lost);
        state.WorldState.Settlements["d"] = "destroyed"; // normalizes to Lost
        svc.RegisterSettlement(state, "e");              // contested

        var counts = svc.GetSettlementFateCounts(state);
        Assert.Equal(2, counts[SettlementFate.Saved]);
        Assert.Equal(2, counts[SettlementFate.Lost]);
        Assert.Equal(0, counts[SettlementFate.Abandoned]);
        Assert.Equal(1, counts[SettlementFate.Contested]);

        var lost = svc.GetSettlementsByFate(state, SettlementFate.Lost);
        Assert.Equal(2, lost.Count);
        Assert.Contains("c", lost);
        Assert.Contains("d", lost);
    }

    [Fact]
    public void RollPendingSettlementFates_FiresOnCampaignEnd()
    {
        var state = new GameState(seed: 42);
        state.Heat.Value = 0;
        state.Overworld.Turns = 34;

        state.IncrementTurns(1); // crosses turn 35 -> campaign end

        Assert.True(state.CampaignEnded);
        Assert.True(SettlementFate.IsTerminal(state.WorldState.Settlements["the_reach"]));
    }

    [Fact]
    public void Epilogue_IncludesSettlementFateCounts()
    {
        var state = new GameState();
        state.Campaign.CampaignConfig = new CampaignConfig
        {
            Mastermind = "inkblood",
            Patron = "bureau",
            Scheme = SchemeType.TheVault,
            FactionTimelines = new Dictionary<string, FactionTimeline>(),
            NpcCasting = new Dictionary<string, string>()
        };
        state.WorldState.Settlements["a"] = SettlementFate.Saved;
        state.WorldState.Settlements["b"] = SettlementFate.Lost;

        var text = EpilogueGenerator.Generate(state);
        Assert.Contains("1 saved", text);
        Assert.Contains("1 lost", text);
    }
}
