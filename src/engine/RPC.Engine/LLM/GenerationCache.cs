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

    public bool TryGet(string key, out string json)
    {
        if (_memory.TryGetValue(key, out var entry) && !entry.IsExpired)
        {
            json = entry.Json;
            return true;
        }

        var path = Path.Combine(_cacheDir, $"{key}.json");
        if (File.Exists(path))
        {
            json = File.ReadAllText(path);
            _memory[key] = new CachedEntry(json, DateTime.UtcNow);
            return true;
        }

        json = "";
        return false;
    }

    public void Put(string key, string json)
    {
        _memory[key] = new CachedEntry(json, DateTime.UtcNow);
        var path = Path.Combine(_cacheDir, $"{key}.json");
        File.WriteAllText(path, json);
    }

    public void Clear()
    {
        _memory.Clear();
        foreach (var file in Directory.GetFiles(_cacheDir, "*.json"))
            File.Delete(file);
    }

    private record CachedEntry(string Json, DateTime CachedAt)
    {
        public bool IsExpired => DateTime.UtcNow - CachedAt > TimeSpan.FromDays(30);
    }
}
