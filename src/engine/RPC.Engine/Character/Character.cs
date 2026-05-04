namespace RPC.Engine.Character;

public class Character
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ClassId { get; set; } = "";
    public int Level { get; set; }
    public int XP { get; set; }
    
    // Stats
    public int Strength { get; set; }
    public int Dexterity { get; set; }
    public int Constitution { get; set; }
    public int Intelligence { get; set; }
    public int Willpower { get; set; }
    
    // Derived
    public int MaxHP => Constitution * 3 + Level * 2;
    public int HP { get; set; }
    public int Speed => Dexterity + (Level / 2);
    
    // Equipment
    public EquipmentSlot MainHand { get; set; } = new();
    public EquipmentSlot OffHand { get; set; } = new();
    public EquipmentSlot Armor { get; set; } = new();
    public EquipmentSlot Accessory1 { get; set; } = new();
    public EquipmentSlot Accessory2 { get; set; } = new();
    
    // Inventory
    public List<ItemStack> Inventory { get; } = new();
    public int MaxInventorySize { get; set; } = 20;
    
    // Row assignment (0 = front, 1 = back)
    public int Row { get; set; }
}

public class EquipmentSlot
{
    public string? ItemId { get; set; }
    public bool IsEquipped => ItemId != null;
}

public class ItemStack
{
    public string ItemId { get; set; } = "";
    public int Count { get; set; }
}

public class ClassDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int BaseStrength { get; set; }
    public int BaseDexterity { get; set; }
    public int BaseConstitution { get; set; }
    public int BaseIntelligence { get; set; }
    public int BaseWillpower { get; set; }
    public List<string> StartingAbilities { get; set; } = new();
    public List<LevelUpEntry> LevelUpTable { get; set; } = new();
}

public class LevelUpEntry
{
    public int Level { get; set; }
    public int HPBonus { get; set; }
    public List<string> NewAbilities { get; set; } = new();
    public List<StatBonus> StatBonuses { get; set; } = new();
}

public class StatBonus
{
    public string Stat { get; set; } = "";
    public int Amount { get; set; }
}
