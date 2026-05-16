using RPC.Engine.Content;
using System.Reflection;

namespace RPC.Content;

public class RpkCatalog : IContentCatalog
{
    private readonly ContentPackReader _reader;
    private readonly string _basePath;

    public RpkCatalog(string rpkPath)
    {
        _reader = new ContentPackReader();
        _reader.Load(rpkPath);
        _basePath = "";
    }

    public RpkCatalog(ContentPackReader reader, string basePath = "")
    {
        _reader = reader;
        _basePath = basePath;
    }

    public bool Exists(string path) => _reader.Contains(Normalize(path));

    public string? GetString(string path) => _reader.GetString(Normalize(path));

    public IEnumerable<string> EnumerateFiles(string directory, string pattern)
    {
        var prefix = Normalize(directory).TrimEnd('/') + "/";
        var ext = pattern.StartsWith("*") ? pattern[1..] : pattern;

        foreach (var key in GetAllPaths())
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && key.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                yield return key;
        }
    }

    private string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

    private IEnumerable<string> GetAllPaths()
    {
        var field = typeof(ContentPackReader).GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field?.GetValue(_reader) is Dictionary<string, ReadOnlyMemory<byte>> dict)
        {
            return dict.Keys;
        }
        return Enumerable.Empty<string>();
    }
}
