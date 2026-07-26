using System.Text.Json;

namespace RPC.Engine.Save;

public class SaveFileIO
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string SavePath { get; }

    public SaveFileIO(string? savePath = null)
    {
        SavePath = savePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TheReach", "save.json");
    }

    public bool Exists() => File.Exists(SavePath);

    public string? ReadAllText()
    {
        if (!File.Exists(SavePath))
            return null;
        return File.ReadAllText(SavePath);
    }

    /// <summary>
    /// Stage the save to a temp file, fsync it, then rename it into place so a crash mid-write
    /// cannot leave a half-written save behind.
    /// <para>
    /// The temp name must be unique per write. With a fixed "save.json.tmp" two concurrent writers
    /// — two hosts on the same per-user path, or an autosave racing a manual save — collided on it:
    /// the loser threw <see cref="IOException"/> straight out of the save call, and an interleaved
    /// truncate-then-rename could publish a partial file as the real save. A unique temp makes the
    /// rename genuinely atomic, so a reader always sees a whole save and the worst concurrent
    /// outcome is a lost update rather than a destroyed run.
    /// </para>
    /// </summary>
    public void WriteAtomic(string json)
    {
        var dir = Path.GetDirectoryName(SavePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var tmpPath = $"{SavePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tmpPath, json);

            using (var fs = new FileStream(tmpPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                fs.Flush(flushToDisk: true);
            }

            File.Move(tmpPath, SavePath, overwrite: true);
        }
        catch
        {
            // Never strand the staging file: it sits next to the player's save and would otherwise
            // accumulate one orphan per failed write.
            TryDelete(tmpPath);
            throw;
        }

        SweepAbandonedTemps();
    }

    /// <summary>
    /// A write killed between staging and rename (crash, power loss) leaves its uniquely-named temp
    /// behind, so without a sweep they accumulate forever next to the player's save. Only temps
    /// untouched for <see cref="AbandonedTempAge"/> are collected: a real write stages and renames
    /// in milliseconds, so anything that old belongs to a dead writer, never to a live one racing us.
    /// </summary>
    private static readonly TimeSpan AbandonedTempAge = TimeSpan.FromMinutes(1);

    private void SweepAbandonedTemps()
    {
        var dir = Path.GetDirectoryName(SavePath);
        if (string.IsNullOrEmpty(dir)) return;

        try
        {
            var cutoff = DateTime.UtcNow - AbandonedTempAge;
            foreach (var stale in Directory.EnumerateFiles(dir, $"{Path.GetFileName(SavePath)}*.tmp"))
            {
                if (File.GetLastWriteTimeUtc(stale) < cutoff)
                    TryDelete(stale);
            }
        }
        catch (IOException) { /* the save already landed; a failed sweep must not fail the save */ }
        catch (UnauthorizedAccessException) { /* same */ }
    }

    /// <summary>
    /// Set the current save aside under a timestamped name. The timestamp carries milliseconds:
    /// at second granularity two quarantines in the same second collided, and the rename threw out
    /// of the load path — turning an unreadable save into an unhandled crash.
    /// </summary>
    public string Quarantine(string reason)
    {
        if (!File.Exists(SavePath))
            return "";

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff");
        var quarantinePath = $"{SavePath}.quarantine.{timestamp}";
        File.Move(SavePath, quarantinePath, overwrite: true);
        return quarantinePath;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException) { /* the save itself already succeeded or failed; litter is not worth masking that */ }
    }

    public SaveData? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SaveData>(json, Options);
        }
        catch
        {
            return null;
        }
    }

    public string Serialize(SaveData data)
    {
        return JsonSerializer.Serialize(data, Options);
    }
}
