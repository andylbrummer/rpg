using System.Text.Json;

namespace RPC.Engine.Character;

public class ClassRegistry
{
    private readonly Dictionary<string, ClassDef> _classes = new();

    public void LoadFromJson(string id, string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var def = JsonSerializer.Deserialize<ClassDef>(json, options);
        if (def is not null)
            _classes[id] = def;
    }

    public ClassDef? Get(string id)
        => _classes.TryGetValue(id, out var def) ? def : null;

    public IEnumerable<ClassDef> All => _classes.Values;
}
