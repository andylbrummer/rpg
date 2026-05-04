using System.Text.Json;
using System.Text.Json.Serialization;
using RPC.Engine.Character;

namespace RPC.Tests;

public class ContentValidationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true
    };

    [Theory]
    [InlineData("bonewarden")]
    [InlineData("stillblade")]
    [InlineData("cauterist")]
    [InlineData("hollow")]
    public void ClassJson_IsValid(string classId)
    {
        var path = $"../../../../../../content/classes/{classId}.json";
        Assert.True(File.Exists(path), $"Missing class file: {path}");

        var json = File.ReadAllText(path);
        var classDef = JsonSerializer.Deserialize<ClassDef>(json, JsonOptions);

        Assert.NotNull(classDef);
        Assert.Equal(classId, classDef.Id);
        Assert.NotEmpty(classDef.Name);
        Assert.NotEmpty(classDef.Description);
        Assert.True(classDef.BaseStats.Constitution > 0);
        Assert.InRange(classDef.Abilities.Length, 3, 6);
        Assert.All(classDef.Abilities, a =>
        {
            Assert.False(string.IsNullOrEmpty(a.Id));
            Assert.False(string.IsNullOrEmpty(a.Name));
            Assert.NotEmpty(a.Tags);
        });
        Assert.All(classDef.Abilities, a =>
            Assert.Single(classDef.Abilities, x => x.Id == a.Id)); // unique IDs
        Assert.InRange(classDef.LevelTable.Length, 5, 10);
        Assert.Contains(classDef.LevelTable, e => e.Level == 1);
    }

    [Fact]
    public void AllClasses_AbilityIdsAreGloballyUnique()
    {
        var classIds = new[] { "bonewarden", "stillblade", "cauterist", "hollow" };
        var allAbilityIds = new HashSet<string>();

        foreach (var classId in classIds)
        {
            var path = $"../../../../../../content/classes/{classId}.json";
            var json = File.ReadAllText(path);
            var classDef = JsonSerializer.Deserialize<ClassDef>(json, JsonOptions);
            Assert.NotNull(classDef);

            foreach (var ability in classDef.Abilities)
            {
                Assert.True(allAbilityIds.Add(ability.Id),
                    $"Duplicate ability ID: {ability.Id} in {classId}");
            }
        }
    }
}
