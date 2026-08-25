namespace RPC.Engine.Content;

using RPC.Engine.Character;

public record ItemDef(
    string Id,
    string Name,
    string Description,
    string Type, // weapon, armor, consumable, component
    string? Slot, // mainHand, offHand, head, body, accessory
    string Icon, // base64 placeholder or URL
    BaseStats? StatBonus,
    int Value,
    ItemEffect? Effect = null,
    int StackSize = 1);

public record ItemEffect(
    string Type, // heal, damage, buff, etc.
    string Value,
    string? Target = null);
