using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Save;
using System.Text.Json;

namespace RPC.Tests;

public class SaveSchemaV2Tests : IDisposable
{
    private readonly string _testSavePath;

    public SaveSchemaV2Tests()
    {
        _testSavePath = Path.Combine(Path.GetTempPath(), $"test_v2_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testSavePath))
            File.Delete(_testSavePath);
        if (File.Exists(_testSavePath + ".tmp"))
            File.Delete(_testSavePath + ".tmp");
    }

    [Fact]
    public void SaveSchemaV2_RoundTrip_YieldsByteIdenticalState()
    {
        var gs = new GameState(seed: 42);
        gs.EnterDungeon(new Dungeon(3, 3, "test"), "test");
        gs.Player = new Player(new Position(1, 2), Direction.East);
        gs.ExploredTiles.Add("1,1");
        gs.ExploredTiles.Add("1,2");
        gs.Reputation["bureau"] = 25;
        gs.Reputation["convocation"] = -10;
        gs.SettingsHash = "abc123";

        gs.SaveGame(_testSavePath);
        var firstBytes = File.ReadAllBytes(_testSavePath);

        var gs2 = new GameState(seed: 99);
        var loaded = gs2.LoadGame(_testSavePath);
        Assert.True(loaded);

        gs2.SaveGame(_testSavePath);
        var secondBytes = File.ReadAllBytes(_testSavePath);

        Assert.Equal(firstBytes.Length, secondBytes.Length);
        Assert.Equal(firstBytes, secondBytes);
    }

    [Fact]
    public void SaveSchemaV2_LoadV1_QuarantinesFileAndReturnsFalse()
    {
        var v1Json = """
            {
              "version": "1",
              "party": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "Kael", "classId": "bonewarden",
                  "level": 1, "xp": 0,
                  "baseStats": {"strength":4,"dexterity":3,"constitution":5,"intelligence":4,"willpower":4},
                  "currentHp": 10, "equipment": {}, "knownAbilities": [], "row": 0
                }
              ],
              "player": { "x": 0, "y": 0, "facing": "North" },
              "exploredTiles": [], "mode": "Menu"
            }
            """;
        File.WriteAllText(_testSavePath, v1Json);
        Assert.True(File.Exists(_testSavePath));

        var sw = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(sw);

        try
        {
            var gs = new GameState(seed: 1);
            var loaded = gs.LoadGame(_testSavePath);

            Assert.False(loaded);
            Assert.False(File.Exists(_testSavePath), "v1 save file should be moved");
            var quarantineFiles = Directory.GetFiles(Path.GetDirectoryName(_testSavePath)!, Path.GetFileName(_testSavePath) + ".quarantine.*");
            Assert.Single(quarantineFiles);

            var logOutput = sw.ToString();
            Assert.Contains("unsupported schema version", logOutput);
            Assert.Contains("Quarantined", logOutput);
        }
        finally
        {
            Console.SetError(originalError);
            foreach (var f in Directory.GetFiles(Path.GetDirectoryName(_testSavePath)!, Path.GetFileName(_testSavePath) + ".quarantine.*"))
                File.Delete(f);
        }
    }

    [Fact]
    public void SaveSchemaV2_AtomicWrite_LeavesIntactSaveOnInterruptedTmp()
    {
        var gs = new GameState(seed: 42);
        gs.EnterDungeon(new Dungeon(3, 3, "test"), "test");
        gs.Player = new Player(new Position(1, 2), Direction.East);
        gs.SaveGame(_testSavePath);

        var originalBytes = File.ReadAllBytes(_testSavePath);

        // Simulate interrupted atomic write: .tmp exists but rename never happened
        var tmpPath = _testSavePath + ".tmp";
        File.WriteAllText(tmpPath, "{\"corrupt");

        // Load should read the original intact save, ignoring .tmp
        var gs2 = new GameState(seed: 99);
        var loaded = gs2.LoadGame(_testSavePath);
        Assert.True(loaded);
        Assert.Equal(gs.Player.Position.X, gs2.Player.Position.X);

        // A fresh save should succeed and remove stale .tmp
        var gs3 = new GameState(seed: 77);
        gs3.EnterDungeon(new Dungeon(5, 5, "other"), "other");
        gs3.Player = new Player(new Position(3, 4), Direction.South);
        gs3.SaveGame(_testSavePath);

        Assert.False(File.Exists(tmpPath), "Stale .tmp should be removed after successful save");
        Assert.True(File.Exists(_testSavePath));

        // After successful save, new state should be loadable
        var gs4 = new GameState(seed: 0);
        var loaded2 = gs4.LoadGame(_testSavePath);
        Assert.True(loaded2);
        Assert.Equal(3, gs4.Player.Position.X);
        Assert.Equal(4, gs4.Player.Position.Y);
    }

    [Fact]
    public void SaveSchemaV2_LoadV1StringVersion_QuarantinesFileAndReturnsFalse()
    {
        var v1Json = """
            {
              "version": "1",
              "party": [],
              "player": { "x": 0, "y": 0, "facing": "North" },
              "exploredTiles": [], "mode": "Menu"
            }
            """;
        File.WriteAllText(_testSavePath, v1Json);

        var sw = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(sw);

        try
        {
            var gs = new GameState(seed: 1);
            var loaded = gs.LoadGame(_testSavePath);

            Assert.False(loaded);
            Assert.False(File.Exists(_testSavePath));
            var quarantineFiles = Directory.GetFiles(Path.GetDirectoryName(_testSavePath)!, Path.GetFileName(_testSavePath) + ".quarantine.*");
            Assert.Single(quarantineFiles);
        }
        finally
        {
            Console.SetError(originalError);
            foreach (var f in Directory.GetFiles(Path.GetDirectoryName(_testSavePath)!, Path.GetFileName(_testSavePath) + ".quarantine.*"))
                File.Delete(f);
        }
    }

    [Fact]
    public void SaveSchemaV2_SixSlotParty_PreservesPositions()
    {
        var gs = new GameState(seed: 42);
        gs.SaveGame(_testSavePath);

        var json = File.ReadAllText(_testSavePath);
        using var doc = JsonDocument.Parse(json);
        var party = doc.RootElement.GetProperty("party");

        Assert.Equal(6, party.GetArrayLength());

        // Default party: 6 members at all slots
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(JsonValueKind.Object, party[i].ValueKind);
        }
    }

    [Fact]
    public void SaveSchemaV2_Reputation_RoundTrips()
    {
        var gs = new GameState(seed: 1);
        gs.Reputation["bureau"] = 50;
        gs.Reputation["convocation"] = -25;
        gs.SaveGame(_testSavePath);

        var gs2 = new GameState(seed: 2);
        var loaded = gs2.LoadGame(_testSavePath);
        Assert.True(loaded);

        Assert.Equal(50, gs2.Reputation["bureau"]);
        Assert.Equal(-25, gs2.Reputation["convocation"]);
    }

    [Fact]
    public void SaveSchemaV2_Reputation_Clamped()
    {
        var json = """
            {
              "schemaVersion": 3,
              "party": [null,null,null,null,null,null],
              "player": { "x": 0, "y": 0, "facing": "North" },
              "exploredTiles": [], "mode": "Menu",
              "reputation": { "bureau": 999, "convocation": -999 },
              "actionLog": []
            }
            """;
        File.WriteAllText(_testSavePath, json);

        var gs = new GameState(seed: 1);
        var loaded = gs.LoadGame(_testSavePath);
        Assert.True(loaded);

        Assert.Equal(100, gs.Reputation["bureau"]);
        Assert.Equal(-100, gs.Reputation["convocation"]);
    }

    [Fact]
    public void SaveSchemaV2_SettingsHash_RoundTrips()
    {
        var gs = new GameState(seed: 1);
        gs.SettingsHash = "kdl-ref-abc";
        gs.SaveGame(_testSavePath);

        var gs2 = new GameState(seed: 2);
        var loaded = gs2.LoadGame(_testSavePath);
        Assert.True(loaded);

        Assert.Equal("kdl-ref-abc", gs2.SettingsHash);
    }
}
