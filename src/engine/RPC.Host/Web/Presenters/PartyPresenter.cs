using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Content;

namespace RPC.Host.Web.Presenters;

public class PartyPresenter
{
    private static readonly Dictionary<string, string> ClassColors = new()
    {
        ["bonewarden"] = "#8B7355",
        ["stillblade"] = "#6B8E9F",
        ["cauterist"] = "#B85C38",
        ["hollow"] = "#6B6B6B",
    };

    private readonly ClassRegistry _classRegistry;
    private readonly ItemRegistry _itemRegistry;

    public PartyPresenter(ClassRegistry classRegistry, ItemRegistry itemRegistry)
    {
        _classRegistry = classRegistry;
        _itemRegistry = itemRegistry;
    }

    public object Present(GameState state)
    {
        return state.Party.Members
            .Where(c => c.Id != Guid.Empty)
            .Select((c, i) =>
            {
                var effective = c.GetEffectiveStats(_itemRegistry);
                var classDef = _classRegistry.Get(c.ClassId);
                return new
                {
                    slot = i,
                    id = c.Id.ToString(),
                    name = c.Name,
                    classId = c.ClassId,
                    className = classDef?.Name ?? c.ClassId,
                    color = ClassColors.GetValueOrDefault(c.ClassId, "#888888"),
                    level = c.Level,
                    xp = c.Xp,
                    hp = c.CurrentHp,
                    maxHp = effective.MaxHp,
                    row = c.Row,
                    alive = c.IsAlive,
                    branchChoice = c.BranchChoice,
                    branchLevel6 = c.BranchLevel6,
                    awaitingBranchChoice = c.AwaitingBranchChoice,
                    availableBranches = c.Level < 6
                        ? (classDef?.AvailableBranches ?? classDef?.Branches?.Where(b => b.RequiresBranch == null).Select(b => b.Id).ToArray() ?? Array.Empty<string>())
                        : (classDef?.Branches?.Where(b => b.RequiresBranch == c.BranchChoice).Select(b => b.Id).ToArray() ?? Array.Empty<string>()),
                    branchWarnings = classDef?.Branches?
                        .Where(b => b.RequiresBranch != null && b.FactionGate != null)
                        .Select(b => b.RequiresBranch)
                        .Distinct()
                        .ToArray() ?? Array.Empty<string>(),
                    stats = new
                    {
                        strength = c.BaseStats.Strength,
                        dexterity = c.BaseStats.Dexterity,
                        constitution = c.BaseStats.Constitution,
                        intelligence = c.BaseStats.Intelligence,
                        willpower = c.BaseStats.Willpower,
                        maxHp = effective.MaxHp,
                        speed = effective.Speed,
                        accuracy = effective.Accuracy,
                        evade = effective.Evade,
                        power = effective.Power,
                    },
                    equipment = new
                    {
                        mainHand = c.Equipment.MainHand,
                        offHand = c.Equipment.OffHand,
                        armor = c.Equipment.Armor,
                        accessory1 = c.Equipment.Accessory1,
                        accessory2 = c.Equipment.Accessory2,
                    },
                    knownAbilities = c.KnownAbilities,
                    availableAbilities = classDef?.Abilities
                        .Where(a => a.IsAvailableInRow(c.Row))
                        .Select(a => a.Id)
                        .ToArray() ?? Array.Empty<string>(),
                    abilities = classDef?.Abilities.Select(a => new { id = a.Id, name = a.Name, branch = a.Branch }).ToArray() ?? Array.Empty<object>(),
                    tempModifiers = c.TempModifiers.Select(m => new { stat = m.Stat, delta = m.Delta, duration = m.Duration, source = m.Source }).ToArray(),
                    componentInventory = c.ComponentInventory.Select(ci => new { itemId = ci.ItemId, count = ci.Count, maxStack = ci.MaxStack }).ToArray(),
                };
            }).ToArray();
    }

    public object PresentDeadCharacters(GameState state)
    {
        return state.Party.DeadCharacters.Select(c =>
        {
            var (goldCost, titheCost, _, _) = c.ResurrectionAttempts switch
            {
                0 => (500, 1, 1, false),
                1 => (1500, 2, 2, true),
                _ => (0, 0, 0, false)
            };
            return new
            {
                id = c.Id.ToString(),
                name = c.Name,
                classId = c.ClassId,
                level = c.Level,
                resurrectionAttempts = c.ResurrectionAttempts,
                branchAdvancementLocked = c.BranchAdvancementLocked,
                resurrectionCost = goldCost,
                titheTokenCost = titheCost
            };
        }).ToArray();
    }
}
