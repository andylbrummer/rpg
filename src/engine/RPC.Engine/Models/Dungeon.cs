namespace RPC.Engine.Models.Dungeons;

public enum TileType
{
    Empty,
    Floor,
    Wall,
    Door,
    SecretDoor,
    StairsUp,
    StairsDown
}

public readonly record struct Tile(TileType Type, int RoomId = -1)
{
    public bool IsWalkable => Type is TileType.Floor or TileType.Door or TileType.StairsUp or TileType.StairsDown;
    public bool IsOpaque => Type is TileType.Wall or TileType.Door or TileType.SecretDoor;
}

public class Dungeon
{
    public int Width { get; }
    public int Height { get; }
    public Tile[,] Tiles { get; }
    public string Name { get; }
    public List<RoomInfo> Rooms { get; } = new();
    
    public Dungeon(int width, int height, string name)
    {
        Width = width;
        Height = height;
        Name = name;
        Tiles = new Tile[width, height];
        
        // Initialize with empty tiles
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                Tiles[x, y] = new Tile(TileType.Empty);
    }

    public bool IsValidPosition(Position pos) => 
        pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;

    public bool CanMoveTo(Position pos) => 
        IsValidPosition(pos) && Tiles[pos.X, pos.Y].IsWalkable;

    public Tile GetTile(Position pos) => 
        IsValidPosition(pos) ? Tiles[pos.X, pos.Y] : new Tile(TileType.Empty);
}

public class RoomInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public Position Min { get; set; }
    public Position Max { get; set; }
    public List<Position> Exits { get; set; } = new();
}
