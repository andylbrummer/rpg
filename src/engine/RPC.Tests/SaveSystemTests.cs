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

        for (int i = 0; i < 6; i++)
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
              "schemaVersion": 3,
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
        var member = gs.Party.Members[3];
        Assert.True(member.Level >= 1, $"Level should be >= 1, was {member.Level}");
        Assert.True(member.Xp >= 0, $"Xp should be >= 0, was {member.Xp}");
        Assert.True(member.CurrentHp >= 0, $"CurrentHp should be >= 0, was {member.CurrentHp}");
    }

    [Fact]
    public void SaveSystem_Load_ClampsRowOutOfRange()
    {
        var json = """
            {
              "schemaVersion": 3,
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
        var member = gs.Party.Members[3];
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

    [Fact]
    public void SaveSystem_RoundTrip_PreservesBranchChoices()
    {
        var gs = new GameState(seed: 42);
        var member = gs.Party.Members[0];
        var updated = member with { BranchChoice = "branch_a", BranchLevel6 = "branch_a6" };
        gs.Party.SetMember(0, updated);

        gs.SaveGame(_testSavePath);
        var gs2 = new GameState(seed: 99);
        var loaded = gs2.LoadGame(_testSavePath);

        Assert.True(loaded);
        var loadedMember = gs2.Party.Members[0];
        Assert.Equal("branch_a", loadedMember.BranchChoice);
        Assert.Equal("branch_a6", loadedMember.BranchLevel6);
    }

    [Fact]
    public void SaveSystem_RoundTrip_RestoresPlayableDungeon()
    {
        var gs = new GameState(seed: 42);
        var dungeon = new Dungeon(3, 3, "test");
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                dungeon.Tiles[x, y] = new Tile(TileType.Floor);
        gs.EnterDungeon(dungeon, "test");
        gs.Player = new Player(new Position(1, 2), Direction.East);
        gs.ExploredTiles.Add("1,1");
        gs.StepsSinceEncounter = 5;

        gs.SaveGame(_testSavePath);

        var gs2 = new GameState(seed: 99);
        var loaded = gs2.LoadGame(_testSavePath, dungeonGenerator: new FixedTileDungeonGenerator(3, 3));

        Assert.True(loaded);
        Assert.NotNull(gs2.CurrentDungeon);
        Assert.Equal("test", gs2.CurrentDungeonType);
        Assert.Equal(1, gs2.Player.Position.X);
        Assert.Equal(2, gs2.Player.Position.Y);
        Assert.True(gs2.ExploredTiles.Contains("1,1"));
        Assert.Equal(5, gs2.StepsSinceEncounter);
        Assert.True(gs2.TryMoveForward());
    }

    [Fact]
    public void SaveRestorer_RestoreOverworld_RestoresNodesTurnsAndCurrencyDirectly()
    {
        // Exercise a feature restore helper directly against a hand-built SaveData,
        // without going through the full save/load round-trip.
        var gs = new GameState(seed: 7);
        var data = new SaveData
        {
            OverworldTurns = 12,
            OverworldCurrentNodeId = "broken_engine",
            PartyGold = 250,
            TitheTokens = 3,
            OverworldNodes = new[]
            {
                new SaveOverworldNode
                {
                    Id = "the_reach",
                    Name = "The Reach",
                    Type = "Town",
                    FactionPresence = new[] { "wardens" }
                },
                new SaveOverworldNode
                {
                    Id = "broken_engine",
                    Name = "Broken Engine",
                    Type = "Dungeon",
                    DungeonTemplateId = "engine_template"
                }
            }
        };

        SaveRestorer.RestoreOverworld(gs, data);

        Assert.Equal(12, gs.Overworld.Turns);
        Assert.Equal("broken_engine", gs.Overworld.CurrentNodeId);
        Assert.Equal(250, gs.PartyGold);
        Assert.Equal(3, gs.TitheTokens);
        Assert.Equal(2, gs.Overworld.Nodes.Count);
        Assert.Equal("engine_template", gs.Overworld.Nodes["broken_engine"].DungeonTemplateId);
        Assert.Contains("wardens", gs.Overworld.Nodes["the_reach"].FactionPresence);
    }
}

/// <summary>Test generator that returns a fully-walkable dungeon of fixed dimensions for the
/// requested type, exercising the <see cref="IDungeonGenerator"/> save/load seam.</summary>
internal sealed class FixedTileDungeonGenerator : IDungeonGenerator
{
    private readonly int _width;
    private readonly int _height;

    public FixedTileDungeonGenerator(int width, int height)
    {
        _width = width;
        _height = height;
    }

    public DungeonGenerationResult Generate(DungeonGenerationRequest request)
    {
        var effectiveSeed = request.Seed ?? 0;
        var d = new Dungeon(_width, _height, request.DungeonType) { Seed = effectiveSeed };
        for (int x = 0; x < _width; x++)
            for (int y = 0; y < _height; y++)
                d.Tiles[x, y] = new Tile(TileType.Floor);
        return new DungeonGenerationResult(
            d, new DungeonGenerationIdentity(request.DungeonType, effectiveSeed, request.ContentHash));
    }

    public Dungeon Generate(string dungeonType, int? seed = null) =>
        Generate(new DungeonGenerationRequest(dungeonType, seed)).Dungeon;
}
