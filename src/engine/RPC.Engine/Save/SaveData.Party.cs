using RPC.Engine.Character;

namespace RPC.Engine.Save;

public class SavePartyMember
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string ClassId { get; set; } = "";
    public int Level { get; set; }
    public int Xp { get; set; }
    public BaseStats BaseStats { get; set; }
    public int CurrentHp { get; set; }
    public Equipment Equipment { get; set; }
    public string[] KnownAbilities { get; set; } = Array.Empty<string>();
    public int Row { get; set; }
    public string? BranchChoice { get; set; }
    public string? BranchLevel6 { get; set; }
    public TempStatModifier[] TempModifiers { get; set; } = Array.Empty<TempStatModifier>();
    public int ResurrectionAttempts { get; set; } = 0;
    public bool BranchAdvancementLocked { get; set; } = false;
    public SaveComponentStack[] ComponentInventory { get; set; } = Array.Empty<SaveComponentStack>();
}

public class SaveComponentStack
{
    public string ItemId { get; set; } = "";
    public int Count { get; set; }
    public int MaxStack { get; set; } = 99;
}
