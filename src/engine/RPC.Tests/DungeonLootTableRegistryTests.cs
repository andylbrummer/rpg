using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using Xunit;

namespace RPC.Tests;

public class DungeonLootTableRegistryTests
{
    private const string Json = """
    { "id": "broken_engine", "entries": [
        { "itemId": "scrap_plating", "weight": 3 },
        { "itemId": "engine_tech_manual", "weight": 1 }
    ] }
    """;

    [Fact]
    public void Roll_is_deterministic_for_same_seed()
    {
        var reg = new DungeonLootTableRegistry();
        reg.LoadFromJson("broken_engine", Json);

        var a = reg.Roll("broken_engine", new GameRandom(42));
        var b = reg.Roll("broken_engine", new GameRandom(42));
        Assert.Equal(a, b);
        Assert.NotNull(a);
    }

    [Fact]
    public void Roll_unknown_table_returns_null()
    {
        var reg = new DungeonLootTableRegistry();
        Assert.Null(reg.Roll("nope", new GameRandom(1)));
    }

    [Fact]
    public void Get_returns_loaded_table_with_entries()
    {
        var reg = new DungeonLootTableRegistry();
        reg.LoadFromJson("broken_engine", Json);
        var table = reg.Get("broken_engine");
        Assert.NotNull(table);
        Assert.Equal(2, table!.Entries.Length);
    }
}
