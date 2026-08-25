using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RPC.Engine.LLM;

/// <summary>
/// Caches LLM-generated campaign configs by six-roll hash.
/// Same rolls → cached result, no LLM call.
/// </summary>
public class GenerationCache
{
    private readonly string _cacheDir;
    private readonly Dictionary<string, CachedEntry> _memory = new();

    public GenerationCache(string? cacheDir = null)
    {
        _cacheDir = cacheDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RPC", "llm-cache");
        Directory.CreateDirectory(_cacheDir);
    }

    public string GetKey(int[] rolls, string contentHash)
    {
        var input = $"{contentHash}:{string.Join(",", rolls)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)))[..16];
    }

    /// <summary>Entries older than this are regenerated rather than served.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// Look up a cached generation, treating anything past <see cref="Lifetime"/> as absent.
    /// <para>
    /// Age comes from the cache file's own write time. Reading the file used to stamp the entry as
    /// cached "now", which made every persisted entry immortal — the expiry only ever applied to a
    /// copy that had never left memory, and each read renewed the clock. A campaign generated
    /// against long-superseded content came back forever.
    /// </para>
    /// </summary>
    public bool TryGet(string key, out string json)
    {
        var path = Path.Combine(_cacheDir, $"{key}.json");

        if (_memory.TryGetValue(key, out var entry) && !IsExpired(entry.CachedAt))
        {
            json = entry.Json;
            return true;
        }

        if (File.Exists(path))
        {
            var cachedAt = File.GetLastWriteTimeUtc(path);
            if (IsExpired(cachedAt))
            {
                Evict(key, path);
            }
            else
            {
                try
                {
                    json = File.ReadAllText(path);
                    _memory[key] = new CachedEntry(json, cachedAt);
                    return true;
                }
                catch (IOException ex)
                {
                    // A cache entry is regenerable, so an unreadable one is a miss, not a failure.
                    // It is dropped rather than left to be re-read on every lookup forever.
                    Console.Error.WriteLine($"[LLMCache] Discarding unreadable entry '{key}': {ex.Message}");
                    Evict(key, path);
                }
            }
        }

        json = "";
        return false;
    }

    public void Put(string key, string json)
    {
        _memory[key] = new CachedEntry(json, DateTime.UtcNow);
        // Atomic: a write interrupted partway used to leave a truncated entry that later reads
        // served as if whole, pushing the damage into campaign generation instead of failing here.
        AtomicFile.WriteAllText(Path.Combine(_cacheDir, $"{key}.json"), json);
    }

    private static bool IsExpired(DateTime cachedAt) => DateTime.UtcNow - cachedAt > Lifetime;

    private void Evict(string key, string path)
    {
        _memory.Remove(key);
        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"[LLMCache] Could not remove entry '{key}': {ex.Message}");
        }
    }

    public void Clear()
    {
        _memory.Clear();
        foreach (var file in Directory.GetFiles(_cacheDir, "*.json"))
            File.Delete(file);
    }

    private record CachedEntry(string Json, DateTime CachedAt);
}
