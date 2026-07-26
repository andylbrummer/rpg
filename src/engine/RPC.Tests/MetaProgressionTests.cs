using System.Collections.Generic;
using RPC.Engine;
using RPC.Engine.Save;

namespace RPC.Tests;

public class MetaProgressionTests
{
    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"rpc_meta_{Guid.NewGuid():N}.json");

    [Fact]
    public void SaveLoad_RoundTrips()
    {
        var path = TempPath();
        var meta = new MetaProgression
        {
            RunsCompleted = 3,
            ConqueredDungeons = new HashSet<string> { "crypt", "sewers" },
            FactionPower = new Dictionary<string, int> { ["bureau"] = 12, ["inkblood"] = -4 },
        };

        MetaProgressionStore.Save(meta, path);
        var loaded = MetaProgressionStore.Load(path);

        Assert.Equal(3, loaded.RunsCompleted);
        Assert.Contains("crypt", loaded.ConqueredDungeons);
        Assert.Contains("sewers", loaded.ConqueredDungeons);
        Assert.Equal(12, loaded.FactionPower["bureau"]);
        Assert.Equal(-4, loaded.FactionPower["inkblood"]);
        Assert.Equal(MetaProgression.CurrentSchemaVersion, loaded.SchemaVersion);

        File.Delete(path);
    }

    [Fact]
    public void Load_MissingFile_ReturnsFreshMeta()
    {
        var loaded = MetaProgressionStore.Load(TempPath());
        Assert.Equal(0, loaded.RunsCompleted);
        Assert.Empty(loaded.ConqueredDungeons);
    }

    /// <summary>
    /// An unreadable meta file used to be swallowed into a fresh instance, which the next save then
    /// wrote over — silently erasing every run the player had completed, and indistinguishable from
    /// a first launch. It must be preserved under a quarantine name instead.
    /// </summary>
    [Fact]
    public void Load_UnreadableFile_IsSetAsideInsteadOfSilentlyDiscarded()
    {
        var path = TempPath();
        File.WriteAllText(path, "{\"runsCompleted\": 41, truncated");

        var loaded = MetaProgressionStore.Load(path);

        Assert.Equal(0, loaded.RunsCompleted);
        Assert.False(File.Exists(path), "the unreadable file should have been moved aside, not left to be overwritten");

        var quarantined = Directory.GetFiles(Path.GetDirectoryName(path)!, $"{Path.GetFileName(path)}.corrupt.*");
        Assert.Single(quarantined);
        Assert.Contains("41", File.ReadAllText(quarantined[0]));

        File.Delete(quarantined[0]);
    }

    /// <summary>
    /// The meta file sits at one well-known per-user path, so two hosts can write it at once. A
    /// non-atomic whole-file write let one truncate the other's; every reader after that saw a
    /// corrupt file and started over.
    /// </summary>
    [Fact]
    public async Task Concurrent_Saves_Never_Leave_A_Partial_File_Behind()
    {
        var path = TempPath();
        var unreadable = 0;
        var stop = false;

        var reader = Task.Run(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                if (!File.Exists(path)) continue;
                try
                {
                    if (MetaProgressionStore.Load(path).RunsCompleted == 0)
                        Interlocked.Increment(ref unreadable);
                }
                catch (IOException) { /* the rename raced this open; not a partial-file defect */ }
            }
        });

        Parallel.For(0, 8, i =>
        {
            var meta = new MetaProgression { RunsCompleted = i + 1 };
            for (int n = 0; n < 20; n++)
                MetaProgressionStore.Save(meta, path);
        });

        Volatile.Write(ref stop, true);
        await reader;

        Assert.Equal(0, unreadable);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, $"{Path.GetFileName(path)}.corrupt.*"));

        File.Delete(path);
    }

    [Fact]
    public void RecordCampaignEnd_FoldsRunIntoMeta()
    {
        var gs = new GameState(seed: 1);
        gs.ActionLog.Add(new ActionLogEntry(1, 1, "dungeon", "dungeon_completed",
            new Dictionary<string, string> { { "dungeonType", "crypt" } }));
        gs.ActionLog.Add(new ActionLogEntry(2, 1, "dungeon", "dungeon_completed",
            new Dictionary<string, string> { { "dungeonType", "boneyard" } }));
        gs.Reputation["bureau"] = 8;

        var meta = MetaProgressionStore.RecordCampaignEnd(new MetaProgression(), gs);

        Assert.Equal(1, meta.RunsCompleted);
        Assert.Contains("crypt", meta.ConqueredDungeons);
        Assert.Contains("boneyard", meta.ConqueredDungeons);
        Assert.Equal(8, meta.FactionPower["bureau"]);
    }

    [Fact]
    public void RecordCampaignEnd_AccumulatesAcrossRuns()
    {
        var meta = new MetaProgression();

        var run1 = new GameState(seed: 1);
        run1.Reputation["bureau"] = 5;
        MetaProgressionStore.RecordCampaignEnd(meta, run1);

        var run2 = new GameState(seed: 2);
        run2.Reputation["bureau"] = 7;
        MetaProgressionStore.RecordCampaignEnd(meta, run2);

        Assert.Equal(2, meta.RunsCompleted);
        Assert.Equal(12, meta.FactionPower["bureau"]); // 5 + 7 accumulated
    }
}
