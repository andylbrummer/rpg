using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Commands;
using RPC.Engine.Party;

namespace RPC.Tests;

/// <summary>
/// Town-only access gating for town-storage transfers. Town storage is the long-term stash
/// the party leaves in town, so the command handler must reject transfers when the party is
/// out in the field (any non-<see cref="GameMode.Menu"/> mode).
/// </summary>
public class TownStorageTransferTests
{
    private static GameState StateWithMember(out Guid memberId)
    {
        var state = new GameState(seed: 42);
        var member = state.Party.Members[0];
        memberId = member.Id;
        state.Party.SetMember(0, member with
        {
            ComponentInventory = new[] { new ComponentStack("bone_shard", 10) }
        });
        return state;
    }

    [Fact]
    public void TransferToTownStorage_InTown_MovesItems()
    {
        var state = StateWithMember(out _);
        state.Mode = GameMode.Menu; // in town
        var handler = new GameCommandHandler(state, new StubDungeonGenerator());

        var result = handler.Execute(new TransferToTownStorageCommand(0, "bone_shard", 4));

        Assert.True(result.StateChanged);
        Assert.Equal(4, state.Party.TownStorage[0].Count);
        Assert.Equal(6, state.Party.Members[0].ComponentInventory[0].Count);
    }

    [Fact]
    public void TransferToTownStorage_OutsideTown_IsRejected()
    {
        var state = StateWithMember(out _);
        state.Mode = GameMode.Exploration; // out in the field
        var handler = new GameCommandHandler(state, new StubDungeonGenerator());

        Assert.Throws<InvalidOperationException>(() =>
            handler.Execute(new TransferToTownStorageCommand(0, "bone_shard", 4)));
        Assert.Empty(state.Party.TownStorage);
    }

    [Fact]
    public void TransferFromTownStorage_OutsideTown_IsRejected()
    {
        var state = StateWithMember(out _);
        state.Party.TownStorage = new[] { new ComponentStack("bone_shard", 10) };
        state.Mode = GameMode.Combat;
        var handler = new GameCommandHandler(state, new StubDungeonGenerator());

        Assert.Throws<InvalidOperationException>(() =>
            handler.Execute(new TransferFromTownStorageCommand(0, "bone_shard", 4)));
    }
}
