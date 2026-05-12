using RPC.Engine.Character;
using RPC.Engine.Content;
using RPC.Engine.Party;

namespace RPC.Engine.Inventory;

public static class ComponentInventorySystem
{
    public const int DefaultComponentStackLimit = 99;
    public const int LowStockThreshold = 3;

    public static bool CanAddComponent(ComponentStack[] inventory, string itemId, int count, int maxSlots)
    {
        var existing = inventory.FirstOrDefault(s => s.ItemId == itemId && !s.IsFull);
        if (existing.ItemId != null)
        {
            return true;
        }
        return inventory.Length < maxSlots;
    }

    public static ComponentStack[] AddComponent(ComponentStack[] inventory, string itemId, int count, int maxSlots)
    {
        if (count <= 0) return inventory;
        if (!CanAddComponent(inventory, itemId, count, maxSlots))
            throw new InvalidOperationException("No space for component.");

        var list = inventory.ToList();
        var remaining = count;

        // Fill existing stacks first
        for (int i = 0; i < list.Count && remaining > 0; i++)
        {
            if (list[i].ItemId == itemId && !list[i].IsFull)
            {
                var add = Math.Min(remaining, list[i].RemainingSpace);
                list[i] = list[i] with { Count = list[i].Count + add };
                remaining -= add;
            }
        }

        // Create new stacks if needed
        while (remaining > 0 && list.Count < maxSlots)
        {
            var add = Math.Min(remaining, DefaultComponentStackLimit);
            list.Add(new ComponentStack(itemId, add, DefaultComponentStackLimit));
            remaining -= add;
        }

        if (remaining > 0)
            throw new InvalidOperationException("No space for component.");

        return list.ToArray();
    }

    public static ComponentStack[] RemoveComponent(ComponentStack[] inventory, string itemId, int count)
    {
        if (count <= 0) return inventory;

        var total = inventory.Where(s => s.ItemId == itemId).Sum(s => s.Count);
        if (total < count)
            throw new InvalidOperationException($"Not enough {itemId} in inventory.");

        var list = inventory.ToList();
        var remaining = count;

        // Remove from fullest stacks first
        var ordered = list
            .Select((s, i) => (s, i))
            .Where(x => x.s.ItemId == itemId)
            .OrderByDescending(x => x.s.Count)
            .ToList();

        foreach (var (stack, idx) in ordered)
        {
            if (remaining <= 0) break;
            var remove = Math.Min(remaining, stack.Count);
            list[idx] = stack with { Count = stack.Count - remove };
            remaining -= remove;
        }

        return list.Where(s => s.Count > 0).ToArray();
    }

    public static bool HasComponent(ComponentStack[] inventory, string itemId, int count)
    {
        return inventory.Where(s => s.ItemId == itemId).Sum(s => s.Count) >= count;
    }

    public static int GetComponentCount(ComponentStack[] inventory, string itemId)
    {
        return inventory.Where(s => s.ItemId == itemId).Sum(s => s.Count);
    }

    public static string[] GetLowStockWarnings(ComponentStack[] inventory)
    {
        return inventory
            .Where(s => s.Count > 0 && s.Count <= LowStockThreshold)
            .Select(s => s.ItemId)
            .Distinct()
            .ToArray();
    }

    public static (ComponentStack[] SourceInventory, ComponentStack[] TargetInventory) TransferComponent(
        ComponentStack[] source, ComponentStack[] target, string itemId, int count, int targetMaxSlots)
    {
        if (!HasComponent(source, itemId, count))
            throw new InvalidOperationException($"Not enough {itemId} in source.");

        var newTarget = AddComponent(target, itemId, count, targetMaxSlots);
        var newSource = RemoveComponent(source, itemId, count);
        return (newSource, newTarget);
    }

    public static PartyState TransferToExpeditionCache(PartyState party, int memberIndex, string itemId, int count)
    {
        if (memberIndex < 0 || memberIndex >= 6)
            throw new ArgumentOutOfRangeException(nameof(memberIndex));

        var member = party.Members[memberIndex];
        if (member.Id == Guid.Empty)
            throw new InvalidOperationException("No character in slot.");

        var (newInventory, newCache) = TransferComponent(
            member.ComponentInventory,
            party.ExpeditionCache,
            itemId,
            count,
            PartyState.MaxExpeditionCacheSlots);

        party.SetMember(memberIndex, member with { ComponentInventory = newInventory });
        party.ExpeditionCache = newCache;
        return party;
    }

    public static PartyState TransferFromExpeditionCache(PartyState party, int memberIndex, string itemId, int count)
    {
        if (memberIndex < 0 || memberIndex >= 6)
            throw new ArgumentOutOfRangeException(nameof(memberIndex));

        var member = party.Members[memberIndex];
        if (member.Id == Guid.Empty)
            throw new InvalidOperationException("No character in slot.");

        var (newCache, newInventory) = TransferComponent(
            party.ExpeditionCache,
            member.ComponentInventory,
            itemId,
            count,
            CharacterState.MaxComponentSlots);

        party.SetMember(memberIndex, member with { ComponentInventory = newInventory });
        party.ExpeditionCache = newCache;
        return party;
    }

    /// <summary>
    /// Fallback casting: Bonewarden can pay ability cost with HP at 2× rate.
    /// Returns (success, hpCost, message).
    /// </summary>
    public static (bool Success, int HpCost, string? Message) TryFallbackCast(
        CharacterState character,
        AbilityCost cost,
        ItemRegistry? items = null)
    {
        if (character.ClassId != "bonewarden")
            return (false, 0, "Fallback casting only available to Bonewarden.");

        if (cost.Type != "component" || cost.Amount is null or <= 0)
            return (false, 0, "Fallback casting only applies to component costs.");

        var hpCost = cost.Amount.Value * 2;
        if (character.CurrentHp <= hpCost)
            return (false, 0, "Not enough HP for fallback casting.");

        return (true, hpCost, null);
    }
}
