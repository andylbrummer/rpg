using System.Text.Json;
using System.Text.Json.Serialization;

namespace RPC.Engine.Dungeons;

public static class SegmentLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static List<RoomSegment> LoadFromDirectory(string dir)
    {
        if (!Directory.Exists(dir))
            return new List<RoomSegment>();

        var segments = new List<RoomSegment>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(f => f))
        {
            var json = File.ReadAllText(file);
            var segment = JsonSerializer.Deserialize<RoomSegment>(json, Options)
                ?? throw new InvalidOperationException($"Failed to deserialize segment: {file}");
            segments.Add(segment);
        }
        return segments;
    }
}
