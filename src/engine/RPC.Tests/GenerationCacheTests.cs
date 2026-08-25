using RPC.Engine.LLM;

namespace RPC.Tests;

public class GenerationCacheTests : IDisposable
{
    private readonly string _cacheDir;

    public GenerationCacheTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), $"rpc_gencache_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    [Fact]
    public void Put_Then_TryGet_Returns_The_Entry()
    {
        var cache = new GenerationCache(_cacheDir);
        cache.Put("key", "{\"patron\":\"bureau\"}");

        Assert.True(cache.TryGet("key", out var json));
        Assert.Equal("{\"patron\":\"bureau\"}", json);
    }

    [Fact]
    public void TryGet_Reports_A_Miss_For_An_Unknown_Key()
    {
        Assert.False(new GenerationCache(_cacheDir).TryGet("absent", out var json));
        Assert.Equal("", json);
    }

    /// <summary>
    /// A cached entry survives the process, so a second run must see what the first one wrote.
    /// </summary>
    [Fact]
    public void An_Entry_Survives_Into_A_New_Cache_Instance()
    {
        new GenerationCache(_cacheDir).Put("key", "payload");

        Assert.True(new GenerationCache(_cacheDir).TryGet("key", out var json));
        Assert.Equal("payload", json);
    }

    /// <summary>
    /// Entries expire after 30 days. The expiry only ever applied to the in-memory copy: a disk read
    /// stamped the entry as cached "now", so anything that had been written to disk was immortal and
    /// every read renewed it. A campaign generated against long-superseded content came back forever.
    /// </summary>
    [Fact]
    public void An_Entry_Older_Than_The_Expiry_Is_Not_Served()
    {
        var cache = new GenerationCache(_cacheDir);
        cache.Put("key", "stale");
        AgeEntry("key", TimeSpan.FromDays(31));

        Assert.False(new GenerationCache(_cacheDir).TryGet("key", out _));
    }

    [Fact]
    public void An_Entry_Within_The_Expiry_Is_Still_Served()
    {
        var cache = new GenerationCache(_cacheDir);
        cache.Put("key", "fresh");
        AgeEntry("key", TimeSpan.FromDays(29));

        Assert.True(new GenerationCache(_cacheDir).TryGet("key", out var json));
        Assert.Equal("fresh", json);
    }

    /// <summary>
    /// Reading an entry must not renew it. Stamping the read time as the cached time meant a key
    /// touched at least once a month never aged out at all.
    /// </summary>
    [Fact]
    public void Reading_An_Entry_Does_Not_Renew_Its_Age()
    {
        var cache = new GenerationCache(_cacheDir);
        cache.Put("key", "payload");
        AgeEntry("key", TimeSpan.FromDays(29));

        Assert.True(cache.TryGet("key", out _));
        AgeEntry("key", TimeSpan.FromDays(31));

        Assert.False(new GenerationCache(_cacheDir).TryGet("key", out _));
    }

    /// <summary>
    /// A cache entry is regenerable, so anything unusable sitting at its path is a miss rather than
    /// an exception thrown out of campaign generation.
    /// </summary>
    [Fact]
    public void Something_Unusable_At_An_Entry_Path_Is_A_Miss_Not_A_Failure()
    {
        Directory.CreateDirectory(_cacheDir);
        Directory.CreateDirectory(Path.Combine(_cacheDir, "key.json")); // a directory where a file belongs

        Assert.False(new GenerationCache(_cacheDir).TryGet("key", out _));
    }

    /// <summary>An expired entry is removed, not merely skipped, so it stops being re-checked.</summary>
    [Fact]
    public void An_Expired_Entry_Is_Removed_From_Disk()
    {
        new GenerationCache(_cacheDir).Put("key", "stale");
        AgeEntry("key", TimeSpan.FromDays(31));

        // A cold instance, as a later run would be: nothing in memory to shadow the aged file.
        new GenerationCache(_cacheDir).TryGet("key", out _);

        Assert.False(File.Exists(Path.Combine(_cacheDir, "key.json")));
    }

    [Fact]
    public void Clear_Drops_Both_The_Memory_And_The_Disk_Copy()
    {
        var cache = new GenerationCache(_cacheDir);
        cache.Put("key", "payload");

        cache.Clear();

        Assert.False(cache.TryGet("key", out _));
        Assert.False(new GenerationCache(_cacheDir).TryGet("key", out _));
    }

    private void AgeEntry(string key, TimeSpan age)
        => File.SetLastWriteTimeUtc(Path.Combine(_cacheDir, $"{key}.json"), DateTime.UtcNow - age);
}
