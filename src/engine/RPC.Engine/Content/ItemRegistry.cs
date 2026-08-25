namespace RPC.Engine.Content;

public class ItemRegistry
{
    private readonly Dictionary<string, ItemDef> _items = new();

    public void Register(ItemDef item) => _items[item.Id] = item;

    public ItemDef? Get(string id) => _items.TryGetValue(id, out var item) ? item : null;

    public bool Contains(string id) => _items.ContainsKey(id);
}
