using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Dungeons;
using RPC.Engine.Save;
using RPC.Engine.Town;

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

    private static FactionContentRepository FactionsWith(params string[] factionIds) =>
        new(factionIds.Select(id => new FactionContentDef(
            id, id, id,
            new FactionContactDef($"contact-{id}", $"Agent {id}", "portrait"),
            "Vendor", "identity", "#fff", 0,
            new RepThresholdsDef(25, 50, 75),
            new List<VendorItem>(),
            new List<FactionMissionDef>())).ToList());

    private static CampaignContentRegistry CampaignWith(string[] schemeIds, string[] complicationIds) =>
        new(
            schemeIds.Select(id => new SchemeDef(
                id, id, "", "", Array.Empty<string>(), Array.Empty<CampaignEventDef>())).ToList(),
            complicationIds.Select(id => new ComplicationDef(
                id, id, "", new WorldStateModifiers(), Array.Empty<CampaignEventDef>())).ToList());

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

    // --- Faction / scheme / complication content-id validation ---

    [Fact]
    public void NullFactionAndCampaignRegistries_ProduceNoWarnings_FailOpen()
    {
        var data = new SaveData
        {
            AccusedFaction = "phantom_faction",
            CampaignConfig = new SaveCampaignConfig { Scheme = "GhostScheme", Complication = "GhostComplication" }
        };

        var warnings = SaveCompatibility.CheckContentReferences(
            data, classRegistry: null, dungeonTemplates: null, factionContent: null, campaignContent: null);

        Assert.Empty(warnings);
    }

    [Fact]
    public void EmptyFactionRepository_ProducesNoWarnings_FailOpen()
    {
        var data = new SaveData { AccusedFaction = "phantom_faction" };

        var warnings = SaveCompatibility.CheckContentReferences(
            data, classRegistry: null, dungeonTemplates: null, factionContent: FactionsWith());

        Assert.Empty(warnings);
    }

    [Fact]
    public void KnownFactionSchemeComplicationIds_ProduceNoWarnings()
    {
        var data = new SaveData
        {
            AccusedFaction = "bureau",
            Reputation = new Dictionary<string, int> { ["convocation"] = 10 },
            Evidence = new Dictionary<string, int> { ["bureau"] = 2 },
            CampaignConfig = new SaveCampaignConfig
            {
                Patron = "bureau",
                Threat = "convocation",
                Mastermind = "bureau",
                WildCard = "convocation",
                Scheme = "BloomHarvest",
                Complication = "BloomSiege",
                WildcardTrigger = new SaveWildcardTrigger { FactionId = "convocation", TurnThreshold = 20 },
                FactionTimelines = new Dictionary<string, SaveFactionTimeline> { ["bureau"] = new() }
            }
        };

        var warnings = SaveCompatibility.CheckContentReferences(
            data, classRegistry: null, dungeonTemplates: null,
            factionContent: FactionsWith("bureau", "convocation"),
            campaignContent: CampaignWith(new[] { "BloomHarvest" }, new[] { "BloomSiege" }));

        Assert.Empty(warnings);
    }

    [Fact]
    public void UnknownFactionId_WithPopulatedRepository_Warns()
    {
        var data = new SaveData { AccusedFaction = "phantom_faction" };

        var warnings = SaveCompatibility.CheckContentReferences(
            data, classRegistry: null, dungeonTemplates: null,
            factionContent: FactionsWith("bureau", "convocation"));

        Assert.Single(warnings);
        Assert.Contains("phantom_faction", warnings[0]);
    }

    [Fact]
    public void UnknownSchemeId_WithPopulatedRegistry_Warns()
    {
        var data = new SaveData
        {
            CampaignConfig = new SaveCampaignConfig { Scheme = "GhostScheme" }
        };

        var warnings = SaveCompatibility.CheckContentReferences(
            data, classRegistry: null, dungeonTemplates: null,
            campaignContent: CampaignWith(new[] { "BloomHarvest" }, Array.Empty<string>()));

        Assert.Single(warnings);
        Assert.Contains("GhostScheme", warnings[0]);
    }

    [Fact]
    public void UnknownComplicationId_WithPopulatedRegistry_Warns()
    {
        var data = new SaveData
        {
            CampaignConfig = new SaveCampaignConfig { Complication = "GhostComplication" }
        };

        var warnings = SaveCompatibility.CheckContentReferences(
            data, classRegistry: null, dungeonTemplates: null,
            campaignContent: CampaignWith(Array.Empty<string>(), new[] { "BloomSiege" }));

        Assert.Single(warnings);
        Assert.Contains("GhostComplication", warnings[0]);
    }

    [Fact]
    public void UnknownFactionScheme_StillLoadsSave_WarningOnly()
    {
        // A save with unknown content ids remains loadable: validation only surfaces warnings.
        var data = new SaveData
        {
            AccusedFaction = "phantom_faction",
            CampaignConfig = new SaveCampaignConfig { Scheme = "GhostScheme" }
        };

        var warnings = SaveCompatibility.CheckContentReferences(
            data, classRegistry: null, dungeonTemplates: null,
            factionContent: FactionsWith("bureau"),
            campaignContent: CampaignWith(new[] { "BloomHarvest" }, Array.Empty<string>()));

        Assert.Equal(2, warnings.Count);
        Assert.All(warnings, w => Assert.Contains("unknown", w));
    }
}
