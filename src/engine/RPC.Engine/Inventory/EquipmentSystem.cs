using RPC.Engine.Character;
using RPC.Engine.Content;

namespace RPC.Engine.Inventory;

/// <summary>
/// Outcome of an equip/unequip operation. On failure <see cref="Character"/> is the
/// unchanged input state and <see cref="Error"/> describes the rejection; on success
/// <see cref="Character"/> is the new state with the equipment/inventory move applied.
/// </summary>
public readonly record struct EquipResult(bool Success, string? Error, CharacterState Character)
{
    public static EquipResult Fail(CharacterState character, string error) => new(false, error, character);
    public static EquipResult Ok(CharacterState character) => new(true, null, character);
}

/// <summary>
/// Server-authoritative equip/unequip logic. Pure functions over <see cref="CharacterState"/>:
/// equipping moves an item from the character's component inventory into an equipment slot;
/// unequipping moves the slotted item back into the inventory (respecting capacity).
///
/// Slot/type enforcement is implemented fully from item content metadata. Class restrictions
/// are content-gated: <see cref="ItemDef"/> carries no class-restriction field today, so no
/// class enforcement is fabricated here. When such metadata is added to content, gate it in
/// <see cref="SlotAcceptsItem"/> or a dedicated check.
/// </summary>
public static class EquipmentSystem
{
    /// <summary>
    /// Resolve the canonical equipment slot an item fits, or null when the item is not
    /// equippable. Mirrors <see cref="SlotAcceptsItem"/> (the authoritative check used during
    /// equip) by returning the first accepting slot, so accessories resolve to "accessory1".
    /// Intended for UI affordances — equip itself re-validates server-side.
    /// </summary>
    public static string? ResolveSlot(ItemDef item)
    {
        if (item is null) return null;
        foreach (var slot in Equipment.SlotNames)
        {
            if (SlotAcceptsItem(slot, item)) return slot;
        }
        return null;
    }

    /// <summary>True if an item of the given definition may occupy the named equipment slot.</summary>
    public static bool SlotAcceptsItem(string slot, ItemDef item) => slot.ToLowerInvariant() switch
    {
        "mainhand" => string.Equals(item.Slot, "mainHand", StringComparison.OrdinalIgnoreCase),
        "offhand" => string.Equals(item.Slot, "offHand", StringComparison.OrdinalIgnoreCase),
        "armor" => string.Equals(item.Type, "armor", StringComparison.OrdinalIgnoreCase),
        "accessory1" or "accessory2" =>
            string.Equals(item.Type, "accessory", StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.Slot, "accessory", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    /// <summary>
    /// Equip <paramref name="itemId"/> from the character's inventory into <paramref name="slot"/>.
    /// Any item already in the slot is returned to the inventory.
    /// </summary>
    public static EquipResult Equip(CharacterState character, string itemId, string slot, ItemRegistry? items)
    {
        if (!Equipment.IsValidSlot(slot))
            return EquipResult.Fail(character, $"Invalid equipment slot: {slot}");

        if (items is null)
            return EquipResult.Fail(character, "Item registry unavailable; cannot validate equipment.");

        if (items.Get(itemId) is not { } item)
            return EquipResult.Fail(character, $"Unknown item: {itemId}");

        if (!SlotAcceptsItem(slot, item))
            return EquipResult.Fail(character, $"Item '{itemId}' ({item.Type}/{item.Slot}) does not fit slot '{slot}'.");

        if (!ComponentInventorySystem.HasComponent(character.ComponentInventory, itemId, 1))
            return EquipResult.Fail(character, $"Item '{itemId}' is not in inventory.");

        var previous = character.Equipment.GetSlot(slot);

        // Remove the item being equipped first (frees a slot if it was the last of its stack),
        // then return any previously equipped item to the inventory.
        var inventory = ComponentInventorySystem.RemoveComponent(character.ComponentInventory, itemId, 1);

        if (previous is not null)
        {
            if (!ComponentInventorySystem.CanAddComponent(inventory, previous, 1, CharacterState.MaxComponentSlots))
                return EquipResult.Fail(character, "Inventory full; cannot unequip the currently equipped item.");
            inventory = ComponentInventorySystem.AddComponent(inventory, previous, 1, CharacterState.MaxComponentSlots);
        }

        var equipped = character with
        {
            ComponentInventory = inventory,
            Equipment = character.Equipment.WithSlot(slot, itemId),
        };
        return EquipResult.Ok(equipped);
    }

    /// <summary>
    /// Unequip whatever occupies <paramref name="slot"/>, returning it to the character's
    /// inventory. Fails if the slot is empty or the inventory has no room.
    /// </summary>
    public static EquipResult Unequip(CharacterState character, string slot)
    {
        if (!Equipment.IsValidSlot(slot))
            return EquipResult.Fail(character, $"Invalid equipment slot: {slot}");

        var itemId = character.Equipment.GetSlot(slot);
        if (itemId is null)
            return EquipResult.Fail(character, $"Nothing equipped in slot '{slot}'.");

        if (!ComponentInventorySystem.CanAddComponent(character.ComponentInventory, itemId, 1, CharacterState.MaxComponentSlots))
            return EquipResult.Fail(character, "Inventory full; cannot unequip.");

        var inventory = ComponentInventorySystem.AddComponent(character.ComponentInventory, itemId, 1, CharacterState.MaxComponentSlots);
        var unequipped = character with
        {
            ComponentInventory = inventory,
            Equipment = character.Equipment.WithSlot(slot, null),
        };
        return EquipResult.Ok(unequipped);
    }
}
