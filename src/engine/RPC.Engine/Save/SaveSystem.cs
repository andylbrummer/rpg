using System.Text.Json;
using System.Text.Json.Serialization;
using RPC.Engine.Character;
using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Save;

public class SaveData
{
    public string Version { get; set; } = "1";
    public SavePartyMember[] Party { get; set; } = Array.Empty<SavePartyMember>();
    public SavePlayer Player { get; set; } = new();
    public string? CurrentDungeonType { get; set; }
    public string[] ExploredTiles { get; set; } = Array.Empty<string>();
    public string Mode { get; set; } = "Menu";
}

public class SavePartyMember
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string ClassId { get; set; } = "";
    public int Level { get; set; }
    public int Xp { get; set; }
    public BaseStats BaseStats { get; set; }
    public int CurrentHp { get; set; }
    public Equipment Equipment { get; set; }
    public string[] KnownAbilities { get; set; } = Array.Empty<string>();
    public int Row { get; set; }
}

public class SavePlayer
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Facing { get; set; } = "North";
}

public static class SaveSystem
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string SavePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TheReach", "save.json");

    public static void Save(GameState state, string? path = null)
    {
        path ??= SavePath;
        var data = new SaveData
        {
            Party = state.Party.Members
                .Where(m => m.Id != Guid.Empty)
                .Select(m => new SavePartyMember
                {
                    Id = m.Id,
                    Name = m.Name,
                    ClassId = m.ClassId,
                    Level = m.Level,
                    Xp = m.Xp,
                    BaseStats = m.BaseStats,
                    CurrentHp = m.CurrentHp,
                    Equipment = m.Equipment,
                    KnownAbilities = m.KnownAbilities,
                    Row = m.Row
                }).ToArray(),
            Player = new SavePlayer
            {
                X = state.Player.Position.X,
                Y = state.Player.Position.Y,
                Facing = state.Player.Facing.ToString()
            },
            CurrentDungeonType = state.CurrentDungeonType,
            ExploredTiles = state.ExploredTiles.ToArray(),
            Mode = state.Mode.ToString()
        };

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(data, Options);
        File.WriteAllText(path, json);
    }

    public static bool Load(GameState state, string? path = null)
    {
        path ??= SavePath;
        if (!File.Exists(path))
            return false;

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<SaveData>(json, Options);
            if (data == null) return false;

            if (data.Version != "1")
            {
                Console.Error.WriteLine($"Save version {data.Version} not supported (expected 1)");
                return false;
            }

            // Restore party with clamped values
            for (int i = 0; i < 4; i++)
            {
                if (i < data.Party.Length)
                {
                    var s = data.Party[i];
                    var level = Math.Max(1, s.Level);
                    var xp = Math.Max(0, s.Xp);
                    var hp = Math.Max(0, s.CurrentHp);
                    var row = Math.Clamp(s.Row, 0, 1);
                    state.Party.SetMember(i, new CharacterState(
                        s.Id, s.Name, s.ClassId, level, xp,
                        s.BaseStats, hp, s.Equipment,
                        s.KnownAbilities, row));
                }
                else
                {
                    state.Party.SetMember(i, default);
                }
            }

            // Restore player
            if (Enum.TryParse<Direction>(data.Player.Facing, out var facing))
            {
                state.Player = new Player(
                    new Position(data.Player.X, data.Player.Y),
                    facing);
            }

            // Restore explored tiles
            state.ExploredTiles.Clear();
            foreach (var tile in data.ExploredTiles)
                state.ExploredTiles.Add(tile);

            // Restore mode
            if (Enum.TryParse<GameMode>(data.Mode, out var mode))
                state.Mode = mode;

            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load save: {ex.Message}");
            return false;
        }
    }

    public static bool HasSave(string? path = null)
    {
        return File.Exists(path ?? SavePath);
    }
}
