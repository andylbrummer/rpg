using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Dungeons;
using RPC.Engine.Inventory;
using RPC.Engine.Save;
using RPC.Engine.Town;

namespace RPC.Tests;

/// <summary>
/// Cross-field combined round-trip guard. Every persisted addition from this session's schema
/// growth (v9..v13, plus the content-id validation of iter14) is populated SIMULTANEOUSLY on one
/// realistic mid-play <see cref="GameState"/>, saved, loaded fresh, and asserted together. Per-field
/// tests (see <see cref="SaveGoldenFixtureTests"/>) prove each field in isolation; this test exists
/// to catch cross-field serialization interference — a field dropped or overwritten only when the
/// others are present — that single-field fixtures cannot surface.
/// </summary>
public class SaveCombinedRoundTripTests : IDisposable
{
    private const int CurrentVersion = 13; // SaveBuilder.CurrentSchemaVersion

    private readonly string _savePath;

    public SaveCombinedRoundTripTests()
    {
        _savePath = Path.Combine(Path.GetTempPath(), $"combined_roundtrip_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_savePath)) File.Delete(_savePath);
        if (File.Exists(_savePath + ".tmp")) File.Delete(_savePath + ".tmp");
    }

    [Fact]
    public void CombinedMidPlayState_RoundTrips_AllSessionAdditionsTogether()
    {
        // --- Build one realistic mid-play state populating every session addition at once. ---
        var gs = new GameState(seed: 1234);

        // Active member carrying a decaying bloom sample (v11 DungeonTurnsAlive on a ComponentStack).
        var carrier = new CharacterState(
            Guid.NewGuid(), "Kael", "bonewarden", 3, 120,
            new BaseStats(4, 3, 5, 4, 4), 17, Equipment.Empty,
            new[] { "bone_spear" }, 0,
            ComponentInventory: new[]
            {
                new ComponentStack(BloomDecaySystem.BloomSampleItemId, 1, 99, DungeonTurnsAlive: 6, Stabilized: false)
            });
        gs.Party.SetMember(0, carrier);

        // Bench roster member (v9).
        gs.Party.Bench.Add(new CharacterState(
            Guid.NewGuid(), "Mira", "stillblade", 2, 40,
            new BaseStats(3, 5, 3, 4, 4), 14, Equipment.Empty,
            Array.Empty<string>(), 0));

        // Town storage stacks (v13).
        gs.Party.TownStorage = new[]
        {
            new ComponentStack("bone_shard", 250),
            new ComponentStack("blood_vial", 40)
        };

        // Tithe state (v10).
        gs.Tithe.Debt = 2;
        gs.Tithe.BilledMilestones = new List<int> { 1, 15 };
        gs.Tithe.OutstandingSinceTurn = 1;

        // Campaign family name (v12).
        gs.Campaign.FamilyName = "Thornwick";

        // Content-id references validated on load (iter14): faction + scheme + complication.
        gs.AccusedFaction = "bureau";
        gs.CampaignConfig = new CampaignConfig
        {
            Patron = "bureau",
            Threat = "convocation",
            Mastermind = "bureau",
            WildCard = "convocation",
            Scheme = SchemeType.BloomHarvest,
            Complication = ComplicationType.BloomSiege
        };

        // --- Save (SaveBuilder) + load fresh (SaveSystem/SaveRestorer). ---
        gs.SaveGame(_savePath);

        // Schema is the current version (catches an accidental version drift in the combined path).
        using (var doc = JsonDocument.Parse(File.ReadAllText(_savePath)))
        {
            Assert.Equal(CurrentVersion, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        }

        var loaded = new GameState(seed: 9999);
        Assert.True(loaded.LoadGame(_savePath), "Combined save failed to load");

        // --- Assert every field restored together. ---

        // Town storage stacks (v13).
        Assert.Equal(2, loaded.Party.TownStorage.Length);
        Assert.Equal(250, Assert.Single(loaded.Party.TownStorage, s => s.ItemId == "bone_shard").Count);
        Assert.Equal(40, Assert.Single(loaded.Party.TownStorage, s => s.ItemId == "blood_vial").Count);

        // Tithe state (v10).
        Assert.Equal(2, loaded.Tithe.Debt);
        Assert.True(loaded.Tithe.HasDebt);
        Assert.Equal(1, loaded.Tithe.OutstandingSinceTurn);
        Assert.Equal(new[] { 1, 15 }, loaded.Tithe.BilledMilestones.OrderBy(x => x).ToArray());

        // Bench roster (v9).
        var benched = Assert.Single(loaded.Party.Bench);
        Assert.Equal("Mira", benched.Name);
        Assert.Equal("stillblade", benched.ClassId);
        Assert.Equal(2, benched.Level);

        // Campaign family name (v12).
        Assert.Equal("Thornwick", loaded.Campaign.FamilyName);

        // Bloom-sample decay counter on a ComponentStack (v11).
        var sample = Assert.Single(
            loaded.Party.Members[0].ComponentInventory,
            s => s.ItemId == BloomDecaySystem.BloomSampleItemId);
        Assert.Equal(6, sample.DungeonTurnsAlive);
        Assert.False(sample.Stabilized);

        // Content-id references survived the round-trip (iter14).
        Assert.Equal("bureau", loaded.AccusedFaction);
        Assert.NotNull(loaded.CampaignConfig);
        Assert.Equal(SchemeType.BloomHarvest, loaded.CampaignConfig!.Scheme);
        Assert.Equal(ComplicationType.BloomSiege, loaded.CampaignConfig.Complication);

        // And the restored content ids validate clean against populated registries — the iter14
        // content-reference check engaged (faction/scheme/complication all recognized).
        var rebuilt = SaveBuilder.Build(loaded);
        var warnings = SaveCompatibility.CheckContentReferences(
            rebuilt, classRegistry: null, dungeonTemplates: null,
            factionContent: FactionsWith("bureau", "convocation"),
            campaignContent: CampaignWith(new[] { "BloomHarvest" }, new[] { "BloomSiege" }));
        Assert.Empty(warnings);
    }

    // Registry builders mirror SaveContentReferenceTests so the validation lens is identical.
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
}
