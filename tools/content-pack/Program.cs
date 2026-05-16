using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Content;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Town;

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
            var json = File.ReadAllText(file);
            int result;

            if (relativePath.Contains("/segments/") || relativePath.StartsWith("segments/"))
            {
                result = ValidateSegment(file, json);
            }
            else if (relativePath.Contains("/synergies/") || relativePath.StartsWith("synergies/"))
            {
                result = ValidateSynergy(file, json);
            }
            else if (relativePath.Contains("/classes/") || relativePath.StartsWith("classes/"))
            {
                result = ValidateClass(file, json);
            }
            else if (relativePath.Contains("/enemies/") || relativePath.StartsWith("enemies/"))
            {
                result = ValidateEnemy(file, json);
            }
            else if (relativePath.Contains("/encounters/") || relativePath.StartsWith("encounters/"))
            {
                result = ValidateEncounter(file, json);
            }
            else if (relativePath.Contains("/factions/") || relativePath.StartsWith("factions/"))
            {
                result = ValidateFaction(file, json);
            }
            else if (relativePath.Contains("/campaigns/dungeons/") || relativePath.StartsWith("campaigns/dungeons/"))
            {
                result = ValidateDungeonTemplate(file, json);
            }
            else if (relativePath.Contains("/items/") || relativePath.StartsWith("items/"))
            {
                result = ValidateItems(file, json);
            }
            else if (relativePath.Contains("/schemes/") || relativePath.StartsWith("schemes/"))
            {
                result = ValidateScheme(file, json);
            }
            else if (relativePath.Contains("/complications/") || relativePath.StartsWith("complications/"))
            {
                result = ValidateComplication(file, json);
            }
            else if (relativePath.Contains("/rumors/") || relativePath.StartsWith("rumors/"))
            {
                result = ValidateRumors(file, json);
            }
            else if (relativePath.Contains("/npcs/") || relativePath.StartsWith("npcs/"))
            {
                result = ValidateNpcs(file, json);
            }
            else if (relativePath.Contains("/campaigns/") || relativePath.StartsWith("campaigns/"))
            {
                result = ValidateCampaign(file, json);
            }
            else if (relativePath.Contains("/schemas/") || relativePath.StartsWith("schemas/"))
            {
                continue; // JSON Schema definitions, not game content
            }
            else
            {
                Console.WriteLine($"WARN: {relativePath} - No validator for this category");
                continue;
            }

            if (result != 0) return result;
        }

        var contentHash = ComputeContentHash(jsonFiles);
        var manifest = new Manifest
        {
            Version = 1,
            ContentHash = contentHash,
            Files = jsonFiles.Select(f => Path.GetRelativePath(contentDir, f).Replace('\\', '/')).ToArray()
        };

        var manifestPath = Path.Combine(outputDir, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Wrote manifest: {manifestPath} (hash: {contentHash})");

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

    static int ValidateClass(string filePath, string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        try
        {
            var def = JsonSerializer.Deserialize<ClassDef>(json, options);
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
            if (string.IsNullOrWhiteSpace(def.Name))
            {
                Console.WriteLine($"FAIL: {filePath} - Missing name");
                return 1;
            }
            if (def.Abilities == null || def.Abilities.Length < 3)
            {
                Console.WriteLine($"FAIL: {filePath} - Expected at least 3 abilities, got {def.Abilities?.Length ?? 0}");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {filePath} - {ex.Message}");
            return 1;
        }

        return 0;
    }

    static int ValidateEnemy(string filePath, string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        try
        {
            var def = JsonSerializer.Deserialize<EnemyDef>(json, options);
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
            if (def.HpBase <= 0)
            {
                Console.WriteLine($"FAIL: {filePath} - hpBase must be positive");
                return 1;
            }
            if (string.IsNullOrWhiteSpace(def.Ai))
            {
                Console.WriteLine($"FAIL: {filePath} - Missing AI behavior tag");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {filePath} - {ex.Message}");
            return 1;
        }

        return 0;
    }

    static int ValidateEncounter(string filePath, string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        try
        {
            var registry = new EncounterTableRegistry();
            registry.LoadFromJson(Path.GetFileNameWithoutExtension(filePath), json);
            var table = registry.Get(Path.GetFileNameWithoutExtension(filePath));
            if (table == null)
            {
                Console.WriteLine($"FAIL: {filePath} - Could not load encounter table");
                return 1;
            }
            if (table.Entries.Length == 0)
            {
                Console.WriteLine($"FAIL: {filePath} - Encounter table has no entries");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {filePath} - {ex.Message}");
            return 1;
        }

        return 0;
    }

    static int ValidateFaction(string filePath, string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        try
        {
            var def = JsonSerializer.Deserialize<FactionContentDef>(json, options);
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
            if (string.IsNullOrWhiteSpace(def.Name))
            {
                Console.WriteLine($"FAIL: {filePath} - Missing name");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {filePath} - {ex.Message}");
            return 1;
        }

        return 0;
    }

    static int ValidateDungeonTemplate(string filePath, string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        DungeonTemplate? template;
        try
        {
            template = JsonSerializer.Deserialize<DungeonTemplate>(json, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {filePath} - {ex.Message}");
            return 1;
        }

        if (template == null)
        {
            Console.WriteLine($"FAIL: {filePath} - Could not deserialize dungeon template");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(template.Id))
        {
            Console.WriteLine($"FAIL: {filePath} - Missing id");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            Console.WriteLine($"FAIL: {filePath} - Missing name");
            return 1;
        }

        if (template.SegmentPool.Length == 0)
        {
            Console.WriteLine($"FAIL: {filePath} - SegmentPool must not be empty");
            return 1;
        }

        if (template.TargetRooms <= 0)
        {
            Console.WriteLine($"FAIL: {filePath} - TargetRooms must be > 0");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(template.BossEncounterId))
        {
            Console.WriteLine($"FAIL: {filePath} - Missing bossEncounterId");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(template.EncounterTableId))
        {
            Console.WriteLine($"FAIL: {filePath} - Missing encounterTableId");
            return 1;
        }

        return 0;
    }

    static int ValidateItems(string filePath, string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        try
        {
            var items = JsonSerializer.Deserialize<ItemDef[]>(json, options);
            if (items == null || items.Length == 0)
            {
                Console.WriteLine($"FAIL: {filePath} - Expected non-empty array of items");
                return 1;
            }
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Id))
                {
                    Console.WriteLine($"FAIL: {filePath} - Item missing id");
                    return 1;
                }
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    Console.WriteLine($"FAIL: {filePath} - Item '{item.Id}' missing name");
                    return 1;
                }
                if (string.IsNullOrWhiteSpace(item.Type))
                {
                    Console.WriteLine($"FAIL: {filePath} - Item '{item.Id}' missing type");
                    return 1;
                }
                if (item.Value < 0)
                {
                    Console.WriteLine($"FAIL: {filePath} - Item '{item.Id}' value must be >= 0");
                    return 1;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {filePath} - {ex.Message}");
            return 1;
        }
        return 0;
    }

    static int ValidateScheme(string filePath, string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        try
        {
            var def = JsonSerializer.Deserialize<SchemeDef>(json, options);
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
            if (string.IsNullOrWhiteSpace(def.Name))
            {
                Console.WriteLine($"FAIL: {filePath} - Missing name");
                return 1;
            }
            if (def.EvidenceChain == null || def.EvidenceChain.Length == 0)
            {
                Console.WriteLine($"FAIL: {filePath} - EvidenceChain must not be empty");
                return 1;
            }
            if (def.Events == null || def.Events.Length == 0)
            {
                Console.WriteLine($"FAIL: {filePath} - Events must not be empty");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {filePath} - {ex.Message}");
            return 1;
        }
        return 0;
    }

    static int ValidateComplication(string filePath, string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        try
        {
            var def = JsonSerializer.Deserialize<ComplicationDef>(json, options);
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
            if (string.IsNullOrWhiteSpace(def.Name))
            {
                Console.WriteLine($"FAIL: {filePath} - Missing name");
                return 1;
            }
            if (def.Events == null || def.Events.Length == 0)
            {
                Console.WriteLine($"FAIL: {filePath} - Events must not be empty");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {filePath} - {ex.Message}");
            return 1;
        }
        return 0;
    }

    static int ValidateRumors(string filePath, string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        try
        {
            var rumors = JsonSerializer.Deserialize<RumorDef[]>(json, options);
            if (rumors == null || rumors.Length == 0)
            {
                Console.WriteLine($"FAIL: {filePath} - Expected non-empty array of rumors");
                return 1;
            }
            foreach (var rumor in rumors)
            {
                if (string.IsNullOrWhiteSpace(rumor.Id))
                {
                    Console.WriteLine($"FAIL: {filePath} - Rumor missing id");
                    return 1;
                }
                if (string.IsNullOrWhiteSpace(rumor.Text))
                {
                    Console.WriteLine($"FAIL: {filePath} - Rumor '{rumor.Id}' missing text");
                    return 1;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {filePath} - {ex.Message}");
            return 1;
        }
        return 0;
    }

    static int ValidateNpcs(string filePath, string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        try
        {
            var npcs = JsonSerializer.Deserialize<NpcDef[]>(json, options);
            if (npcs == null || npcs.Length == 0)
            {
                Console.WriteLine($"FAIL: {filePath} - Expected non-empty array of NPCs");
                return 1;
            }
            foreach (var npc in npcs)
            {
                if (string.IsNullOrWhiteSpace(npc.Id))
                {
                    Console.WriteLine($"FAIL: {filePath} - NPC missing id");
                    return 1;
                }
                if (string.IsNullOrWhiteSpace(npc.Name))
                {
                    Console.WriteLine($"FAIL: {filePath} - NPC '{npc.Id}' missing name");
                    return 1;
                }
                if (npc.Level < 1)
                {
                    Console.WriteLine($"FAIL: {filePath} - NPC '{npc.Id}' level must be >= 1");
                    return 1;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {filePath} - {ex.Message}");
            return 1;
        }
        return 0;
    }

    static int ValidateCampaign(string filePath, string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        try
        {
            var config = JsonSerializer.Deserialize<CampaignConfig>(json, options);
            if (config == null)
            {
                Console.WriteLine($"FAIL: {filePath} - Deserialization returned null");
                return 1;
            }
            if (!config.Validate(out var error))
            {
                Console.WriteLine($"FAIL: {filePath} - {error}");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {filePath} - {ex.Message}");
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

    static string ComputeContentHash(List<string> files)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        foreach (var file in files.OrderBy(f => f))
        {
            var data = File.ReadAllBytes(file);
            sha.TransformBlock(data, 0, data.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!);
    }
}

class Manifest
{
    public int Version { get; set; }
    public string ContentHash { get; set; } = "";
    public string[] Files { get; set; } = Array.Empty<string>();
}
