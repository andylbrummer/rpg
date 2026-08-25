using RPC.Engine.Character;

namespace RPC.Engine.Inventory;

/// <summary>
/// T51c Bloom sample decay. Bloom samples rot while carried through a dungeon: each in-dungeon turn
/// advances a per-entry counter, and once an entry reaches <see cref="DecayThreshold"/> dungeon
/// turns it is destroyed.
///
/// Model: decay age is tracked PER <see cref="ComponentStack"/> entry via
/// <see cref="ComponentStack.DungeonTurnsAlive"/>, not per individual sample. Samples gathered in
/// the same pickup share one entry and therefore one age, so they decay together as a unit. This is
/// the simplest correct model: <see cref="AddBloomSample"/> always appends a fresh age-0 entry
/// rather than merging into an existing stack, which means mixed-age stacks never arise and no
/// stack-splitting is ever required.
///
/// Counters advance only on in-dungeon turns (see <see cref="TickDungeonTurn"/>); town and travel
/// turns never call into this system. Entries flagged <see cref="ComponentStack.Stabilized"/> skip
/// decay entirely (set by the Heretic "Tend Blooms" downtime in Phase 2; for now exposed only via
/// the <c>Stabilized</c> field as a test hook).
/// </summary>
public static class BloomDecaySystem
{
    /// <summary>Content id of the decaying bloom sample component.</summary>
    public const string BloomSampleItemId = "bloom_sample";

    /// <summary>Dungeon turns a sample survives; at this age it is destroyed.</summary>
    public const int DecayThreshold = 10;

    /// <summary>Action-log/notification text emitted when a bloom sample decays.</summary>
    public const string DecayMessage = "A bloom sample has decayed into inert matter.";

    /// <summary>
    /// Advance one in-dungeon turn over a single inventory. Non-bloom and stabilized entries are
    /// left untouched. Returns the new inventory plus the number of entries that decayed (each is
    /// one decay event for notification purposes).
    /// </summary>
    public static (ComponentStack[] Inventory, int DecayedEntries) TickInventory(ComponentStack[] inventory)
    {
        if (inventory.Length == 0) return (inventory, 0);

        List<ComponentStack>? result = null;
        var decayed = 0;

        for (int i = 0; i < inventory.Length; i++)
        {
            var stack = inventory[i];
            if (stack.ItemId != BloomSampleItemId || stack.Stabilized)
            {
                result?.Add(stack);
                continue;
            }

            var aged = stack with { DungeonTurnsAlive = stack.DungeonTurnsAlive + 1 };
            if (aged.DungeonTurnsAlive >= DecayThreshold)
            {
                result ??= new List<ComponentStack>(inventory[..i]);
                decayed++;
            }
            else
            {
                result ??= new List<ComponentStack>(inventory[..i]);
                result.Add(aged);
            }
        }

        return result is null ? (inventory, 0) : (result.ToArray(), decayed);
    }

    /// <summary>
    /// Add freshly picked-up bloom samples as a new age-0 entry. Deliberately does not merge into an
    /// existing bloom-sample stack so each pickup ages independently.
    /// </summary>
    public static ComponentStack[] AddBloomSample(ComponentStack[] inventory, int count, int maxSlots)
    {
        if (count <= 0) return inventory;
        if (inventory.Length >= maxSlots)
            throw new InvalidOperationException("No space for bloom sample.");

        var list = inventory.ToList();
        list.Add(new ComponentStack(BloomSampleItemId, count, 99, 0, false));
        return list.ToArray();
    }

    /// <summary>
    /// Advance one in-dungeon turn across the whole party (each member inventory plus the expedition
    /// cache) and emit a notification for every sample that decays. No-op unless the party is
    /// currently inside a dungeon node, so town and travel turns never age samples.
    /// </summary>
    public static void TickDungeonTurn(GameState state)
    {
        if (state.Mode != GameMode.Exploration || state.CurrentDungeon == null)
            return;

        var decayed = 0;

        for (int i = 0; i < state.Party.Members.Length; i++)
        {
            var member = state.Party.Members[i];
            if (member.Id == Guid.Empty) continue;

            var (inv, d) = TickInventory(member.ComponentInventory);
            if (d > 0)
            {
                state.Party.SetMember(i, member with { ComponentInventory = inv });
                decayed += d;
            }
            else if (!ReferenceEquals(inv, member.ComponentInventory))
            {
                state.Party.SetMember(i, member with { ComponentInventory = inv });
            }
        }

        var (cache, cacheDecayed) = TickInventory(state.Party.ExpeditionCache);
        if (!ReferenceEquals(cache, state.Party.ExpeditionCache))
            state.Party.ExpeditionCache = cache;
        decayed += cacheDecayed;

        for (int i = 0; i < decayed; i++)
        {
            state.EmitActionLog("dungeon", "bloom_sample_decayed", new Dictionary<string, string>
            {
                { "message", DecayMessage }
            });
        }
    }
}
