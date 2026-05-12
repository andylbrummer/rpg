using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

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

        foreach (var file in jsonFiles)
        {
            var relativePath = Path.GetRelativePath(contentDir, file).Replace('\\', '/');
            if (relativePath.Contains("/segments/") || relativePath.StartsWith("segments/"))
            {
                var json = File.ReadAllText(file);
                var result = ValidateSegment(file, json);
                if (result != 0) return result;
            }
            else if (relativePath.Contains("/synergies/") || relativePath.StartsWith("synergies/"))
            {
                var json = File.ReadAllText(file);
                var result = ValidateSynergy(file, json);
                if (result != 0) return result;
            }
        }

        var manifest = new Manifest
        {
            Version = 1,
            Files = jsonFiles.Select(f => Path.GetRelativePath(contentDir, f).Replace('\\', '/')).ToArray()
        };

        var manifestPath = Path.Combine(outputDir, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Wrote manifest: {manifestPath}");

        var synergyMap = CompileSynergyMap(contentDir);
        var synergyMapPath = Path.Combine(outputDir, "synergies.map.json");
        File.WriteAllText(synergyMapPath, JsonSerializer.Serialize(synergyMap, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Wrote synergy map: {synergyMapPath}");

        var rpkPath = Path.Combine(outputDir, "content.rpk");
        CompileRpk(contentDir, jsonFiles, rpkPath);
        Console.WriteLine($"Wrote pack: {rpkPath} ({new FileInfo(rpkPath).Length} bytes)");

        return 0;
    }

    static int ValidateSegment(string filePath, string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        RoomSegment? segment;
        try
        {
            segment = JsonSerializer.Deserialize<RoomSegment>(json, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {filePath} - {ex.Message}");
            return 1;
        }

        if (segment == null)
        {
            Console.WriteLine($"FAIL: {filePath} - Deserialization returned null");
            return 1;
        }

        var seen = new HashSet<(int, int)>();
        foreach (var tile in segment.Tiles)
        {
            if (!seen.Add((tile.X, tile.Y)))
            {
                var line = FindLineNumber(json, tile.X, tile.Y);
                Console.WriteLine($"FAIL: {filePath}:{line} - Duplicate tile at ({tile.X}, {tile.Y})");
                return 1;
            }
        }

        if (segment.Tiles.Count > 1)
        {
            foreach (var tile in segment.Tiles)
            {
                bool hasNeighbor = segment.Tiles.Any(t =>
                    !ReferenceEquals(t, tile) &&
                    Math.Abs(t.X - tile.X) + Math.Abs(t.Y - tile.Y) == 1);
                if (!hasNeighbor)
                {
                    var line = FindLineNumber(json, tile.X, tile.Y);
                    Console.WriteLine($"FAIL: {filePath}:{line} - Orphan tile at ({tile.X}, {tile.Y})");
                    return 1;
                }
            }
        }

        foreach (var tile in segment.Tiles)
        {
            if (tile.IsExit)
            {
                if (tile.ExitDirection == null)
                {
                    var line = FindLineNumber(json, tile.X, tile.Y);
                    Console.WriteLine($"FAIL: {filePath}:{line} - Exit tile at ({tile.X}, {tile.Y}) missing exitDirection");
                    return 1;
                }

                var border = tile.ExitDirection.Value switch
                {
                    Direction.North => tile.North,
                    Direction.South => tile.South,
                    Direction.East => tile.East,
                    Direction.West => tile.West,
                    _ => null
                };

                if (border != BorderType.Door)
                {
                    var line = FindLineNumber(json, tile.X, tile.Y);
                    Console.WriteLine($"FAIL: {filePath}:{line} - Exit tile at ({tile.X}, {tile.Y}) has {tile.ExitDirection} border '{border}' but expected 'Door'");
                    return 1;
                }
            }
        }

        return 0;
    }

    static int ValidateSynergy(string filePath, string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        SynergyDef? def;
        try
        {
            def = JsonSerializer.Deserialize<SynergyDef>(json, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {filePath} - {ex.Message}");
            return 1;
        }

        if (def == null)
        {
            Console.WriteLine($"FAIL: {filePath} - Deserialization returned null");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(def.Id))
        {
            Console.WriteLine($"FAIL: {filePath} - Missing id");
            return 1;
        }

        if (def.Abilities == null || def.Abilities.Length != 2)
        {
            Console.WriteLine($"FAIL: {filePath} - Expected exactly 2 abilities");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(def.Abilities[0]) || string.IsNullOrWhiteSpace(def.Abilities[1]))
        {
            Console.WriteLine($"FAIL: {filePath} - Ability IDs must not be empty");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(def.Hint))
        {
            Console.WriteLine($"FAIL: {filePath} - Hint must not be empty");
            return 1;
        }

        var validAppliesAfter = new[] { "first_ability", "second_ability", "round_end" };
        if (def.Effect == null || !validAppliesAfter.Contains(def.Effect.AppliesAfter))
        {
            Console.WriteLine($"FAIL: {filePath} - Invalid or missing effect.appliesAfter");
            return 1;
        }

        return 0;
    }

    static Dictionary<string, SynergyEffect> CompileSynergyMap(string contentDir)
    {
        var map = new Dictionary<string, SynergyEffect>();
        var synergyDir = Path.Combine(contentDir, "synergies");
        if (!Directory.Exists(synergyDir))
            return map;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        foreach (var file in Directory.EnumerateFiles(synergyDir, "*.json"))
        {
            var json = File.ReadAllText(file);
            var def = JsonSerializer.Deserialize<SynergyDef>(json, options);
            if (def == null || def.Anti || def.Abilities.Length != 2)
                continue;

            var key = SynergyRegistry.MakeKey(def.Abilities[0], def.Abilities[1]);
            if (string.IsNullOrEmpty(key))
                continue;

            map[key] = new SynergyEffect(def.Effect.Type, def.Effect.Value);
        }

        return map;
    }

    static int FindLineNumber(string text, int x, int y)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains($"\"x\": {x}") && lines[i].Contains($"\"y\": {y}"))
                return i + 1;
        }
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
