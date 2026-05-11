using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Save;

namespace RPC.Tests;

public class SaveSystemTests : IDisposable
{
    private readonly string _testSavePath;

    public SaveSystemTests()
    {
        _testSavePath = Path.Combine(Path.GetTempPath(), $"test_save_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testSavePath))
            File.Delete(_testSavePath);
        if (File.Exists(_testSavePath + ".tmp"))
            File.Delete(_testSavePath + ".tmp");
    }

    [Fact]
    public void SaveSystem_RoundTrip_PreservesParty()
    {
        var gs = new GameState(seed: 42);
        gs.EnterDungeon(new Dungeon(3, 3, "test"), "test");
        gs.Player = new Player(new Position(1, 2), Direction.East);
        gs.ExploredTiles.Add("1,1");
        gs.ExploredTiles.Add("1,2");

        gs.SaveGame(_testSavePath);
        Assert.True(File.Exists(_testSavePath));

        var gs2 = new GameState(seed: 99);
        var loaded = gs2.LoadGame(_testSavePath);
        Assert.True(loaded);

        Assert.Equal(gs.Mode, gs2.Mode);
        Assert.Equal(gs.Player.Position.X, gs2.Player.Position.X);
        Assert.Equal(gs.Player.Position.Y, gs2.Player.Position.Y);
        Assert.Equal(gs.Player.Facing, gs2.Player.Facing);
        Assert.Equal(gs.ExploredTiles.Count, gs2.ExploredTiles.Count);

        for (int i = 0; i < 4; i++)
        {
            var original = gs.Party.Members[i];
            var loadedMember = gs2.Party.Members[i];
            if (original.Id == Guid.Empty)
            {
                Assert.Equal(Guid.Empty, loadedMember.Id);
                continue;
            }
            Assert.Equal(original.Name, loadedMember.Name);
            Assert.Equal(original.Level, loadedMember.Level);
            Assert.Equal(original.Xp, loadedMember.Xp);
            Assert.Equal(original.CurrentHp, loadedMember.CurrentHp);
        }
    }

    [Fact]
    public void SaveSystem_NoSave_ReturnsFalse()
    {
        var gs = new GameState(seed: 42);
        Assert.False(gs.LoadGame(_testSavePath));
    }

    [Fact]
    public void SaveSystem_Load_ClampsNegativeLevel()
    {
        var json = """
            {
              "schemaVersion": 2,
              "party": [
                null,
                null,
                null,
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "Kael", "classId": "bonewarden",
                  "level": -5, "xp": -100,
                  "baseStats": {"strength":4,"dexterity":3,"constitution":5,"intelligence":4,"willpower":4},
                  "currentHp": -999, "equipment": {}, "knownAbilities": [], "row": 0
                },
                null,
                null
              ],
              "player": { "x": 0, "y": 0, "facing": "North" },
              "exploredTiles": [], "mode": "Menu",
              "reputation": {},
              "actionLog": []
            }
            """;
        File.WriteAllText(_testSavePath, json);

        var gs = new GameState(seed: 1);
        var loaded = gs.LoadGame(_testSavePath);

        Assert.True(loaded);
        var member = gs.Party.Members[2];
        Assert.True(member.Level >= 1, $"Level should be >= 1, was {member.Level}");
        Assert.True(member.Xp >= 0, $"Xp should be >= 0, was {member.Xp}");
        Assert.True(member.CurrentHp >= 0, $"CurrentHp should be >= 0, was {member.CurrentHp}");
    }

    [Fact]
    public void SaveSystem_Load_ClampsRowOutOfRange()
    {
        var json = """
            {
              "schemaVersion": 2,
              "party": [
                null,
                null,
                null,
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "name": "Kael", "classId": "bonewarden",
                  "level": 1, "xp": 0,
                  "baseStats": {"strength":4,"dexterity":3,"constitution":5,"intelligence":4,"willpower":4},
                  "currentHp": 10, "equipment": {}, "knownAbilities": [], "row": 99
                },
                null,
                null
              ],
              "player": { "x": 0, "y": 0, "facing": "North" },
              "exploredTiles": [], "mode": "Menu",
              "reputation": {},
              "actionLog": []
            }
            """;
        File.WriteAllText(_testSavePath, json);

        var gs = new GameState(seed: 1);
        var loaded = gs.LoadGame(_testSavePath);

        Assert.True(loaded);
        var member = gs.Party.Members[2];
        Assert.True(member.Row is 0 or 1, $"Row should be 0 or 1, was {member.Row}");
    }

    [Fact]
    public void SaveSystem_Load_ReturnsFalse_OnVersionMismatch()
    {
        var json = """{"schemaVersion":99,"party":[null,null,null,null,null,null],"player":{"x":0,"y":0,"facing":"North"},"exploredTiles":[],"mode":"Menu","reputation":{},"actionLog":[]}""";
        File.WriteAllText(_testSavePath, json);

        var gs = new GameState(seed: 1);
        var loaded = gs.LoadGame(_testSavePath);

        Assert.False(loaded);
    }

    [Fact]
    public void ExploredTiles_DoesNotExceedCap()
    {
        var gs = new GameState(seed: 1);
        for (int x = 0; x < 100; x++)
            for (int y = 0; y < 50; y++)
                gs.ExploredTiles.Add($"{x},{y}");

        Assert.True(gs.ExploredTiles.Count <= 4096, $"Expected <= 4096 tiles, got {gs.ExploredTiles.Count}");
    }
}
