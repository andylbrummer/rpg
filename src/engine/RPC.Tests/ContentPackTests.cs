using System.Text;
using System.Text.Json;
using RPC.Content;

namespace RPC.Tests;

public class ContentPackTests
{
    [Fact]
    public void ContentPackReader_RoundTrip()
    {
        var path1 = "classes/bonewarden.json";
        var data1 = Encoding.UTF8.GetBytes("{\"id\":\"bonewarden\"}");
        var path2 = "items/weapons.json";
        var data2 = Encoding.UTF8.GetBytes("[{\"id\":\"sword\"}]");

        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RPK1"));
            writer.Write(1u);
            writer.Write(2u);

            var path1Bytes = Encoding.UTF8.GetBytes(path1);
            var path2Bytes = Encoding.UTF8.GetBytes(path2);

            // Entry 1
            var offset1 = (uint)(stream.Position + 4 + 4 + 2 + path1Bytes.Length);
            writer.Write(offset1);
            writer.Write((uint)data1.Length);
            writer.Write((ushort)path1Bytes.Length);
            writer.Write(path1Bytes);
            writer.Write(data1);

            // Entry 2
            var offset2 = (uint)(stream.Position + 4 + 4 + 2 + path2Bytes.Length);
            writer.Write(offset2);
            writer.Write((uint)data2.Length);
            writer.Write((ushort)path2Bytes.Length);
            writer.Write(path2Bytes);
            writer.Write(data2);
        }

        var reader = new ContentPackReader();
        reader.Read(stream.ToArray());

        Assert.True(reader.IsLoaded);
        Assert.True(reader.Contains(path1));
        Assert.True(reader.Contains(path2));
        Assert.Equal("{\"id\":\"bonewarden\"}", reader.GetString(path1));
        Assert.Equal("[{\"id\":\"sword\"}]", reader.GetString(path2));
    }

    [Fact]
    public void ContentPackReader_InvalidMagic_Throws()
    {
        var reader = new ContentPackReader();
        Assert.Throws<InvalidDataException>(() => reader.Read(new byte[] { 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0 }));
    }

    [Fact]
    public void ContentPackCompiler_ProducesValidRpk()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var contentDir = Path.Combine(tempDir, "content");
        var outputDir = Path.Combine(tempDir, "output");
        Directory.CreateDirectory(contentDir);
        Directory.CreateDirectory(Path.Combine(contentDir, "classes"));

        File.WriteAllText(Path.Combine(contentDir, "classes", "test.json"), "{\"test\":true}");
        File.WriteAllText(Path.Combine(contentDir, "manifest.json"), "{\"version\":1}");

        var result = RunCompiler(contentDir, outputDir);
        Assert.Equal(0, result.ExitCode);

        Assert.True(File.Exists(Path.Combine(outputDir, "content.rpk")));
        Assert.True(File.Exists(Path.Combine(outputDir, "manifest.json")));

        var manifestText = File.ReadAllText(Path.Combine(outputDir, "manifest.json"));
        var manifest = JsonSerializer.Deserialize<JsonElement>(manifestText);
        Assert.True(manifest.TryGetProperty("version", out var versionProp) || manifest.TryGetProperty("Version", out versionProp),
            $"Manifest missing version. Content: {manifestText}");
        Assert.Equal(1, versionProp.GetInt32());

        var packReader = new ContentPackReader();
        packReader.Load(Path.Combine(outputDir, "content.rpk"));
        Assert.True(packReader.Contains("classes/test.json"));
        Assert.True(packReader.Contains("manifest.json"));

        Directory.Delete(tempDir, recursive: true);
    }

    private static (int ExitCode, string Output) RunCompiler(string contentDir, string outputDir)
    {
        var toolPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "tools", "content-pack");
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
        return (process.ExitCode, output);
    }
}
