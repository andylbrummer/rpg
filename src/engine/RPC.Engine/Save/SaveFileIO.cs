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

    /// <summary>Durably replace the save file. See <see cref="AtomicFile.WriteAllText"/>.</summary>
    public void WriteAtomic(string json) => AtomicFile.WriteAllText(SavePath, json);

    /// <summary>
    /// Set an unloadable save aside under a timestamped name so it is preserved rather than
    /// overwritten. Callers report why; this only moves the file.
    /// </summary>
    public string Quarantine() => AtomicFile.Quarantine(SavePath, "quarantine");

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
