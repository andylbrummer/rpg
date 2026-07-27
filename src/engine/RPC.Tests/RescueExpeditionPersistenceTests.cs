using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

/// <summary>
/// A rescue expedition is the ironman answer to a total party kill: the bench walks back in to
/// recover the fallen party's gear, and reaching the spot where they died resolves it. It was not
/// written to the save. Ironman autosaves on every state-changing command and quitting and
/// resuming is the ordinary way to play it, so a resumed run came back with the rescue party in
/// the dungeon and no expedition — reaching the site did nothing, the equipment was never
/// recovered, and the rescue could neither succeed nor fail for the rest of the run.
/// </summary>
public class RescueExpeditionPersistenceTests : IDisposable
{
    private readonly string _savePath = Path.Combine(
        Path.GetTempPath(), $"reach-rescue-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_savePath)) File.Delete(_savePath);
    }

    private static CharacterState Member(string name)
        => new(Guid.NewGuid(), name, "stillblade", 1, 0,
            new BaseStats(4, 4, 4, 4, 4), 20, Equipment.Empty, Array.Empty<string>(), 0);

    private GameState IronmanInDungeon()
    {
        var gs = new GameState(seed: 11) { SavePath = _savePath, IsIronman = true };
        var dungeon = new Dungeon(6, 6, "crypt");
        for (int x = 0; x < 6; x++)
            for (int y = 0; y < 6; y++)
                dungeon.Tiles[x, y] = new Tile(TileType.Floor);
        gs.CurrentDungeon = dungeon;
        gs.CurrentDungeonType = "crypt";
        gs.Mode = GameMode.Exploration;
        return gs;
    }

    [Fact]
    public void AnActiveRescueExpedition_SurvivesSaveAndLoad()
    {
        var before = IronmanInDungeon();
        before.Player.Position = new Position(4, 3);
        for (int i = 0; i < 3; i++) before.Party.Bench.Add(Member($"Reserve{i}"));

        Assert.True(before.StartRescueExpedition());
        var expected = before.RescueExpedition!;
        before.SaveGame();

        var after = IronmanInDungeon();
        Assert.True(after.LoadGame());

        Assert.NotNull(after.RescueExpedition);
        Assert.True(after.RescueExpedition!.IsActive);
        Assert.Equal(expected.DungeonType, after.RescueExpedition.DungeonType);
        Assert.Equal(expected.TpkLocation, after.RescueExpedition.TpkLocation);
        Assert.Equal(expected.RescuePartyIds, after.RescueExpedition.RescuePartyIds);
    }

    [Fact]
    public void NoExpedition_StaysAbsentAcrossSaveAndLoad()
    {
        var before = IronmanInDungeon();
        before.SaveGame();

        var after = IronmanInDungeon();
        after.RescueExpedition = new RPC.Engine.Combat.RescueExpeditionState { IsActive = true };
        Assert.True(after.LoadGame());

        Assert.Null(after.RescueExpedition);
    }
}
