using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Save;
using RPC.Engine.Save.Migrations;
using RPC.Engine.Character;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Town;

namespace RPC.Tests;

public class SaveMigrationTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _goldenPath;

    public SaveMigrationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"test_migrations_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _goldenPath = Path.Combine(_testDir, "v8-golden.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void Pipeline_CanMigrate_SupportedVersions()
    {
        var pipeline = SaveMigrationPipeline.CreateDefault(8);
        for (int v = 3; v <= 8; v++)
        {
            Assert.True(pipeline.CanMigrate(v), $"Should be able to migrate from v{v}");
        }
    }

    [Fact]
    public void Pipeline_CannotMigrate_UnsupportedVersions()
    {
        var pipeline = SaveMigrationPipeline.CreateDefault(8);
        Assert.False(pipeline.CanMigrate(1));
        Assert.False(pipeline.CanMigrate(2));
        Assert.False(pipeline.CanMigrate(99));
        Assert.False(pipeline.CanMigrate(9));
    }

    [Fact]
    public void V3Minimal_MigratesToV8_LoadsSuccessfully()
    {
        var v3Json = """
            {
              "schemaVersion": 3,
              "party": [null,null,null,null,null,null],
              "player": { "x": 0, "y": 0, "facing": "North" },
              "exploredTiles": [],
              "mode": "Menu",
              "reputation": {},
              "actionLog": []
            }
            """;

        File.WriteAllText(_goldenPath, v3Json);
        var gs = new GameState(seed: 1);
        var loaded = gs.LoadGame(_goldenPath);

        Assert.True(loaded);
        Assert.Equal(GameMode.Menu, gs.Mode);
    }

    [Fact]
    public void V8GoldenFixture_RoundTrips()
    {
        var factionRepo = new FactionContentRepository(FactionContentLoader.LoadAll("../../../../../../content/factions"));
        var gs = new GameState(seed: 42, factionContent: factionRepo);
        gs.EnterDungeon(new Dungeon(3, 3, "test"), "crypt");
        gs.Player = new Player(new Position(1, 2), Direction.East);
        gs.ExploredTiles.Add("1,1");
        gs.Reputation["bureau"] = 25;
        gs.SettingsHash = "test-settings-123";
        gs.ContentHash = "abc123";

        gs.SaveGame(_goldenPath);
        Assert.True(File.Exists(_goldenPath));

        var json = File.ReadAllText(_goldenPath);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(8, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("abc123", doc.RootElement.GetProperty("contentHash").GetString());

        var gs2 = new GameState(seed: 99, factionContent: factionRepo);
        gs2.ContentHash = "abc123";
        var loaded = gs2.LoadGame(_goldenPath);
        Assert.True(loaded);

        Assert.Equal(gs.Player.Position.X, gs2.Player.Position.X);
        Assert.Equal(gs.Player.Position.Y, gs2.Player.Position.Y);
        Assert.Equal(gs.Reputation["bureau"], gs2.Reputation["bureau"]);
        Assert.Equal(gs.SettingsHash, gs2.SettingsHash);
    }

    [Fact]
    public void ContentHashMismatch_LogsWarningButLoads()
    {
        var gs = new GameState(seed: 1);
        gs.ContentHash = "old-hash";
        gs.SaveGame(_goldenPath);

        var gs2 = new GameState(seed: 2);
        gs2.ContentHash = "new-hash";

        var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        try
        {
            var loaded = gs2.LoadGame(_goldenPath);
            Assert.True(loaded);
            Assert.Contains("Content hash mismatch", sw.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Quarantine_UnsupportedVersion_MovesFile()
    {
        var badJson = """
            {
              "schemaVersion": 99,
              "party": [],
              "player": { "x": 0, "y": 0, "facing": "North" },
              "exploredTiles": [],
              "mode": "Menu"
            }
            """;

        File.WriteAllText(_goldenPath, badJson);
        var gs = new GameState(seed: 1);
        var loaded = gs.LoadGame(_goldenPath);

        Assert.False(loaded);
        Assert.False(File.Exists(_goldenPath));
        var quarantineFiles = Directory.GetFiles(_testDir, "*.quarantine.*");
        Assert.Single(quarantineFiles);
    }
}
