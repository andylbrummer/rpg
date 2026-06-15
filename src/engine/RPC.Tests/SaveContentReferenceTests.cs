using RPC.Engine.Character;
using RPC.Engine.Dungeons;
using RPC.Engine.Save;

namespace RPC.Tests;

/// <summary>
/// Unit tests for <see cref="SaveCompatibility.CheckContentReferences"/> — content-id validation
/// on load. Pure logic, no filesystem: validates referenced class ids and dungeon template ids
/// against supplied registries, fail-open when a registry is absent/empty.
/// </summary>
public class SaveContentReferenceTests
{
    private static DungeonTemplate Template(string id) =>
        new(id, id, Array.Empty<string>(), Array.Empty<string>(), 1, "boss", "table");

    private static ClassRegistry RegistryWith(params string[] classIds)
    {
        var reg = new ClassRegistry();
        foreach (var id in classIds)
            reg.LoadFromJson(id, $"{{\"name\":\"{id}\"}}");
        return reg;
    }

    [Fact]
    public void NullRegistries_ProduceNoWarnings_FailOpen()
    {
        var data = new SaveData
        {
            Party = new SavePartyMember?[] { new() { ClassId = "ghost_class" } },
            DungeonType = "unknown_dungeon"
        };

        var warnings = SaveCompatibility.CheckContentReferences(data, classRegistry: null, dungeonTemplates: null);

        Assert.Empty(warnings);
    }

    [Fact]
    public void EmptyRegistries_ProduceNoWarnings_FailOpen()
    {
        var data = new SaveData
        {
            Party = new SavePartyMember?[] { new() { ClassId = "ghost_class" } },
            DungeonType = "unknown_dungeon"
        };

        var warnings = SaveCompatibility.CheckContentReferences(
            data, new ClassRegistry(), new Dictionary<string, DungeonTemplate>());

        Assert.Empty(warnings);
    }

    [Fact]
    public void UnknownClassId_WithPopulatedRegistry_Warns()
    {
        var data = new SaveData
        {
            Party = new SavePartyMember?[] { new() { ClassId = "ghost_class" } }
        };

        var warnings = SaveCompatibility.CheckContentReferences(
            data, RegistryWith("bonewarden"), dungeonTemplates: null);

        Assert.Single(warnings);
        Assert.Contains("ghost_class", warnings[0]);
    }

    [Fact]
    public void KnownClassId_WithPopulatedRegistry_NoWarning()
    {
        var data = new SaveData
        {
            Party = new SavePartyMember?[] { new() { ClassId = "bonewarden" } },
            DeadCharacters = new[] { new SavePartyMember { ClassId = "bonewarden" } }
        };

        var warnings = SaveCompatibility.CheckContentReferences(
            data, RegistryWith("bonewarden"), dungeonTemplates: null);

        Assert.Empty(warnings);
    }

    [Fact]
    public void UnknownDungeonType_WithPopulatedTemplates_Warns()
    {
        var data = new SaveData { DungeonType = "unknown_dungeon" };
        var templates = new Dictionary<string, DungeonTemplate> { ["crypt"] = Template("crypt") };

        var warnings = SaveCompatibility.CheckContentReferences(data, classRegistry: null, templates);

        Assert.Single(warnings);
        Assert.Contains("unknown_dungeon", warnings[0]);
    }

    [Fact]
    public void UnknownOverworldTemplateId_WithPopulatedTemplates_Warns()
    {
        var data = new SaveData
        {
            DungeonType = "crypt",
            OverworldNodes = new[]
            {
                new SaveOverworldNode { Id = "node_x", DungeonTemplateId = "phantom_template" }
            }
        };
        var templates = new Dictionary<string, DungeonTemplate> { ["crypt"] = Template("crypt") };

        var warnings = SaveCompatibility.CheckContentReferences(data, classRegistry: null, templates);

        Assert.Single(warnings);
        Assert.Contains("phantom_template", warnings[0]);
    }
}
