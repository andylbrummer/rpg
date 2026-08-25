namespace RPC.Engine.Content;

public interface IContentCatalog
{
    bool Exists(string path);
    string? GetString(string path);
    IEnumerable<string> EnumerateFiles(string directory, string pattern);
}
