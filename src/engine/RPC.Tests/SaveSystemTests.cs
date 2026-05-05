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
}
