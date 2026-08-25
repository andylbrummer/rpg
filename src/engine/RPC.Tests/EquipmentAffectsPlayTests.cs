using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Content;

namespace RPC.Tests;

/// <summary>
/// Equipment stat bonuses have to apply where they are spent, not only where they are shown.
/// Resolving a bonus needs the item registry, and it was an optional argument that only the
/// party presenter passed: the character sheet showed a boosted Power and MaxHp while combat,
/// levelling, resting at the inn and downtime all computed the same character's stats with the
/// bonus omitted. Equipping a weapon changed the number on the screen and nothing in the game.
/// </summary>
public class EquipmentAffectsPlayTests
{
    private static ItemRegistry Items()
    {
        var registry = new ItemRegistry();
        registry.Register(new ItemDef(
            "test_mace", "Test Mace", "Heavy.", "weapon", "mainHand", "",
            new BaseStats(4, 0, 5, 0, 0), 25));
        return registry;
    }

    private static CharacterState Armed(Guid id)
        => new(id, "Armed", "bonewarden", 1, 0,
            new BaseStats(4, 4, 4, 4, 4), 20,
            Equipment.Empty with { MainHand = "test_mace" },
            new[] { "bone_spear" }, 0);

    [Fact]
    public void EquippedBonus_ReachesTheCombatant()
    {
        var items = Items();
        var gs = new GameState(seed: 3, items: items);
        gs.Party.SetMember(0, Armed(new Guid("11111111-1111-1111-1111-111111111111")));

        gs.TriggerEncounter(new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) }));

        var armed = Assert.Single(gs.Combat!.Combatants, c => c.Name == "Armed");
        var bare = new CharacterState(Guid.NewGuid(), "Bare", "bonewarden", 1, 0,
            new BaseStats(4, 4, 4, 4, 4), 20, Equipment.Empty, new[] { "bone_spear" }, 0);

        Assert.True(armed.Power > bare.GetEffectiveStats().Power,
            $"Equipped Power {armed.Power} should exceed unequipped {bare.GetEffectiveStats().Power}.");
        Assert.True(armed.MaxHp > bare.GetEffectiveStats().MaxHp,
            $"Equipped MaxHp {armed.MaxHp} should exceed unequipped {bare.GetEffectiveStats().MaxHp}.");
    }

    [Fact]
    public void RestingAtTheInn_HealsToTheEquippedMaxHp()
    {
        var items = Items();
        var gs = new GameState(seed: 3, items: items);
        var armed = Armed(new Guid("22222222-2222-2222-2222-222222222222"));
        gs.Party.SetMember(0, armed with { CurrentHp = 1 });

        Assert.True(gs.RestAtInn());

        var rested = gs.Party.Members[0];
        Assert.Equal(armed.GetEffectiveStats(items).MaxHp, rested.CurrentHp);
    }

    [Fact]
    public void EffectiveStats_WithoutTheRegistry_StillIgnoreEquipment()
    {
        // The bonus is unresolvable without the registry, so this stays the documented behaviour of
        // the bare overload — the fix is that gameplay no longer calls it that way.
        var armed = Armed(Guid.NewGuid());

        Assert.Equal(new BaseStats(4, 4, 4, 4, 4), armed.BaseStats);
        Assert.True(armed.GetEffectiveStats(Items()).Power > armed.GetEffectiveStats().Power);
    }
}
