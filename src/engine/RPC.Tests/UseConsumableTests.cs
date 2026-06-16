using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Commands;
using RPC.Engine.Content;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Inventory;
using RPC.Engine.Party;

namespace RPC.Tests;

/// <summary>
/// Use-consumable-in-combat (split from inventory UI task av0AvEAlIjNU): a UseConsumable
/// command consumes 1 of the item from the actor's ComponentInventory and applies its
/// ItemEffect (heal/damage/buff) to a target combatant. Fail-fast on non-consumables or
/// items the actor does not hold — no fallback, no fabricated effect.
/// </summary>
public class UseConsumableTests
{
    // --- ConsumableSystem pure-effect tests (deterministic, flat values) ---

    private static Combatant MakeCombatant(string name, int hp, int maxHp, bool isPlayer = true)
        => new(Guid.NewGuid(), name, isPlayer, hp, maxHp, Speed: 5, Row: 0,
            StatusEffects: new List<StatusEffect>(), Power: 3);

    private static ItemDef Consumable(string id, string effectType, string value)
        => new(id, id, "", "consumable", null, "", null, 10,
            new ItemEffect(effectType, value), StackSize: 5);

    [Fact]
    public void ApplyEffect_Heal_RestoresHpCappedAtMax()
    {
        var actor = MakeCombatant("Healer", 10, 30);
        var target = MakeCombatant("Hurt", 5, 30);
        var item = Consumable("salve", "heal", "10");

        var (result, _) = ConsumableSystem.ApplyEffect(item, actor, target, new GameRandom(1));

        Assert.Equal(15, result.Hp);
    }

    [Fact]
    public void ApplyEffect_Heal_DoesNotExceedMaxHp()
    {
        var actor = MakeCombatant("Healer", 10, 30);
        var target = MakeCombatant("Nearly", 28, 30);
        var item = Consumable("salve", "heal", "10");

        var (result, _) = ConsumableSystem.ApplyEffect(item, actor, target, new GameRandom(1));

        Assert.Equal(30, result.Hp);
    }

    [Fact]
    public void ApplyEffect_Damage_ReducesTargetHpFlooredAtZero()
    {
        var actor = MakeCombatant("Thrower", 20, 20);
        var target = MakeCombatant("Goblin", 4, 12, isPlayer: false);
        var item = Consumable("bomb", "damage", "10");

        var (result, _) = ConsumableSystem.ApplyEffect(item, actor, target, new GameRandom(1));

        Assert.Equal(0, result.Hp);
    }

    [Fact]
    public void ApplyEffect_Buff_AddsStatusEffect()
    {
        var actor = MakeCombatant("Drinker", 20, 20);
        var target = MakeCombatant("Ally", 20, 20);
        var item = Consumable("tonic", "buff", "fortified:3:2");

        var (result, _) = ConsumableSystem.ApplyEffect(item, actor, target, new GameRandom(1));

        var status = Assert.Single(result.StatusEffects, s => s.Type == "fortified");
        Assert.Equal(3, status.Duration);
        Assert.Equal(2, status.Potency);
    }

    [Fact]
    public void ApplyEffect_NoEffect_Throws()
    {
        var actor = MakeCombatant("A", 20, 20);
        var target = MakeCombatant("B", 20, 20);
        var item = new ItemDef("inert", "Inert", "", "consumable", null, "", null, 1, Effect: null);

        Assert.Throws<InvalidOperationException>(
            () => ConsumableSystem.ApplyEffect(item, actor, target, new GameRandom(1)));
    }

    // --- GameCommandHandler end-to-end (inventory consumption + validation) ---

    private static CharacterState MakeChar(string name, int currentHp, params ComponentStack[] inventory)
    {
        var stats = new BaseStats(4, 5, 10, 4, 4);
        return new CharacterState(
            Guid.NewGuid(), name, "test", 1, 0, stats, currentHp,
            Equipment.Empty, Array.Empty<string>(), 0)
        {
            ComponentInventory = inventory
        };
    }

    private static (GameState gs, GameCommandHandler handler, ItemRegistry items) SetupCombat(CharacterState member)
    {
        var items = new ItemRegistry();
        items.Register(Consumable("salve", "heal", "10"));
        items.Register(new ItemDef("sword", "Sword", "", "weapon", "mainHand", "", null, 50));

        var gs = new GameState(seed: 42);
        gs.EnterDungeon(new Dungeon(3, 3, "test"), "test");
        gs.Party.SetMember(0, member);
        gs.TriggerEncounter(new EncounterDef("e", "Test", new[] { new EnemySpawn("rat", 1) }, 0));

        var handler = new GameCommandHandler(gs, new StubDungeonGenerator(), items);
        return (gs, handler, items);
    }

    private static UseConsumableCommand UseCmd(Guid actorId, string itemId, Guid? targetId = null)
        => new(actorId, itemId, targetId ?? actorId);

    [Fact]
    public void UseConsumable_Heal_AppliesEffectAndConsumesOne()
    {
        var member = MakeChar("Hero", currentHp: 5, new ComponentStack("salve", 3));
        var (gs, handler, _) = SetupCombat(member);
        Assert.Equal(GameMode.Combat, gs.Mode);

        var actor = gs.Combat!.Combatants.First(c => c.IsPlayer);
        var hpBefore = actor.Hp;

        var result = handler.Execute(UseCmd(actor.Id, "salve"));

        Assert.True(result.StateChanged);
        var after = gs.Combat!.Combatants.First(c => c.Id == actor.Id);
        Assert.Equal(Math.Min(actor.MaxHp, hpBefore + 10), after.Hp);
        Assert.Equal(2, ComponentInventorySystem.GetComponentCount(
            gs.Party.Members[0].ComponentInventory, "salve"));
    }

    [Fact]
    public void UseConsumable_LastInStack_RemovesStack()
    {
        var member = MakeChar("Hero", currentHp: 5, new ComponentStack("salve", 1));
        var (gs, handler, _) = SetupCombat(member);

        var actor = gs.Combat!.Combatants.First(c => c.IsPlayer);
        handler.Execute(UseCmd(actor.Id, "salve"));

        Assert.Empty(gs.Party.Members[0].ComponentInventory);
    }

    [Fact]
    public void UseConsumable_NonConsumable_Rejected()
    {
        var member = MakeChar("Hero", currentHp: 5, new ComponentStack("sword", 1));
        var (gs, handler, _) = SetupCombat(member);

        var actor = gs.Combat!.Combatants.First(c => c.IsPlayer);

        Assert.Throws<InvalidOperationException>(() => handler.Execute(UseCmd(actor.Id, "sword")));
    }

    [Fact]
    public void UseConsumable_ActorDoesNotHoldItem_Rejected()
    {
        var member = MakeChar("Hero", currentHp: 5); // empty inventory
        var (gs, handler, _) = SetupCombat(member);

        var actor = gs.Combat!.Combatants.First(c => c.IsPlayer);

        Assert.Throws<InvalidOperationException>(() => handler.Execute(UseCmd(actor.Id, "salve")));
    }

    [Fact]
    public void Dispatcher_Parses_UseConsumable()
    {
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var action = new CombatAction(actorId, ActionType.UseItem, targetId, null, "salve");

        var cmd = CommandDispatcher.Parse(new PlayerAction { Type = "use_consumable", Action = action });

        var use = Assert.IsType<UseConsumableCommand>(cmd);
        Assert.Equal(actorId, use.ActorId);
        Assert.Equal("salve", use.ItemId);
        Assert.Equal(targetId, use.TargetId);
    }
}
