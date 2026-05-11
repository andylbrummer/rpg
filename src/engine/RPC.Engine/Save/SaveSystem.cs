using System.Text.Json;
using RPC.Engine.Character;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Town;

namespace RPC.Engine.Save;

public class SaveData
{
    public int SchemaVersion { get; set; }
    public SavePartyMember?[] Party { get; set; } = new SavePartyMember?[6];
    public SavePlayer Player { get; set; } = new();
    public string? DungeonType { get; set; }
    public string[] ExploredTiles { get; set; } = Array.Empty<string>();
    public string Mode { get; set; } = "Menu";
    public SaveTownState? Town { get; set; }
    public SaveActionLogEntry[] ActionLog { get; set; } = Array.Empty<SaveActionLogEntry>();
    public Dictionary<string, int> Reputation { get; set; } = new();
    public string? Settings { get; set; }
}

public class SaveTownState
{
    public string CurrentTownId { get; set; } = "the_reach";
    public SaveMissionOffer[] AvailableMissions { get; set; } = Array.Empty<SaveMissionOffer>();
    public SaveVendorItem[] VendorStock { get; set; } = Array.Empty<SaveVendorItem>();
    public string[] FactionContacts { get; set; } = Array.Empty<string>();
    public SaveTavernRecruit[] TavernRoster { get; set; } = Array.Empty<SaveTavernRecruit>();
    public string[] ViewedMissions { get; set; } = Array.Empty<string>();
}

public class SaveMissionOffer
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int MinLevel { get; set; }
    public string[] Rewards { get; set; } = Array.Empty<string>();
}

public class SaveVendorItem
{
    public string ItemId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Price { get; set; }
    public int Quantity { get; set; }
}

public class SaveActionLogEntry
{
    public int Turn { get; set; }
    public string Category { get; set; } = "";
    public string Type { get; set; } = "";
    public Dictionary<string, string> Payload { get; set; } = new();
}

public class SaveTavernRecruit
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ClassId { get; set; } = "";
    public int Level { get; set; }
    public BaseStats BaseStats { get; set; }
    public int Cost { get; set; }
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
        var data = BuildSaveData(state);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(data, Options);
        var tmpPath = path + ".tmp";

        File.WriteAllText(tmpPath, json);

        using (var fs = new FileStream(tmpPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            fs.Flush(flushToDisk: true);
        }

        File.Move(tmpPath, path, overwrite: true);
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

            if (data.SchemaVersion != 2)
            {
                Console.Error.WriteLine(
                    $"Save file '{path}' has unsupported schema version {data.SchemaVersion}. Deleting; player starts new game.");
                File.Delete(path);
                return false;
            }

            RestoreParty(state, data);
            RestorePlayer(state, data);
            RestoreExploredTiles(state, data);
            RestoreMode(state, data);
            RestoreDungeonType(state, data);
            RestoreTown(state, data);
            RestoreActionLog(state, data);
            RestoreReputation(state, data);
            RestoreSettings(state, data);

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

    private static SaveData BuildSaveData(GameState state)
    {
        var party = new SavePartyMember?[6];
        for (int i = 0; i < 4; i++)
        {
            var m = state.Party.Members[i];
            if (m.Id != Guid.Empty)
            {
                int slot = i < 2 ? i : i + 1;
                party[slot] = new SavePartyMember
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
                };
            }
        }

        return new SaveData
        {
            SchemaVersion = 2,
            Party = party,
            Player = new SavePlayer
            {
                X = state.Player.Position.X,
                Y = state.Player.Position.Y,
                Facing = state.Player.Facing.ToString()
            },
            DungeonType = state.CurrentDungeonType,
            ExploredTiles = state.ExploredTiles.AsEnumerable().ToArray(),
            Mode = state.Mode.ToString(),
            Town = new SaveTownState
            {
                CurrentTownId = state.Town.CurrentTownId,
                AvailableMissions = state.Town.AvailableMissions
                    .Select(m => new SaveMissionOffer
                    {
                        Id = m.Id,
                        Title = m.Title,
                        Description = m.Description,
                        MinLevel = m.MinLevel,
                        Rewards = m.Rewards
                    }).ToArray(),
                VendorStock = state.Town.VendorStock
                    .Select(v => new SaveVendorItem
                    {
                        ItemId = v.ItemId,
                        Name = v.Name,
                        Price = v.Price,
                        Quantity = v.Quantity
                    }).ToArray(),
                FactionContacts = state.Town.FactionContacts.ToArray(),
                TavernRoster = state.Town.TavernRoster
                    .Select(r => new SaveTavernRecruit
                    {
                        Id = r.Id,
                        Name = r.Name,
                        ClassId = r.ClassId,
                        Level = r.Level,
                        BaseStats = r.BaseStats,
                        Cost = r.Cost
                    }).ToArray(),
                ViewedMissions = state.Town.ViewedMissions.ToArray()
            },
            ActionLog = state.ActionLog.Select(e => new SaveActionLogEntry
            {
                Turn = e.Turn,
                Category = e.Category,
                Type = e.Type,
                Payload = e.Payload
            }).ToArray(),
            Reputation = new Dictionary<string, int>(state.Reputation),
            Settings = state.SettingsHash
        };
    }

    private static void RestoreParty(GameState state, SaveData data)
    {
        for (int i = 0; i < 4; i++)
        {
            int slot = i < 2 ? i : i + 1;
            var party = data.Party ?? Array.Empty<SavePartyMember?>();
            if (slot < party.Length && party[slot] is { } s)
            {
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
    }

    private static void RestorePlayer(GameState state, SaveData data)
    {
        if (Enum.TryParse<Direction>(data.Player.Facing, out var facing))
        {
            state.Player = new Player(
                new Position(data.Player.X, data.Player.Y),
                facing);
        }
    }

    private static void RestoreExploredTiles(GameState state, SaveData data)
    {
        state.ExploredTiles.Clear();
        foreach (var tile in data.ExploredTiles)
            state.ExploredTiles.Add(tile);
    }

    private static void RestoreMode(GameState state, SaveData data)
    {
        if (Enum.TryParse<GameMode>(data.Mode, out var mode))
            state.Mode = mode;
    }

    private static void RestoreTown(GameState state, SaveData data)
    {
        if (data.Town != null)
        {
            state.Town.CurrentTownId = data.Town.CurrentTownId;
            state.Town.AvailableMissions = data.Town.AvailableMissions
                .Select(m => new MissionOffer(m.Id, m.Title, m.Description, m.MinLevel, m.Rewards))
                .ToList();
            state.Town.VendorStock = data.Town.VendorStock
                .Select(v => new VendorItem(v.ItemId, v.Name, v.Price, v.Quantity))
                .ToList();
            state.Town.FactionContacts = data.Town.FactionContacts.ToList();
            state.Town.TavernRoster = data.Town.TavernRoster
                .Select(r => new TavernRecruit(r.Id, r.Name, r.ClassId, r.Level, r.BaseStats, r.Cost))
                .ToList();
            state.Town.ViewedMissions = data.Town.ViewedMissions.ToList();
        }
    }

    private static void RestoreActionLog(GameState state, SaveData data)
    {
        state.RestoreActionLog(
            (data.ActionLog ?? Array.Empty<SaveActionLogEntry>())
            .Select(e => new ActionLogEntry(e.Turn, e.Category, e.Type, e.Payload))
            .ToList());
    }

    private static void RestoreDungeonType(GameState state, SaveData data)
    {
        state.CurrentDungeonType = data.DungeonType;
    }

    private static void RestoreReputation(GameState state, SaveData data)
    {
        state.Reputation.Clear();
        foreach (var kv in data.Reputation)
        {
            state.Reputation[kv.Key] = Math.Clamp(kv.Value, -100, 100);
        }
    }

    private static void RestoreSettings(GameState state, SaveData data)
    {
        state.SettingsHash = data.Settings;
    }
}
