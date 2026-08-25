using RPC.Engine.Character;
using RPC.Engine.Content;
using RPC.Engine.Inventory;

namespace RPC.Tests;

public class EquipmentSystemTests
{
    private static ItemRegistry BuildRegistry()
    {
        var reg = new ItemRegistry();
        reg.Register(new ItemDef("rusty_sword", "Rusty Sword", "", "weapon", "mainHand", "icon",
            new BaseStats(2, 0, 0, 0, 0), 10));
        reg.Register(new ItemDef("buckler", "Buckler", "", "armor", "offHand", "icon",
            new BaseStats(0, 0, 0, 0, 0), 10));
        reg.Register(new ItemDef("iron_helm", "Iron Helm", "", "armor", "head", "icon",
            new BaseStats(0, 0, 3, 0, 0), 20));
        return reg;
    }

    private static CharacterState MakeCharacter(params ComponentStack[] inventory) =>
        new(
            Guid.NewGuid(), "Tester", "stillblade", 1, 0,
            new BaseStats(5, 5, 5, 5, 5), 10, Equipment.Empty,
            Array.Empty<string>(), 0,
            ComponentInventory: inventory.Length == 0 ? Array.Empty<ComponentStack>() : inventory);

    [Fact]
    public void Equip_ValidItem_MovesFromInventoryToSlot()
    {
        var reg = BuildRegistry();
        var c = MakeCharacter(new ComponentStack("rusty_sword", 1));

        var result = EquipmentSystem.Equip(c, "rusty_sword", "mainHand", reg);

        Assert.True(result.Success);
        Assert.Equal("rusty_sword", result.Character.Equipment.MainHand);
        Assert.False(ComponentInventorySystem.HasComponent(result.Character.ComponentInventory, "rusty_sword", 1));
    }

    [Fact]
    public void Equip_InvalidSlotForItemType_Rejected()
    {
        var reg = BuildRegistry();
        var c = MakeCharacter(new ComponentStack("iron_helm", 1));

        // iron_helm is armor; mainHand only accepts weapons.
        var result = EquipmentSystem.Equip(c, "iron_helm", "mainHand", reg);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Null(result.Character.Equipment.MainHand);
        Assert.True(ComponentInventorySystem.HasComponent(result.Character.ComponentInventory, "iron_helm", 1));
    }

    [Fact]
    public void Equip_ItemNotInInventory_Rejected()
    {
        var reg = BuildRegistry();
        var c = MakeCharacter();

        var result = EquipmentSystem.Equip(c, "rusty_sword", "mainHand", reg);

        Assert.False(result.Success);
        Assert.Null(result.Character.Equipment.MainHand);
    }

    [Fact]
    public void Equip_UnknownItem_Rejected()
    {
        var reg = BuildRegistry();
        var c = MakeCharacter(new ComponentStack("phantom_blade", 1));

        var result = EquipmentSystem.Equip(c, "phantom_blade", "mainHand", reg);

        Assert.False(result.Success);
    }

    [Fact]
    public void Equip_SwapsExistingItemBackToInventory()
    {
        var reg = BuildRegistry();
        var c = MakeCharacter(new ComponentStack("rusty_sword", 1));
        var equipped = EquipmentSystem.Equip(c, "rusty_sword", "mainHand", reg).Character;
        // Now inventory has another sword; equip it, expecting the first back in inventory.
        equipped = equipped with
        {
            ComponentInventory = ComponentInventorySystem.AddComponent(
                equipped.ComponentInventory, "rusty_sword", 1, CharacterState.MaxComponentSlots)
        };

        var result = EquipmentSystem.Equip(equipped, "rusty_sword", "mainHand", reg);

        Assert.True(result.Success);
        Assert.Equal("rusty_sword", result.Character.Equipment.MainHand);
        Assert.True(ComponentInventorySystem.HasComponent(result.Character.ComponentInventory, "rusty_sword", 1));
    }

    [Fact]
    public void Unequip_RoundTripsItemBackToInventory()
    {
        var reg = BuildRegistry();
        var c = MakeCharacter(new ComponentStack("rusty_sword", 1));
        var equipped = EquipmentSystem.Equip(c, "rusty_sword", "mainHand", reg).Character;

        var result = EquipmentSystem.Unequip(equipped, "mainHand");

        Assert.True(result.Success);
        Assert.Null(result.Character.Equipment.MainHand);
        Assert.True(ComponentInventorySystem.HasComponent(result.Character.ComponentInventory, "rusty_sword", 1));
    }

    [Fact]
    public void Unequip_EmptySlot_Rejected()
    {
        var c = MakeCharacter();

        var result = EquipmentSystem.Unequip(c, "mainHand");

        Assert.False(result.Success);
    }

    [Fact]
    public void Equip_RecomputesEffectiveStats()
    {
        var reg = BuildRegistry();
        var c = MakeCharacter(new ComponentStack("iron_helm", 1));
        var before = c.GetEffectiveStats(reg);

        var equipped = EquipmentSystem.Equip(c, "iron_helm", "armor", reg).Character;
        var after = equipped.GetEffectiveStats(reg);

        // iron_helm grants +3 constitution, which raises MaxHp.
        Assert.True(after.MaxHp > before.MaxHp);
    }

    [Fact]
    public void Unequip_InventoryFull_Guarded()
    {
        var reg = BuildRegistry();
        // Equip a sword, then fill the inventory to capacity with distinct items.
        var c = MakeCharacter(new ComponentStack("rusty_sword", 1));
        var equipped = EquipmentSystem.Equip(c, "rusty_sword", "mainHand", reg).Character;

        var full = new ComponentStack[CharacterState.MaxComponentSlots];
        for (int i = 0; i < full.Length; i++)
            full[i] = new ComponentStack($"filler_{i}", 99);
        equipped = equipped with { ComponentInventory = full };

        var result = EquipmentSystem.Unequip(equipped, "mainHand");

        Assert.False(result.Success);
        Assert.Equal("rusty_sword", result.Character.Equipment.MainHand);
    }
}
