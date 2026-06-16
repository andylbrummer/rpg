using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Dungeons;
using RPC.Engine.Inventory;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Save;

namespace RPC.Tests;

/// <summary>
/// T51c Bloom sample decay. Model: decay age is tracked PER ComponentStack entry
/// (<see cref="ComponentStack.DungeonTurnsAlive"/>). Samples gathered together share one entry and
/// one age; <see cref="BloomDecaySystem.AddBloomSample"/> always appends a fresh age-0 entry rather
/// than merging into an existing one, so mixed-age stacks never arise (no splitting needed). The
/// counter advances only on in-dungeon turns; stabilized entries are skipped.
/// </summary>
public class BloomDecayTests
{
    private static ComponentStack Sample(int age = 0, int count = 1, bool stabilized = false) =>
        new(BloomDecaySystem.BloomSampleItemId, count, 99, age, stabilized);

    [Fact]
    public void TickInventory_AtNineTurns_DoesNotDecay()
    {
        var inv = new[] { Sample(age: 8) };
        var (result, decayed) = BloomDecaySystem.TickInventory(inv);

        Assert.Equal(0, decayed);
        Assert.Single(result);
        Assert.Equal(9, result[0].DungeonTurnsAlive);
    }

    [Fact]
    public void TickInventory_AtTenTurns_Decays()
    {
        var inv = new[] { Sample(age: 9) };
        var (result, decayed) = BloomDecaySystem.TickInventory(inv);

        Assert.Equal(1, decayed);
        Assert.Empty(result);
    }

    [Fact]
    public void TickInventory_FromZero_DecaysExactlyAtTenthTurn()
    {
        var inv = new[] { Sample(age: 0) };
        for (int turn = 1; turn <= 9; turn++)
        {
            int d;
            (inv, d) = BloomDecaySystem.TickInventory(inv);
            Assert.Equal(0, d);
            Assert.Single(inv);
        }

        var (after10, decayed) = BloomDecaySystem.TickInventory(inv);
        Assert.Equal(1, decayed);
        Assert.Empty(after10);
    }

    [Fact]
    public void TickInventory_StabilizedSample_SkipsDecay()
    {
        var inv = new[] { Sample(age: 9, stabilized: true) };
        var (result, decayed) = BloomDecaySystem.TickInventory(inv);

        Assert.Equal(0, decayed);
        Assert.Single(result);
        Assert.Equal(9, result[0].DungeonTurnsAlive); // untouched
        Assert.True(result[0].Stabilized);
    }

    [Fact]
    public void TickInventory_NonBloomComponents_Untouched()
    {
        var inv = new[] { new ComponentStack("bone_fragment", 5) };
        var (result, decayed) = BloomDecaySystem.TickInventory(inv);

        Assert.Equal(0, decayed);
        Assert.Single(result);
        Assert.Equal(0, result[0].DungeonTurnsAlive);
    }

    [Fact]
    public void AddBloomSample_AppendsFreshAgeZeroEntry_DoesNotMerge()
    {
        var inv = new[] { Sample(age: 7) };
        var result = BloomDecaySystem.AddBloomSample(inv, 1, maxSlots: 8);

        Assert.Equal(2, result.Length);
        Assert.Equal(7, result[0].DungeonTurnsAlive);
        Assert.Equal(0, result[1].DungeonTurnsAlive);
    }

    [Fact]
    public void TickDungeonTurn_OutsideDungeon_DoesNotAdvanceCounter()
    {
        var state = CreateDungeonState(enterDungeon: false);
        SeedSampleInMember0(state, age: 0);

        // Town / travel turns: not in a dungeon node.
        state.Mode = GameMode.Menu;
        BloomDecaySystem.TickDungeonTurn(state);
        BloomDecaySystem.TickDungeonTurn(state);

        Assert.Equal(0, state.Party.Members[0].ComponentInventory[0].DungeonTurnsAlive);
    }

    [Fact]
    public void TickDungeonTurn_InDungeon_AdvancesAndDecaysWithNotification()
    {
        var state = CreateDungeonState(enterDungeon: true);
        SeedSampleInMember0(state, age: 0);

        for (int i = 0; i < 9; i++)
            BloomDecaySystem.TickDungeonTurn(state);
        Assert.Single(state.Party.Members[0].ComponentInventory);
        Assert.Equal(9, state.Party.Members[0].ComponentInventory[0].DungeonTurnsAlive);

        BloomDecaySystem.TickDungeonTurn(state); // 10th in-dungeon turn

        Assert.Empty(state.Party.Members[0].ComponentInventory);
        var decayLog = Assert.Single(state.ActionLog, e => e.Type == "bloom_sample_decayed");
        Assert.Equal("A bloom sample has decayed into inert matter.", decayLog.Payload["message"]);
    }

    [Fact]
    public void TickDungeonTurn_StabilizedSample_PersistsIndefinitely()
    {
        var state = CreateDungeonState(enterDungeon: true);
        SeedSampleInMember0(state, age: 0, stabilized: true);

        for (int i = 0; i < 50; i++)
            BloomDecaySystem.TickDungeonTurn(state);

        Assert.Single(state.Party.Members[0].ComponentInventory);
        Assert.True(state.Party.Members[0].ComponentInventory[0].Stabilized);
        Assert.DoesNotContain(state.ActionLog, e => e.Type == "bloom_sample_decayed");
    }

    [Fact]
    public void SaveLoad_RoundTripsPerSampleDungeonTurnsAndStabilizedFlag()
    {
        var state = CreateDungeonState(enterDungeon: true);
        var member = state.Party.Members[0];
        state.Party.SetMember(0, member with
        {
            ComponentInventory = new[] { Sample(age: 4), Sample(age: 2, stabilized: true) }
        });

        var path = Path.Combine(Path.GetTempPath(), $"bloom_decay_{Guid.NewGuid()}.json");
        try
        {
            state.SaveGame(path);

            var reloaded = new GameState(seed: 1);
            Assert.True(reloaded.LoadGame(path));

            var inv = reloaded.Party.Members[0].ComponentInventory
                .Where(s => s.ItemId == BloomDecaySystem.BloomSampleItemId)
                .OrderByDescending(s => s.DungeonTurnsAlive)
                .ToArray();

            Assert.Equal(2, inv.Length);
            Assert.Equal(4, inv[0].DungeonTurnsAlive);
            Assert.False(inv[0].Stabilized);
            Assert.Equal(2, inv[1].DungeonTurnsAlive);
            Assert.True(inv[1].Stabilized);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void DungeonMove_CountsAsDungeonTurn_AdvancesCounter()
    {
        var state = CreateMovableDungeonState();
        SeedSampleInMember0(state, age: 0);

        Assert.True(state.TryMoveForward());

        Assert.Equal(1, state.Party.Members[0].ComponentInventory[0].DungeonTurnsAlive);
    }

    // --- helpers ---

    private static GameState CreateDungeonState(bool enterDungeon)
    {
        var state = new GameState(seed: 42);
        // Level 1 avoids the pending branch-choice gate that would block EnterDungeon.
        var character = new CharacterState(
            Guid.NewGuid(), "Kael", "bonewarden", 1, 0,
            new BaseStats(4, 3, 5, 4, 4), 17, Equipment.Empty,
            new[] { "bone_spear" }, 0);
        state.Party.SetMember(0, character);
        if (enterDungeon)
        {
            state.EnterDungeon(CreateTestDungeon(), "crypt");
        }
        else
        {
            state.Mode = GameMode.Menu;
        }
        return state;
    }

    private static GameState CreateMovableDungeonState()
    {
        var state = CreateDungeonState(enterDungeon: true);
        state.Player.Position = new Position(5, 5);
        state.Player.Facing = Direction.North;
        return state;
    }

    private static void SeedSampleInMember0(GameState state, int age, bool stabilized = false)
    {
        var member = state.Party.Members[0];
        state.Party.SetMember(0, member with
        {
            ComponentInventory = new[] { Sample(age, stabilized: stabilized) }
        });
    }

    private static Dungeon CreateTestDungeon()
    {
        var dungeon = new Dungeon(11, 11, "Test");
        for (int x = 0; x < 11; x++)
            for (int y = 0; y < 11; y++)
                dungeon.Tiles[x, y] = new Tile(TileType.Floor);
        return dungeon;
    }
}
