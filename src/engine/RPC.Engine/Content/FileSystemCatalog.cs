namespace RPC.Engine.Content;

public class FileSystemCatalog : IContentCatalog
{
    private readonly string _baseDir;

    public string BaseDirectory => _baseDir;

    public FileSystemCatalog(string? baseDir = null)
    {
        _baseDir = baseDir ?? FindContentDir() ?? AppContext.BaseDirectory;
    }

    public bool Exists(string path) => File.Exists(Resolve(path));

    public string? GetString(string path)
    {
        var full = Resolve(path);
        return File.Exists(full) ? File.ReadAllText(full) : null;
    }

    public IEnumerable<string> EnumerateFiles(string directory, string pattern)
    {
        var full = Resolve(directory);
        return Directory.Exists(full) ? Directory.EnumerateFiles(full, pattern) : Enumerable.Empty<string>();
    }

    private string Resolve(string path) => Path.Combine(_baseDir, path);

    private static string? FindContentDir()
    {
        var baseDir = AppContext.BaseDirectory;
        for (int ups = 0; ups <= 8; ups++)
        {
            var parts = new List<string> { baseDir };
            for (int i = 0; i < ups; i++) parts.Add("..");
            parts.Add("content");
            var candidate = Path.GetFullPath(Path.Combine(parts.ToArray()));
            if (Directory.Exists(candidate))
                return candidate;
        }
        return null;
    }
}
