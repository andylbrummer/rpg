using RPC.Engine.Character;
using RPC.Engine.Content;
using RPC.Engine.Inventory;

namespace RPC.Host.Web.Presenters;

/// <summary>
/// Projects a <see cref="ComponentStack"/> into the client DTO, enriching it with
/// item metadata (display name, type, resolved equip slot) from the item registry so
/// the inventory UI can show names and offer equip affordances without a second lookup.
/// Unknown items fall back to their id for the name and carry a null equip slot.
/// </summary>
public static class ItemStackPresenter
{
    public static object Present(ComponentStack stack, ItemRegistry items)
    {
        var def = items.Get(stack.ItemId);
        return new
        {
            itemId = stack.ItemId,
            count = stack.Count,
            maxStack = stack.MaxStack,
            name = def?.Name ?? stack.ItemId,
            type = def?.Type,
            equipSlot = def is null ? null : EquipmentSystem.ResolveSlot(def),
        };
    }
}
