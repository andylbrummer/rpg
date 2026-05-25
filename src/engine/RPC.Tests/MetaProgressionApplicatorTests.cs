using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Combat;
using RPC.Engine.Save;

namespace RPC.Tests;

public class MetaProgressionApplicatorTests
{
    [Fact]
    public void Apply_BiasesStartingReputationFromFactionPower()
    {
        var state = new GameState();
        var meta = new MetaProgression { FactionPower = { ["bureau"] = 100, ["stillness"] = -40 } };

        MetaProgressionApplicator.Apply(state, meta);

        // 10% carry-over: 100 -> +10, -40 -> -4.
        Assert.Equal(10, state.Reputation["bureau"]);
        Assert.Equal(-4, state.Reputation["stillness"]);
    }

    [Fact]
    public void Apply_ClampsStartingReputationToCap()
    {
        var state = new GameState();
        var meta = new MetaProgression { FactionPower = { ["convocation"] = 5000 } };

        MetaProgressionApplicator.Apply(state, meta);

        Assert.Equal(MetaProgressionApplicator.MaxStartingReputation, state.Reputation["convocation"]);
    }

    [Fact]
    public void Apply_UnlocksConqueredDungeons()
    {
        var state = new GameState();
        var meta = new MetaProgression { ConqueredDungeons = { "the-vault", "underway" } };

        MetaProgressionApplicator.Apply(state, meta);

        Assert.Contains("the-vault", state.Campaign.UnlockedDungeons);
        Assert.Contains("underway", state.Campaign.UnlockedDungeons);
    }

    [Fact]
    public void Apply_RaisesStartingHeatPerCompletedRun()
    {
        var state = new GameState();
        var meta = new MetaProgression { RunsCompleted = 4 };

        MetaProgressionApplicator.Apply(state, meta);

        Assert.Equal(4 * MetaProgressionApplicator.HeatPerRun, state.Heat.Value);
    }

    [Fact]
    public void Apply_CapsStartingHeat()
    {
        var state = new GameState();
        var meta = new MetaProgression { RunsCompleted = 1000 };

        MetaProgressionApplicator.Apply(state, meta);

        Assert.Equal(MetaProgressionApplicator.MaxStartingHeat, state.Heat.Value);
    }

    [Fact]
    public void Apply_EmptyMeta_IsNoOp()
    {
        var state = new GameState();

        MetaProgressionApplicator.Apply(state, new MetaProgression());

        Assert.Equal(0, state.Heat.Value);
        Assert.Empty(state.Campaign.UnlockedDungeons);
        Assert.Equal(0, state.Reputation["bureau"]);
    }

    [Fact]
    public void GenerateOverworld_AppliesMetaAtCampaignStart()
    {
        var state = new GameState();
        // Persistence stays off → no disk IO; bias comes from the in-memory Meta we set here.
        state.Meta = new MetaProgression
        {
            RunsCompleted = 2,
            FactionPower = { ["bureau"] = 60 },
            ConqueredDungeons = { "underway" },
        };

        state.GenerateOverworld(CampaignConfig.Roll(new GameRandom(1)));

        Assert.Equal(6, state.Reputation["bureau"]); // 60 * 0.1
        Assert.Contains("underway", state.Campaign.UnlockedDungeons);
        Assert.True(state.Heat.Value >= 2 * MetaProgressionApplicator.HeatPerRun);
    }

    [Fact]
    public void LoadAndSaveMetaProgression_RoundTripsViaPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rpc_meta_{Guid.NewGuid()}.json");
        try
        {
            var state = new GameState { MetaPath = path };
            state.Meta = new MetaProgression { RunsCompleted = 3, FactionPower = { ["inkblood"] = 12 } };
            state.SaveMetaProgression();

            var reloaded = new GameState { MetaPath = path };
            reloaded.LoadMetaProgression();

            Assert.Equal(3, reloaded.Meta.RunsCompleted);
            Assert.Equal(12, reloaded.Meta.FactionPower["inkblood"]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
