namespace RPC.Engine.Models.Dungeons;

public enum TileType
{
    Empty,
    Floor,
    StairsUp,
    StairsDown
}

public enum BorderType
{
    None,
    Wall,
    Door,
    SecretDoor,
    BreakableWall
}

public readonly record struct Tile(
    TileType Type,
    BorderType North = BorderType.None,
    BorderType South = BorderType.None,
    BorderType East = BorderType.None,
    BorderType West = BorderType.None,
    int RoomId = -1,
    string? EncounterId = null)
{
    public bool IsWalkable => Type is TileType.Floor or TileType.StairsUp or TileType.StairsDown;

    public BorderType GetBorder(Direction dir) => dir switch
    {
        Direction.North => North,
        Direction.South => South,
        Direction.East => East,
        Direction.West => West,
        _ => BorderType.None
    };

    public Tile WithBorder(Direction dir, BorderType border) => dir switch
    {
        Direction.North => this with { North = border },
        Direction.South => this with { South = border },
        Direction.East => this with { East = border },
        Direction.West => this with { West = border },
        _ => this
    };
}

public class Dungeon
{
    public int Width { get; }
    public int Height { get; }
    public Tile[,] Tiles { get; }
    public string Name { get; }
    public string? WanderingTableId { get; set; }
    public string? EncounterTableId { get; set; }
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

    public bool CanMoveTo(Position from, Direction dir)
    {
        if (!IsValidPosition(from)) return false;
        var tile = Tiles[from.X, from.Y];
        if (!tile.IsWalkable) return false;

        var border = tile.GetBorder(dir);
        if (border is BorderType.Wall or BorderType.SecretDoor or BorderType.BreakableWall)
            return false;

        var to = from.Move(dir);
        if (!IsValidPosition(to)) return false;
        var targetTile = Tiles[to.X, to.Y];
        return targetTile.IsWalkable;
    }

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
