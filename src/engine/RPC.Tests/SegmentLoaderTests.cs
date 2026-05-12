using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Tests;

public class SegmentLoaderTests
{
    [Fact]
    public void LoadFromDirectory_ParsesAllSegments()
    {
        var dir = FindSegmentsDir();
        var segments = SegmentLoader.LoadFromDirectory(dir);

        Assert.Equal(5, segments.Count);
        Assert.Contains(segments, s => s.Id == "entrance");
        Assert.Contains(segments, s => s.Id == "corridor");
        Assert.Contains(segments, s => s.Id == "chamber");
        Assert.Contains(segments, s => s.Id == "dead_end");
        Assert.Contains(segments, s => s.Id == "boss_room");
    }

    [Fact]
    public void LoadFromDirectory_SegmentHasCorrectTiles()
    {
        var dir = FindSegmentsDir();
        var segments = SegmentLoader.LoadFromDirectory(dir);
        var entrance = segments.First(s => s.Id == "entrance");

        Assert.Equal(6, entrance.Tiles.Count);
        Assert.Contains(entrance.Tiles, t => t.X == 1 && t.Y == 0 && t.IsExit && t.ExitDirection == Direction.North);
    }

    [Fact]
    public void JsonSegments_Assemble_Equivalently_To_Hardcoded()
    {
        var seed = "broken_engine".GetHashCode();

        var hardcoded = BuildHardcodedDungeon(seed);
        var fromJson = BuildJsonDungeon(seed);

        Assert.Equal(hardcoded.Name, fromJson.Name);
        Assert.Equal(hardcoded.Width, fromJson.Width);
        Assert.Equal(hardcoded.Height, fromJson.Height);

        for (int x = 0; x < hardcoded.Width; x++)
        {
            for (int y = 0; y < hardcoded.Height; y++)
            {
                var expected = hardcoded.Tiles[x, y];
                var actual = fromJson.Tiles[x, y];
                Assert.Equal(expected.Type, actual.Type);
                Assert.Equal(expected.North, actual.North);
                Assert.Equal(expected.South, actual.South);
                Assert.Equal(expected.East, actual.East);
                Assert.Equal(expected.West, actual.West);
                Assert.Equal(expected.RoomId, actual.RoomId);
            }
        }
    }

    private static string FindSegmentsDir()
    {
        var baseDir = AppContext.BaseDirectory;
        for (int ups = 0; ups <= 8; ups++)
        {
            var parts = new List<string> { baseDir };
            for (int i = 0; i < ups; i++) parts.Add("..");
            parts.AddRange(new[] { "content", "segments", "broken-engine" });
            var candidate = Path.GetFullPath(Path.Combine(parts.ToArray()));
            if (Directory.Exists(candidate))
                return candidate;
        }
        throw new DirectoryNotFoundException("Could not find content/segments/broken-engine");
    }

    private static Dungeon BuildHardcodedDungeon(int seed)
    {
        var builder = new DungeonBuilder(seed: seed);
        builder.AddSegment(CreateEntranceRoom());
        builder.AddSegment(CreateCorridor());
        builder.AddSegment(CreateChamber());
        builder.AddSegment(CreateDeadEnd());
        builder.AddSegment(CreateBossRoom());
        return builder.Build("Broken Engine", 8);
    }

    private static Dungeon BuildJsonDungeon(int seed)
    {
        var builder = new DungeonBuilder(seed: seed);
        var segments = SegmentLoader.LoadFromDirectory(FindSegmentsDir());
        builder.AddSegment(segments.First(s => s.Id == "entrance"));
        builder.AddSegment(segments.First(s => s.Id == "corridor"));
        builder.AddSegment(segments.First(s => s.Id == "chamber"));
        builder.AddSegment(segments.First(s => s.Id == "dead_end"));
        builder.AddSegment(segments.First(s => s.Id == "boss_room"));
        return builder.Build("Broken Engine", 8);
    }

    private static RoomSegment CreateEntranceRoom()
    {
        return new RoomSegment
        {
            Id = "entrance",
            Name = "Entrance Hall",
            Tags = new() { "entrance" },
            Tiles = new()
            {
                new() { X = 0, Y = 0, Type = TileType.Floor },
                new() { X = 1, Y = 0, Type = TileType.Floor, North = BorderType.Door, IsExit = true, ExitDirection = Direction.North },
                new() { X = 2, Y = 0, Type = TileType.Floor },
                new() { X = 0, Y = 1, Type = TileType.Floor },
                new() { X = 1, Y = 1, Type = TileType.Floor },
                new() { X = 2, Y = 1, Type = TileType.Floor },
            }
        };
    }

    private static RoomSegment CreateCorridor()
    {
        return new RoomSegment
        {
            Id = "corridor",
            Name = "Corridor",
            Tiles = new()
            {
                new() { X = 0, Y = 0, Type = TileType.Floor, South = BorderType.Door, IsExit = true, ExitDirection = Direction.South },
                new() { X = 0, Y = -1, Type = TileType.Floor },
                new() { X = 0, Y = -2, Type = TileType.Floor },
                new() { X = 0, Y = -3, Type = TileType.Floor, North = BorderType.Door, IsExit = true, ExitDirection = Direction.North },
            }
        };
    }

    private static RoomSegment CreateChamber()
    {
        return new RoomSegment
        {
            Id = "chamber",
            Name = "Small Chamber",
            Tiles = new()
            {
                new() { X = 0, Y = 1, Type = TileType.Floor, South = BorderType.Door, IsExit = true, ExitDirection = Direction.South },
                new() { X = -1, Y = 0, Type = TileType.Floor },
                new() { X = 0, Y = 0, Type = TileType.Floor },
                new() { X = 1, Y = 0, Type = TileType.Floor },
                new() { X = -1, Y = -1, Type = TileType.Floor },
                new() { X = 0, Y = -1, Type = TileType.Floor },
                new() { X = 1, Y = -1, Type = TileType.Floor },
            }
        };
    }

    private static RoomSegment CreateDeadEnd()
    {
        return new RoomSegment
        {
            Id = "dead_end",
            Name = "Dead End",
            Tiles = new()
            {
                new() { X = 0, Y = 1, Type = TileType.Floor, South = BorderType.Door, IsExit = true, ExitDirection = Direction.South },
                new() { X = 0, Y = 0, Type = TileType.Floor },
            }
        };
    }

    private static RoomSegment CreateBossRoom()
    {
        return new RoomSegment
        {
            Id = "boss_room",
            Name = "Boss Room",
            Tags = new() { "encounter:boss-encounter-1" },
            Tiles = new()
            {
                new() { X = 0, Y = 0, Type = TileType.Floor, South = BorderType.Door, IsExit = true, ExitDirection = Direction.South },
                new() { X = -1, Y = -1, Type = TileType.Floor },
                new() { X = 0, Y = -1, Type = TileType.Floor },
                new() { X = 1, Y = -1, Type = TileType.Floor },
            }
        };
    }

    [Fact]
    public void ContentPack_InvalidSegment_FailsBuild()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rpc-test-{Guid.NewGuid()}");
        var segmentsDir = Path.Combine(tempDir, "segments", "broken-engine");
        Directory.CreateDirectory(segmentsDir);

        var invalidJson = @"{
  ""id"": ""invalid"",
  ""name"": ""Invalid Room"",
  ""tiles"": [
    { ""x"": 0, ""y"": 0, ""type"": ""Floor"" },
    { ""x"": 5, ""y"": 5, ""type"": ""Floor"", ""north"": ""Door"", ""isExit"": true, ""exitDirection"": ""North"" }
  ]
}";
        File.WriteAllText(Path.Combine(segmentsDir, "invalid.json"), invalidJson);

        var outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(outputDir);

        var toolPath = FindContentPackPath();
        var psi = new System.Diagnostics.ProcessStartInfo("dotnet", $"{toolPath} {tempDir} {outputDir}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = System.Diagnostics.Process.Start(psi)!;
        process.WaitForExit();

        Directory.Delete(tempDir, recursive: true);

        Assert.NotEqual(0, process.ExitCode);
        var output = process.StandardOutput.ReadToEnd();
        Assert.Contains("Orphan tile", output);
        Assert.Contains("invalid.json", output);
    }

    private static string FindContentPackPath()
    {
        var baseDir = AppContext.BaseDirectory;
        for (int ups = 0; ups <= 8; ups++)
        {
            var parts = new List<string> { baseDir };
            for (int i = 0; i < ups; i++) parts.Add("..");
            parts.AddRange(new[] { "tools", "content-pack", "bin", "Debug", "net9.0", "content-pack.dll" });
            var candidate = Path.GetFullPath(Path.Combine(parts.ToArray()));
            if (File.Exists(candidate))
                return candidate;
        }
        throw new FileNotFoundException("Could not find content-pack.dll");
    }
}
