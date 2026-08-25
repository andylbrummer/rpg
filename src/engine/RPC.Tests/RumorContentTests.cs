using System.Linq;
using System.Text.Json;
using RPC.Engine.Combat;
using RPC.Engine.Content;
using RPC.Engine.Town;

namespace RPC.Tests;

public class RumorContentTests
{
    private static string Dir => "../../../../../../content/rumors";

    private static List<RumorDef> LoadAll()
    {
        var all = new List<RumorDef>();
        foreach (var f in Directory.EnumerateFiles(Dir, "*.json"))
        {
            var defs = JsonSerializer.Deserialize<List<RumorDef>>(File.ReadAllText(f), ContentJsonOptions.Standard);
            if (defs != null) all.AddRange(defs);
        }
        return all;
    }

    [Fact]
    public void Library_HasAtLeastFiftyRumors()
    {
        Assert.True(LoadAll().Count >= 50, $"Expected >=50 rumors, found {LoadAll().Count}");
    }

    [Fact]
    public void AllRumors_HaveUniqueIds_AndNonEmptyText()
    {
        var ids = new HashSet<string>();
        foreach (var r in LoadAll())
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Id));
            Assert.False(string.IsNullOrWhiteSpace(r.Text));
            Assert.True(ids.Add(r.Id), $"Duplicate rumor id: {r.Id}");
        }
    }

    [Fact]
    public void AllRumors_CarryAHiddenDialogueTag()
    {
        foreach (var r in LoadAll())
            Assert.False(string.IsNullOrWhiteSpace(r.HiddenTag), $"Rumor {r.Id} is missing a hiddenTag");
    }

    [Fact]
    public void AllTruthCategories_AreRepresented()
    {
        var statuses = LoadAll().Select(r => r.TruthStatus).Distinct().ToList();
        Assert.Contains(RumorTruthStatus.True, statuses);
        Assert.Contains(RumorTruthStatus.Planted, statuses);
        Assert.Contains(RumorTruthStatus.Outdated, statuses);
    }

    [Fact]
    public void GenerateForVisit_CarriesHiddenTagInto_TownRumor()
    {
        var repo = new RumorRepository(new List<RumorDef>
        {
            new("r1", "A whisper.", RumorTruthStatus.True, null, "bureau", "engine_crisis")
        });

        var visit = repo.GenerateForVisit(new GameRandom(1), 3);
        Assert.All(visit, tr => Assert.Equal("engine_crisis", tr.HiddenTag));
    }

    [Fact]
    public void GetRumorsByHiddenTag_FiltersToMatchingTag()
    {
        var repo = new RumorRepository(new List<RumorDef>
        {
            new("a", "x", RumorTruthStatus.True, null, null, "engine_crisis"),
            new("b", "y", RumorTruthStatus.Planted, null, null, "smuggling"),
            new("c", "z", RumorTruthStatus.True, null, null, "engine_crisis"),
        });

        var matches = repo.GetRumorsByHiddenTag("engine_crisis");
        Assert.Equal(2, matches.Count);
        Assert.DoesNotContain(matches, r => r.Id == "b");
    }
}
