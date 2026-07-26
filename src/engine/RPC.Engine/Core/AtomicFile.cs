namespace RPC.Engine;

/// <summary>
/// Durable whole-file replacement for the per-user state files the game owns — the save, the
/// cross-campaign meta-progression, the analytics aggregates. Each of these lives at a single
/// well-known path that more than one writer can reach (two hosts, an autosave racing a manual
/// save), and each one losing its contents costs the player something they cannot get back.
/// <para>
/// The write stages to a per-write unique temp, fsyncs it, then renames it into place. Uniqueness
/// is the load-bearing part: a fixed "<c>x.tmp</c>" let two writers collide on the staging name,
/// so the loser failed outright and an interleaved truncate-then-rename could publish a partial
/// file as the real one. With a unique temp the rename is atomic — a reader always observes a
/// whole file, and the worst concurrent outcome is a lost update rather than destroyed state.
/// </para>
/// </summary>
public static class AtomicFile
{
    /// <summary>
    /// A write killed between staging and rename (crash, power loss) leaves its temp behind, so
    /// without a sweep they accumulate forever beside the real file. Only temps untouched for this
    /// long are collected: a real write stages and renames in milliseconds, so anything older
    /// belongs to a dead writer and never to a live one racing us.
    /// </summary>
    private static readonly TimeSpan AbandonedTempAge = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Replace <paramref name="path"/> with <paramref name="contents"/>, creating the containing
    /// directory if needed. Throws whatever the underlying write throws — callers decide whether a
    /// failure to persist is fatal — but never leaves its own staging file behind.
    /// </summary>
    public static void WriteAllText(string path, string contents)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmpPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tmpPath, contents);

            using (var fs = new FileStream(tmpPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                fs.Flush(flushToDisk: true);
            }

            File.Move(tmpPath, path, overwrite: true);
        }
        catch
        {
            TryDelete(tmpPath);
            throw;
        }

        SweepAbandonedTemps(path);
    }

    /// <summary>
    /// Move an unreadable file aside under a timestamped name and return that name, or "" when
    /// there was nothing to set aside. The timestamp carries milliseconds: at second granularity
    /// two quarantines in the same second collided on the destination and the rename threw out of
    /// the read path, turning an unreadable file into an unhandled crash.
    /// </summary>
    public static string Quarantine(string path, string label)
    {
        if (!File.Exists(path)) return "";

        var quarantinePath = $"{path}.{label}.{DateTime.UtcNow:yyyyMMddTHHmmssfff}";
        File.Move(path, quarantinePath, overwrite: true);
        return quarantinePath;
    }

    private static void SweepAbandonedTemps(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir)) return;

        try
        {
            var cutoff = DateTime.UtcNow - AbandonedTempAge;
            foreach (var stale in Directory.EnumerateFiles(dir, $"{Path.GetFileName(path)}*.tmp"))
            {
                if (File.GetLastWriteTimeUtc(stale) < cutoff)
                    TryDelete(stale);
            }
        }
        catch (IOException) { /* the write already landed; a failed sweep must not fail it */ }
        catch (UnauthorizedAccessException) { /* same */ }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException) { /* litter is not worth masking the outcome of the write itself */ }
        catch (UnauthorizedAccessException) { /* same */ }
    }
}
