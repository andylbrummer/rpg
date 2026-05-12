using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Content;
using RPC.Engine.Town;

namespace RPC.Tests;

public class TownStateTests : IDisposable
{
    private readonly string _testSavePath;

    public TownStateTests()
    {
        _testSavePath = Path.Combine(Path.GetTempPath(), $"test_town_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testSavePath))
            File.Delete(_testSavePath);
    }

    [Fact]
    public void TavernRecruitGenerator_SameSeed_SameRoster()
    {
        var roster1 = TavernRecruitGenerator.GenerateRoster(12345);
        var roster2 = TavernRecruitGenerator.GenerateRoster(12345);

        Assert.Equal(6, roster1.Count);
        Assert.Equal(6, roster2.Count);

        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(roster1[i].Id, roster2[i].Id);
            Assert.Equal(roster1[i].Name, roster2[i].Name);
            Assert.Equal(roster1[i].ClassId, roster2[i].ClassId);
            Assert.Equal(roster1[i].Level, roster2[i].Level);
            Assert.Equal(roster1[i].Cost, roster2[i].Cost);
            Assert.Equal(roster1[i].BaseStats, roster2[i].BaseStats);
        }
    }

    [Fact]
    public void TavernRecruitGenerator_DifferentSeed_DifferentRoster()
    {
        var roster1 = TavernRecruitGenerator.GenerateRoster(12345);
        var roster2 = TavernRecruitGenerator.GenerateRoster(54321);

        Assert.Equal(6, roster1.Count);
        Assert.Equal(6, roster2.Count);

        var allSame = true;
        for (int i = 0; i < 6; i++)
        {
            if (roster1[i].Id != roster2[i].Id)
            {
                allSame = false;
                break;
            }
        }
        Assert.False(allSame, "Different seeds should produce different rosters");
    }

    [Fact]
    public void GameState_InitializesTown_WithRoster()
    {
        var gs = new GameState(seed: 42);

        Assert.NotNull(gs.Town);
        Assert.Equal("the_reach", gs.Town.CurrentTownId);
        Assert.Equal(6, gs.Town.TavernRoster.Count);
        Assert.True(gs.Town.AvailableMissions.Count >= 4, "Town should initialize with at least 4 available missions");
        Assert.Empty(gs.Town.VendorStock);
        Assert.True(gs.Town.FactionContacts.Count >= 2, "Town should initialize with at least 2 faction contacts");
    }

    [Fact]
    public void SaveSystem_TownRoundTrip_PreservesRoster()
    {
        var gs = new GameState(seed: 99);
        var originalRoster = gs.Town.TavernRoster;
        gs.Town.ViewedMissions.Add("mission_1");

        gs.SaveGame(_testSavePath);
        Assert.True(File.Exists(_testSavePath));

        var gs2 = new GameState(seed: 9999);
        var loaded = gs2.LoadGame(_testSavePath);
        Assert.True(loaded);

        Assert.Equal(gs.Town.CurrentTownId, gs2.Town.CurrentTownId);
        Assert.Equal(originalRoster.Count, gs2.Town.TavernRoster.Count);

        for (int i = 0; i < originalRoster.Count; i++)
        {
            Assert.Equal(originalRoster[i].Id, gs2.Town.TavernRoster[i].Id);
            Assert.Equal(originalRoster[i].Name, gs2.Town.TavernRoster[i].Name);
            Assert.Equal(originalRoster[i].ClassId, gs2.Town.TavernRoster[i].ClassId);
            Assert.Equal(originalRoster[i].Level, gs2.Town.TavernRoster[i].Level);
            Assert.Equal(originalRoster[i].Cost, gs2.Town.TavernRoster[i].Cost);
        }

        Assert.Single(gs2.Town.ViewedMissions);
        Assert.Contains("mission_1", gs2.Town.ViewedMissions);
    }

    [Fact]
    public void SaveSystem_TownRoundTrip_PreservesEmptyCollections()
    {
        var gs = new GameState(seed: 77);
        gs.SaveGame(_testSavePath);

        var gs2 = new GameState(seed: 88);
        var loaded = gs2.LoadGame(_testSavePath);
        Assert.True(loaded);

        Assert.Equal(gs.Town.AvailableMissions.Count, gs2.Town.AvailableMissions.Count);
        Assert.Empty(gs2.Town.VendorStock);
        Assert.Equal(gs.Town.FactionContacts.Count, gs2.Town.FactionContacts.Count);
    }

    [Fact]
    public void RecruitFromTavern_AddsToParty_RemovesFromRoster()
    {
        var gs = new GameState(seed: 42);
        var recruit = gs.Town.TavernRoster[0];
        var rosterCount = gs.Town.TavernRoster.Count;

        // Clear a party slot first
        gs.Party.SetMember(3, default);

        var result = gs.RecruitFromTavern(recruit.Id);
        Assert.True(result);

        Assert.Equal(rosterCount - 1, gs.Town.TavernRoster.Count);
        Assert.DoesNotContain(gs.Town.TavernRoster, r => r.Id == recruit.Id);
        Assert.Contains(gs.Party.Members, m => m.Name == recruit.Name);
    }

    [Fact]
    public void RecruitFromTavern_InvalidId_ReturnsFalse()
    {
        var gs = new GameState(seed: 42);
        var result = gs.RecruitFromTavern("nonexistent");
        Assert.False(result);
    }

    [Fact]
    public void RecruitFromTavern_FullParty_ReturnsFalse()
    {
        var gs = new GameState(seed: 42);
        var recruit = gs.Town.TavernRoster[0];
        var result = gs.RecruitFromTavern(recruit.Id);
        Assert.False(result);
    }

    [Fact]
    public void AcceptMission_RemovesFromAvailable()
    {
        var gs = new GameState(seed: 1);
        var initialCount = gs.Town.AvailableMissions.Count;
        var mission = gs.Town.AvailableMissions[0];

        var result = gs.AcceptMission(mission.Id);
        Assert.True(result);
        Assert.Equal(initialCount - 1, gs.Town.AvailableMissions.Count);
        Assert.Single(gs.Town.QuestLog);
        Assert.Equal(mission.Id, gs.Town.QuestLog[0].Id);
        Assert.Equal("active", gs.Town.QuestLog[0].Status);
    }

    [Fact]
    public void AcceptMission_InvalidId_ReturnsFalse()
    {
        var gs = new GameState(seed: 1);
        var result = gs.AcceptMission("nonexistent");
        Assert.False(result);
    }

    [Fact]
    public void PurchaseVendorItem_RemovesFromStock()
    {
        var gs = new GameState(seed: 1);
        gs.Town.VendorStock.Add(new VendorItem("i1", "Potion", 10, 3));

        var result = gs.PurchaseVendorItem("i1");
        Assert.True(result);
        Assert.Empty(gs.Town.VendorStock);
    }

    [Fact]
    public void PurchaseVendorItem_InvalidId_ReturnsFalse()
    {
        var gs = new GameState(seed: 1);
        var result = gs.PurchaseVendorItem("nonexistent");
        Assert.False(result);
    }

    [Fact]
    public void FactionContentJson_LoadsWithoutErrors()
    {
        var defs = FactionContentLoader.LoadAll("../../../../../../content/factions");

        Assert.Equal(5, defs.Count);
        Assert.Contains(defs, d => d.Id == "bureau");
        Assert.Contains(defs, d => d.Id == "convocation");
        Assert.Contains(defs, d => d.Id == "stillness");
        Assert.Contains(defs, d => d.Id == "inkblood");
        Assert.Contains(defs, d => d.Id == "cartography");
    }

    [Fact]
    public void FactionContent_Bureau_HasEightVendorItems()
    {
        var defs = FactionContentLoader.LoadAll("../../../../../../content/factions");
        var bureau = defs.First(d => d.Id == "bureau");

        Assert.Equal(8, bureau.VendorStock.Count);
        Assert.All(bureau.VendorStock, item =>
        {
            Assert.False(string.IsNullOrEmpty(item.ItemId));
            Assert.True(item.Price >= 0);
            Assert.True(item.Quantity >= 1);
        });
    }

    [Fact]
    public void FactionContent_Convocation_HasEightVendorItems()
    {
        var defs = FactionContentLoader.LoadAll("../../../../../../content/factions");
        var convocation = defs.First(d => d.Id == "convocation");

        Assert.Equal(8, convocation.VendorStock.Count);
        Assert.All(convocation.VendorStock, item =>
        {
            Assert.False(string.IsNullOrEmpty(item.ItemId));
            Assert.True(item.Price >= 0);
            Assert.True(item.Quantity >= 1);
        });
    }

    [Fact]
    public void FactionContent_VendorItems_ReferenceExistingItemIds()
    {
        var defs = FactionContentLoader.LoadAll("../../../../../../content/factions");
        var registry = new ItemRegistry();
        var categories = new[] { "weapons", "armor", "consumables", "components" };
        foreach (var category in categories)
        {
            var path = $"../../../../../../content/items/{category}.json";
            if (!File.Exists(path)) continue;
            var json = File.ReadAllText(path);
            var items = JsonSerializer.Deserialize<ItemDef[]>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            if (items != null)
            {
                foreach (var item in items)
                    registry.Register(item);
            }
        }

        foreach (var def in defs)
        {
            foreach (var stock in def.VendorStock)
            {
                Assert.True(registry.Contains(stock.ItemId), $"Faction {def.Id} references unknown item: {stock.ItemId}");
            }
        }
    }

    [Fact]
    public void FactionContent_Missions_AreSideMissions_WithPlusFiveRep()
    {
        var defs = FactionContentLoader.LoadAll("../../../../../../content/factions");
        foreach (var def in defs)
        {
            Assert.True(def.Missions.Count >= 2, $"Faction {def.Id} should have at least 2 missions");
            Assert.All(def.Missions, m => Assert.True(m.RepReward >= 5, $"Mission {m.Id} rep reward should be >= 5"));
        }
    }

    [Fact]
    public void FactionContent_RepThresholds_MatchDesign()
    {
        var defs = FactionContentLoader.LoadAll("../../../../../../content/factions");
        foreach (var def in defs)
        {
            // Stillness is harder to access due to negative starter reputation
            var expectedVendor = def.Id == "stillness" ? 20 : 25;
            Assert.Equal(expectedVendor, def.RepThresholds.VendorAccess);
            Assert.Equal(50, def.RepThresholds.ExclusiveRecruit);
            Assert.Equal(75, def.RepThresholds.PatronOffice);
        }
    }

    [Fact]
    public void FactionContent_IdentityString_IsPresent()
    {
        var defs = FactionContentLoader.LoadAll("../../../../../../content/factions");
        foreach (var def in defs)
        {
            Assert.False(string.IsNullOrWhiteSpace(def.Identity));
            Assert.True(def.Identity.Length > 200, $"Faction {def.Id} identity should be 3-4 paragraphs");
        }
    }

    [Fact]
    public void GameState_InitializesTown_WithFactionContentLoaded()
    {
        var factionContent = FactionContentLoader.LoadAll("../../../../../../content/factions");
        FactionContactGenerator.SetContent(factionContent);
        FactionVendorGenerator.SetContent(factionContent);

        var gs = new GameState(seed: 42);

        Assert.Equal(5, gs.Town.FactionContacts.Count);
        Assert.Equal(20, gs.Town.AvailableMissions.Count);
        Assert.Equal(5, gs.Town.FactionVendors.Count);
        Assert.Equal(8, gs.Town.FactionVendors.First(v => v.FactionId == "bureau").Stock.Count);
        Assert.Equal(8, gs.Town.FactionVendors.First(v => v.FactionId == "convocation").Stock.Count);
        Assert.Equal(8, gs.Town.FactionVendors.First(v => v.FactionId == "stillness").Stock.Count);
        Assert.Equal(8, gs.Town.FactionVendors.First(v => v.FactionId == "inkblood").Stock.Count);
        Assert.Equal(8, gs.Town.FactionVendors.First(v => v.FactionId == "cartography").Stock.Count);
    }
}
