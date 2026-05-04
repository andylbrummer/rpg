using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Dungeons;

public class RoomSegment
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public List<SegmentTile> Tiles { get; set; } = new();
    public int Width { get; set; }
    public int Height { get; set; }
}

public class SegmentTile
{
    public int X { get; set; }
    public int Y { get; set; }
    public TileType Type { get; set; }
    public bool IsExit { get; set; }
    public Direction? ExitDirection { get; set; }
}
