using System.Text;
using System.Text.Json;

namespace ContentPack;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run -- <content-dir> <output-dir>");
            return 1;
        }

        var contentDir = args[0];
        var outputDir = args[1];

        if (!Directory.Exists(contentDir))
        {
            Console.WriteLine($"Content directory not found: {contentDir}");
            return 1;
        }

        Directory.CreateDirectory(outputDir);

        var jsonFiles = Directory.EnumerateFiles(contentDir, "*.json", SearchOption.AllDirectories)
            .OrderBy(f => f)
            .ToList();

        var manifest = new Manifest
        {
            Version = 1,
            Files = jsonFiles.Select(f => Path.GetRelativePath(contentDir, f).Replace('\\', '/')).ToArray()
        };

        var manifestPath = Path.Combine(outputDir, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Wrote manifest: {manifestPath}");

        var rpkPath = Path.Combine(outputDir, "content.rpk");
        CompileRpk(contentDir, jsonFiles, rpkPath);
        Console.WriteLine($"Wrote pack: {rpkPath} ({new FileInfo(rpkPath).Length} bytes)");

        return 0;
    }

    static void CompileRpk(string contentDir, List<string> jsonFiles, string outputPath)
    {
        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        writer.Write(Encoding.ASCII.GetBytes("RPK1"));
        writer.Write(1u);
        writer.Write((uint)jsonFiles.Count);

        foreach (var file in jsonFiles)
        {
            var relativePath = Path.GetRelativePath(contentDir, file).Replace('\\', '/');
            var pathBytes = Encoding.UTF8.GetBytes(relativePath);
            var data = File.ReadAllBytes(file);

            var dataOffset = (uint)(stream.Position + 4 + 4 + 2 + pathBytes.Length);

            writer.Write(dataOffset);
            writer.Write((uint)data.Length);
            writer.Write((ushort)pathBytes.Length);
            writer.Write(pathBytes);
            writer.Write(data);
        }
    }
}

class Manifest
{
    public int Version { get; set; }
    public string[] Files { get; set; } = Array.Empty<string>();
}
