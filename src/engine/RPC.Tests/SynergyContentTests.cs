using System.Text.Json;
using RPC.Engine.Combat;

namespace RPC.Tests;

public class SynergyContentTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true
    };

    private static string SynergyDir => "../../../../../../content/synergies";

    [Fact]
    public void AllSynergyFiles_AreValidJson()
    {
        var files = Directory.EnumerateFiles(SynergyDir, "*.json");
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var def = JsonSerializer.Deserialize<SynergyDef>(json, JsonOptions);
            Assert.NotNull(def);
        }
    }

    [Fact]
    public void AllSynergies_IdsAreUnique()
    {
        var files = Directory.EnumerateFiles(SynergyDir, "*.json").ToList();
        var ids = new HashSet<string>();

        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var def = JsonSerializer.Deserialize<SynergyDef>(json, JsonOptions);
            Assert.NotNull(def);
            Assert.True(ids.Add(def.Id), $"Duplicate synergy ID: {def.Id}");
        }
    }

    [Fact]
    public void AllSynergies_HaveExactlyTwoAbilities()
    {
        var files = Directory.EnumerateFiles(SynergyDir, "*.json");

        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var def = JsonSerializer.Deserialize<SynergyDef>(json, JsonOptions);
            Assert.NotNull(def);
            Assert.Equal(2, def.Abilities.Length);
            Assert.All(def.Abilities, a => Assert.False(string.IsNullOrWhiteSpace(a)));
        }
    }

    [Fact]
    public void AllSynergies_HintIsNonEmpty()
    {
        var files = Directory.EnumerateFiles(SynergyDir, "*.json");

        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var def = JsonSerializer.Deserialize<SynergyDef>(json, JsonOptions);
            Assert.NotNull(def);
            Assert.False(string.IsNullOrWhiteSpace(def.Hint), $"Empty hint in {Path.GetFileName(file)}");
        }
    }

    [Fact]
    public void AntiSynergy_BonewardenStillblade_IsMarkedAnti()
    {
        var path = Path.Combine(SynergyDir, "bonewarden_stillblade_anti.json");
        Assert.True(File.Exists(path), "Missing anti-synergy file");

        var json = File.ReadAllText(path);
        var def = JsonSerializer.Deserialize<SynergyDef>(json, JsonOptions);
        Assert.NotNull(def);
        Assert.True(def.Anti, "Expected anti synergy to have anti=true");
    }

    [Fact]
    public void NonAntiSynergies_AreNotMarkedAnti()
    {
        var files = Directory.EnumerateFiles(SynergyDir, "*.json")
            .Where(f => !f.EndsWith("bonewarden_stillblade_anti.json"));

        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var def = JsonSerializer.Deserialize<SynergyDef>(json, JsonOptions);
            Assert.NotNull(def);
            Assert.False(def.Anti, $"Expected {Path.GetFileName(file)} to not be anti");
        }
    }

    [Fact]
    public void SynergyCompiler_EmitsFlatMap()
    {
        var toolPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "tools", "content-pack");
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var outputDir = Path.Combine(tempDir, "output");
        Directory.CreateDirectory(outputDir);

        var contentDir = Path.GetFullPath(Path.Combine(SynergyDir, ".."));
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{toolPath}\" -- \"{contentDir}\" \"{outputDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = System.Diagnostics.Process.Start(startInfo)!;
        process.WaitForExit();
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        Assert.Equal(0, process.ExitCode);

        var mapPath = Path.Combine(outputDir, "synergies.map.json");
        Assert.True(File.Exists(mapPath), "Expected synergies.map.json to be emitted");

        var mapJson = File.ReadAllText(mapPath);
        var map = JsonSerializer.Deserialize<Dictionary<string, SynergyEffect>>(mapJson, JsonOptions);
        Assert.NotNull(map);
        Assert.NotEmpty(map);

        // Anti synergy should not appear in the compiled map
        Assert.DoesNotContain(map.Keys, k => k.Contains("bone_spear") && k.Contains("rend"));

        Directory.Delete(tempDir, recursive: true);
    }
}
