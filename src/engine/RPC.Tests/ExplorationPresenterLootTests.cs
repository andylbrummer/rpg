using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Models.Dungeons;
using RPC.Host.Web.Presenters;
using Xunit;

namespace RPC.Tests;

public class ExplorationPresenterLootTests
{
    [Fact]
    public void Loot_tile_serializes_hasLoot_true_until_collected()
    {
        var gs = new GameState(seed: 1);
        var d = new Dungeon(5, 5, "t");
        d.Tiles[2, 2] = new Tile(TileType.Floor, RoomId: 0, LootId: "rat_tail");
        gs.CurrentDungeon = d;
        gs.Player.Position = new Position(2, 2);

        var json = JsonSerializer.Serialize(ExplorationPresenter.Present(gs));
        Assert.Contains("\"hasLoot\":true", json);

        gs.Exploration.CollectedLoot.Add("2,2");
        var json2 = JsonSerializer.Serialize(ExplorationPresenter.Present(gs));
        Assert.Contains("\"hasLoot\":false", json2);
    }
}
