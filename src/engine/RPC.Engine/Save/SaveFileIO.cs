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

    public void WriteAtomic(string json)
    {
        var dir = Path.GetDirectoryName(SavePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var tmpPath = SavePath + ".tmp";
        File.WriteAllText(tmpPath, json);

        using (var fs = new FileStream(tmpPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            fs.Flush(flushToDisk: true);
        }

        File.Move(tmpPath, SavePath, overwrite: true);
    }

    public string Quarantine(string reason)
    {
        if (!File.Exists(SavePath))
            return "";

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmss");
        var quarantinePath = $"{SavePath}.quarantine.{timestamp}";
        File.Move(SavePath, quarantinePath);
        return quarantinePath;
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
