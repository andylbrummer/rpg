using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Inventory;
using RPC.Engine.Party;
using RPC.Engine.Save;

namespace RPC.Tests;

public class ComponentInventoryTests
{
    [Fact]
    public void AddComponent_ToEmptyInventory()
    {
        var inv = Array.Empty<ComponentStack>();
        var result = ComponentInventorySystem.AddComponent(inv, "bone_shard", 5, 8);

        Assert.Single(result);
        Assert.Equal("bone_shard", result[0].ItemId);
        Assert.Equal(5, result[0].Count);
    }

    [Fact]
    public void AddComponent_StacksSameItem()
    {
        var inv = new[] { new ComponentStack("bone_shard", 10) };
        var result = ComponentInventorySystem.AddComponent(inv, "bone_shard", 5, 8);

        Assert.Single(result);
        Assert.Equal(15, result[0].Count);
    }

    [Fact]
    public void AddComponent_CreatesNewStackWhenFull()
    {
        var inv = new[] { new ComponentStack("bone_shard", 99) };
        var result = ComponentInventorySystem.AddComponent(inv, "bone_shard", 5, 8);

        Assert.Equal(2, result.Length);
        Assert.Equal(99, result[0].Count);
        Assert.Equal(5, result[1].Count);
    }

    [Fact]
    public void AddComponent_RespectsSlotLimit()
    {
        var inv = new[]
        {
            new ComponentStack("bone_shard", 99),
            new ComponentStack("blood_vial", 99),
            new ComponentStack("cinder", 99),
            new ComponentStack("shiv_blade", 99),
            new ComponentStack("ink_bottle", 99),
            new ComponentStack("map_fragment", 99),
            new ComponentStack("tithe_token", 99),
            new ComponentStack("hollow_essence", 99)
        };

        Assert.Throws<InvalidOperationException>(() =>
            ComponentInventorySystem.AddComponent(inv, "new_item", 1, 8));
    }

    [Fact]
    public void RemoveComponent_FromStack()
    {
        var inv = new[] { new ComponentStack("bone_shard", 10) };
        var result = ComponentInventorySystem.RemoveComponent(inv, "bone_shard", 3);

        Assert.Single(result);
        Assert.Equal(7, result[0].Count);
    }

    [Fact]
    public void RemoveComponent_RemovesEmptyStacks()
    {
        var inv = new[] { new ComponentStack("bone_shard", 3) };
        var result = ComponentInventorySystem.RemoveComponent(inv, "bone_shard", 3);

        Assert.Empty(result);
    }

    [Fact]
    public void RemoveComponent_FromMultipleStacks()
    {
        var inv = new[]
        {
            new ComponentStack("bone_shard", 50),
            new ComponentStack("bone_shard", 40)
        };
        var result = ComponentInventorySystem.RemoveComponent(inv, "bone_shard", 60);

        Assert.Single(result);
        Assert.Equal(30, result[0].Count);
    }

    [Fact]
    public void RemoveComponent_ThrowsWhenInsufficient()
    {
        var inv = new[] { new ComponentStack("bone_shard", 5) };
        Assert.Throws<InvalidOperationException>(() =>
            ComponentInventorySystem.RemoveComponent(inv, "bone_shard", 10));
    }

    [Fact]
    public void HasComponent_ReturnsCorrectValue()
    {
        var inv = new[] { new ComponentStack("bone_shard", 10) };
        Assert.True(ComponentInventorySystem.HasComponent(inv, "bone_shard", 5));
        Assert.False(ComponentInventorySystem.HasComponent(inv, "bone_shard", 15));
        Assert.False(ComponentInventorySystem.HasComponent(inv, "blood_vial", 1));
    }

    [Fact]
    public void GetComponentCount_SumsAcrossStacks()
    {
        var inv = new[]
        {
            new ComponentStack("bone_shard", 50),
            new ComponentStack("bone_shard", 40)
        };
        Assert.Equal(90, ComponentInventorySystem.GetComponentCount(inv, "bone_shard"));
        Assert.Equal(0, ComponentInventorySystem.GetComponentCount(inv, "blood_vial"));
    }

    [Fact]
    public void GetLowStockWarnings_IdentifiesLowStock()
    {
        var inv = new[]
        {
            new ComponentStack("bone_shard", 2),
            new ComponentStack("blood_vial", 5),
            new ComponentStack("cinder", 3)
        };
        var warnings = ComponentInventorySystem.GetLowStockWarnings(inv);

        Assert.Equal(2, warnings.Length);
        Assert.Contains("bone_shard", warnings);
        Assert.Contains("cinder", warnings);
        Assert.DoesNotContain("blood_vial", warnings);
    }

    [Fact]
    public void TransferComponent_MovesItems()
    {
        var source = new[] { new ComponentStack("bone_shard", 10) };
        var target = Array.Empty<ComponentStack>();

        var (newSource, newTarget) = ComponentInventorySystem.TransferComponent(
            source, target, "bone_shard", 5, 8);

        Assert.Single(newSource);
        Assert.Equal(5, newSource[0].Count);
        Assert.Single(newTarget);
        Assert.Equal(5, newTarget[0].Count);
    }

    [Fact]
    public void TransferToExpeditionCache_MovesFromMemberToCache()
    {
        var party = new PartyState();
        var member = new CharacterState(
            Guid.NewGuid(), "Test", "bonewarden", 1, 0,
            new BaseStats(4, 3, 5, 4, 4), 17, Equipment.Empty,
            Array.Empty<string>(), 0, null, null, null, 0, false,
            new[] { new ComponentStack("bone_shard", 10) });
        party.SetMember(0, member);

        ComponentInventorySystem.TransferToExpeditionCache(party, 0, "bone_shard", 5);

        Assert.Equal(5, party.Members[0].ComponentInventory[0].Count);
        Assert.Single(party.ExpeditionCache);
        Assert.Equal(5, party.ExpeditionCache[0].Count);
    }

    [Fact]
    public void TransferFromExpeditionCache_MovesFromCacheToMember()
    {
        var party = new PartyState();
        party.ExpeditionCache = new[] { new ComponentStack("bone_shard", 10) };
        var member = new CharacterState(
            Guid.NewGuid(), "Test", "bonewarden", 1, 0,
            new BaseStats(4, 3, 5, 4, 4), 17, Equipment.Empty,
            Array.Empty<string>(), 0, null, null, null, 0, false,
            Array.Empty<ComponentStack>());
        party.SetMember(0, member);

        ComponentInventorySystem.TransferFromExpeditionCache(party, 0, "bone_shard", 5);

        Assert.Single(party.Members[0].ComponentInventory);
        Assert.Equal(5, party.Members[0].ComponentInventory[0].Count);
        Assert.Single(party.ExpeditionCache);
        Assert.Equal(5, party.ExpeditionCache[0].Count);
    }

    [Fact]
    public void TryFallbackCast_Bonewarden_WithEnoughHp()
    {
        var character = new CharacterState(
            Guid.NewGuid(), "Kael", "bonewarden", 1, 0,
            new BaseStats(4, 3, 5, 4, 4), 17, Equipment.Empty,
            Array.Empty<string>(), 0, null, null, null, 0, false,
            Array.Empty<ComponentStack>());

        var cost = new AbilityCost("component", 3);
        var (success, hpCost, message) = ComponentInventorySystem.TryFallbackCast(character, cost);

        Assert.True(success);
        Assert.Equal(6, hpCost);
        Assert.Null(message);
    }

    [Fact]
    public void TryFallbackCast_Bonewarden_WithLowHp()
    {
        var character = new CharacterState(
            Guid.NewGuid(), "Kael", "bonewarden", 1, 0,
            new BaseStats(4, 3, 5, 4, 4), 5, Equipment.Empty,
            Array.Empty<string>(), 0, null, null, null, 0, false,
            Array.Empty<ComponentStack>());

        var cost = new AbilityCost("component", 3);
        var (success, hpCost, message) = ComponentInventorySystem.TryFallbackCast(character, cost);

        Assert.False(success);
        Assert.Equal(0, hpCost);
        Assert.Equal("Not enough HP for fallback casting.", message);
    }

    [Fact]
    public void TryFallbackCast_NonBonewarden_Fails()
    {
        var character = new CharacterState(
            Guid.NewGuid(), "Sera", "stillblade", 1, 0,
            new BaseStats(4, 3, 5, 4, 4), 17, Equipment.Empty,
            Array.Empty<string>(), 0, null, null, null, 0, false,
            Array.Empty<ComponentStack>());

        var cost = new AbilityCost("component", 3);
        var (success, hpCost, message) = ComponentInventorySystem.TryFallbackCast(character, cost);

        Assert.False(success);
        Assert.Equal(0, hpCost);
        Assert.Equal("Fallback casting only available to Bonewarden.", message);
    }

    [Fact]
    public void TryFallbackCast_NonComponentCost_Fails()
    {
        var character = new CharacterState(
            Guid.NewGuid(), "Kael", "bonewarden", 1, 0,
            new BaseStats(4, 3, 5, 4, 4), 17, Equipment.Empty,
            Array.Empty<string>(), 0, null, null, null, 0, false,
            Array.Empty<ComponentStack>());

        var cost = new AbilityCost("memory", 3);
        var (success, hpCost, message) = ComponentInventorySystem.TryFallbackCast(character, cost);

        Assert.False(success);
        Assert.Equal(0, hpCost);
        Assert.Equal("Fallback casting only applies to component costs.", message);
    }

    [Fact]
    public void SaveLoad_PreservesComponentInventory()
    {
        var state = new GameState(seed: 42);
        var member = state.Party.Members[0];
        var inventory = new[] { new ComponentStack("bone_shard", 10), new ComponentStack("blood_vial", 5) };
        state.Party.SetMember(0, member with { ComponentInventory = inventory });
        state.Party.ExpeditionCache = new[] { new ComponentStack("cinder", 20) };

        using var tempDir = new TempDirectory();
        var path = Path.Combine(tempDir.Path, "save.json");
        SaveSystem.Save(state, path);

        var loaded = new GameState(seed: 42);
        Assert.True(SaveSystem.Load(loaded, path));

        Assert.Equal(2, loaded.Party.Members[0].ComponentInventory.Length);
        Assert.Equal("bone_shard", loaded.Party.Members[0].ComponentInventory[0].ItemId);
        Assert.Equal(10, loaded.Party.Members[0].ComponentInventory[0].Count);
        Assert.Equal("blood_vial", loaded.Party.Members[0].ComponentInventory[1].ItemId);
        Assert.Equal(5, loaded.Party.Members[0].ComponentInventory[1].Count);
        Assert.Single(loaded.Party.ExpeditionCache);
        Assert.Equal("cinder", loaded.Party.ExpeditionCache[0].ItemId);
        Assert.Equal(20, loaded.Party.ExpeditionCache[0].Count);
    }

    private class TempDirectory : IDisposable
    {
        public string Path { get; }
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(Path);
        }
        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
