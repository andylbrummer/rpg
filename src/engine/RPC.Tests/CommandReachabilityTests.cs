using RPC.Engine.Combat;
using RPC.Engine.Commands;

namespace RPC.Tests;

/// <summary>
/// Pins which commands a client can actually reach.
///
/// A command type is only worth the engine behind it if some action string builds it.
/// <see cref="CommandDispatchTests"/> asserts that every command the dispatcher produces is handled;
/// this asserts the other half — that every command that exists can be produced at all. Without it,
/// engine behaviour can be written, tested and shipped while remaining unreachable in the running
/// game, which is exactly what had happened to ironman mode.
///
/// Commands that are deliberately internal belong in <see cref="NotReachableFromTheWire"/>, with
/// the reason. The list is the point: it turns "nothing can invoke this" from something nobody
/// notices into something a reader has to justify.
/// </summary>
public class CommandReachabilityTests
{
    /// <summary>
    /// Command types no action string builds, and why that is currently so.
    /// <para>
    /// Empty, and worth keeping that way. Anything added here is engine behaviour that exists but
    /// that no player can reach, which is the state ironman mode and the betrayal choice were both
    /// found in — built, tested and documented, with nothing on the wire to invoke them.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> NotReachableFromTheWire = new();

    [Fact]
    public void Every_Command_Type_Is_Reachable_From_Some_Action()
    {
        var reachable = CommandDispatcher.KnownActions
            .Select(BuildAction)
            .Select(CommandDispatcher.Parse)
            .Select(c => c.GetType().Name)
            .ToHashSet();

        var declared = typeof(ICommand).Assembly.GetTypes()
            .Where(t => typeof(ICommand).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .Select(t => t.Name)
            .ToHashSet();

        var unreachable = declared.Except(reachable).Except(NotReachableFromTheWire).OrderBy(n => n).ToList();
        Assert.Empty(unreachable);
    }

    /// <summary>
    /// Every exclusion must name a command that still exists, so deleting or wiring one up forces
    /// the list to be updated rather than leaving a stale entry behind.
    /// </summary>
    [Fact]
    public void The_Unreachable_List_Has_No_Stale_Entries()
    {
        var reachable = CommandDispatcher.KnownActions
            .Select(BuildAction)
            .Select(CommandDispatcher.Parse)
            .Select(c => c.GetType().Name)
            .ToHashSet();

        var declared = typeof(ICommand).Assembly.GetTypes()
            .Where(t => typeof(ICommand).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .Select(t => t.Name)
            .ToHashSet();

        foreach (var name in NotReachableFromTheWire)
        {
            Assert.True(declared.Contains(name), $"{name} is listed as unreachable but no longer exists.");
            Assert.False(reachable.Contains(name), $"{name} is listed as unreachable but an action now builds it.");
        }
    }

    /// <summary>
    /// An action carrying every optional field populated. The factories reject missing arguments,
    /// and which ones each needs is not the subject here — only which command comes out.
    /// </summary>
    private static PlayerAction BuildAction(string type) => new()
    {
        Type = type,
        Action = new CombatAction(Guid.NewGuid(), ActionType.Attack, Guid.NewGuid(), "ability", "item"),
        DungeonType = "test",
        Slot = 0,
        // A GUID string: some factories parse this field as a character id, and the ones that
        // treat it as an opaque id accept any string.
        TargetId = Guid.NewGuid().ToString(),
        Value = 0,
        Branch = "branch",
        DowntimeAction = nameof(RPC.Engine.Town.DowntimeAction.Rest),
        Source = nameof(RPC.Engine.Town.RumorVerificationSource.Firsthand),
        ItemId = "item",
        EquipSlot = "weapon",
        Enabled = true,
    };
}
