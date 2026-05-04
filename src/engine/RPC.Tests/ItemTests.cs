using System.Text.Json;
using RPC.Engine.Character;
using RPC.Engine.Content;

namespace RPC.Tests;

public class ItemTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true
    };

    [Theory]
    [InlineData("weapons")]
    [InlineData("armor")]
    [InlineData("consumables")]
    [InlineData("components")]
    public void ItemJson_LoadsWithoutErrors(string category)
    {
        var path = $"../../../../../../content/items/{category}.json";
        Assert.True(File.Exists(path));

        var json = File.ReadAllText(path);
        var items = JsonSerializer.Deserialize<ItemDef[]>(json, JsonOptions);

        Assert.NotNull(items);
        Assert.NotEmpty(items);
        Assert.All(items, item =>
        {
            Assert.False(string.IsNullOrEmpty(item.Id));
            Assert.False(string.IsNullOrEmpty(item.Name));
            Assert.False(string.IsNullOrEmpty(item.Icon));
            Assert.True(item.Value >= 0);
        });
    }

    [Fact]
    public void Equipment_StatBonus_SumsFromRegistry()
    {
        var registry = new ItemRegistry();
        registry.Register(new ItemDef(
            "iron_mace", "Iron Mace", "", "weapon", "mainHand", "",
            new BaseStats(1, 0, 0, 0, 0), 25));
        registry.Register(new ItemDef(
            "scale_mail", "Scale Mail", "", "armor", "body", "",
            new BaseStats(0, 0, 1, 0, 0), 30));

        var eq = new Equipment("iron_mace", null, "scale_mail", null, null);
        var bonus = eq.StatBonus(registry);

        Assert.Equal(1, bonus.Strength);
        Assert.Equal(1, bonus.Constitution);
    }

    [Fact]
    public void Equipment_UnknownItem_Ignored()
    {
        var registry = new ItemRegistry();
        var eq = new Equipment("nonexistent", null, null, null, null);
        var bonus = eq.StatBonus(registry);

        Assert.Equal(new BaseStats(0, 0, 0, 0, 0), bonus);
    }

    [Fact]
    public void ItemCounts_MatchPhase1Requirements()
    {
        var weapons = LoadItems("weapons");
        var armor = LoadItems("armor");
        var consumables = LoadItems("consumables");
        var components = LoadItems("components");

        Assert.True(weapons.Length >= 4, $"Expected 4+ weapons, got {weapons.Length}");
        Assert.True(armor.Length >= 6, $"Expected 6+ armor, got {armor.Length}");
        Assert.True(consumables.Length >= 6, $"Expected 6+ consumables, got {consumables.Length}");
        Assert.True(components.Length >= 4, $"Expected 4+ components, got {components.Length}");
    }

    private static ItemDef[] LoadItems(string category)
    {
        var path = $"../../../../../../content/items/{category}.json";
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ItemDef[]>(json, JsonOptions)!;
    }
}
