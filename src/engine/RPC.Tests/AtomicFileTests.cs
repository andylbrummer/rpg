using RPC.Engine;

namespace RPC.Tests;

/// <summary>
/// Contract tests for the durable whole-file replace shared by the save, the meta-progression, and
/// the analytics aggregates. These are deterministic on purpose: the defects they pin were found
/// through a race, but a stress test only reproduces them some of the time, so each invariant is
/// asserted directly rather than by hammering the file and hoping.
/// </summary>
public class AtomicFileTests : IDisposable
{
    private readonly string _path;

    public AtomicFileTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"rpc_atomic_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        foreach (var f in Directory.GetFiles(Path.GetDirectoryName(_path)!, $"{Path.GetFileName(_path)}*"))
        {
            if (Directory.Exists(f)) Directory.Delete(f, recursive: true);
            else File.Delete(f);
        }
        if (Directory.Exists(_path)) Directory.Delete(_path, recursive: true);
    }

    [Fact]
    public void WriteAllText_Replaces_The_File_And_Creates_Its_Directory()
    {
        var nested = Path.Combine(Path.GetDirectoryName(_path)!, $"rpc_atomic_dir_{Guid.NewGuid():N}", "state.json");
        try
        {
            AtomicFile.WriteAllText(nested, "first");
            Assert.Equal("first", File.ReadAllText(nested));

            AtomicFile.WriteAllText(nested, "second");
            Assert.Equal("second", File.ReadAllText(nested));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(nested)!, recursive: true);
        }
    }

    /// <summary>
    /// Staging used to go through one fixed "&lt;path&gt;.tmp" shared by every writer, so two of them
    /// collided on it: the loser threw out of the write, and an interleaved truncate-then-rename
    /// could publish a partial file. Occupying that legacy name proves the staging name is no longer
    /// derived from the target alone — deterministically, where a concurrency stress test only
    /// catches it sometimes.
    /// </summary>
    [Fact]
    public void WriteAllText_Does_Not_Stage_Through_A_Name_Another_Writer_Could_Hold()
    {
        var legacyStagingName = $"{_path}.tmp";
        Directory.CreateDirectory(legacyStagingName); // an occupied staging name a write cannot use

        AtomicFile.WriteAllText(_path, "payload");

        Assert.Equal("payload", File.ReadAllText(_path));
    }

    [Fact]
    public void WriteAllText_Leaves_No_Staging_File_Behind()
    {
        AtomicFile.WriteAllText(_path, "payload");

        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(_path)!, $"{Path.GetFileName(_path)}*.tmp"));
    }

    /// <summary>
    /// A writer killed between staging and rename leaves its temp behind. Uniquely-named temps
    /// would otherwise pile up forever next to the player's file.
    /// </summary>
    [Fact]
    public void WriteAllText_Collects_A_Temp_Abandoned_By_A_Dead_Writer()
    {
        var abandoned = $"{_path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(abandoned, "half-written");
        File.SetLastWriteTimeUtc(abandoned, DateTime.UtcNow.AddHours(-1));

        AtomicFile.WriteAllText(_path, "payload");

        Assert.False(File.Exists(abandoned));
    }

    /// <summary>
    /// A temp that was touched moments ago may belong to a writer that is mid-write right now.
    /// Collecting it would delete the file out from under that writer's rename.
    /// </summary>
    [Fact]
    public void WriteAllText_Leaves_A_Temp_That_A_Live_Writer_Could_Still_Be_Using()
    {
        var inFlight = $"{_path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(inFlight, "staged by someone else, right now");

        AtomicFile.WriteAllText(_path, "payload");

        Assert.True(File.Exists(inFlight), "a temp written moments ago belongs to a live writer");
        File.Delete(inFlight);
    }

    /// <summary>
    /// Quarantine names used to carry only whole seconds, so two quarantines inside the same second
    /// collided on the destination and the rename threw — turning an unreadable file into an
    /// unhandled crash on the read path.
    /// </summary>
    [Fact]
    public void Quarantine_Names_Do_Not_Collide_Within_The_Same_Second()
    {
        File.WriteAllText(_path, "first");
        var first = AtomicFile.Quarantine(_path, "corrupt");

        File.WriteAllText(_path, "second");
        var second = AtomicFile.Quarantine(_path, "corrupt");

        Assert.NotEqual(first, second);
        Assert.Equal("first", File.ReadAllText(first));
        Assert.Equal("second", File.ReadAllText(second));
        Assert.False(File.Exists(_path));
    }

    [Fact]
    public void Quarantine_Reports_Nothing_When_There_Is_No_File_To_Set_Aside()
    {
        Assert.Equal("", AtomicFile.Quarantine(_path, "corrupt"));
    }
}
