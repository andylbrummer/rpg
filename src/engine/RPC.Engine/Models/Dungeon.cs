namespace RPC.Engine.Models.Dungeons;

public enum TileType
{
    Empty,
    Floor,
    StairsUp,
    StairsDown,
    // A pit trap disguised as ordinary floor — walkable (and treacherous) until revealed.
    IllusoryFloor
}

public enum BorderType
{
    None,
    Wall,
    Door,
    SecretDoor,
    BreakableWall,
    // A breakable wall whose secret has been revealed (explicit search / area damage): shows
    // cracked-wall material. Still impassable until a break action opens it.
    CrackedWall,
    // A wall hiding a compartment — impassable like a wall until its secret is opened.
    ConcealedCompartment
}

public readonly record struct Tile(
    TileType Type,
    BorderType North = BorderType.None,
    BorderType South = BorderType.None,
    BorderType East = BorderType.None,
    BorderType West = BorderType.None,
    int RoomId = -1,
    string? EncounterId = null,
    string? LootId = null)
{
    public bool IsWalkable => Type is TileType.Floor or TileType.StairsUp or TileType.StairsDown or TileType.IllusoryFloor;

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

/// <summary>
/// The mutable tile plane of a <see cref="Dungeon"/>. Wrapping the raw <c>Tile[,]</c> exists so
/// that every write bumps <see cref="Version"/>: presenters cache derived views of the map
/// (notably the explored-tile automap payload) and key them on this counter, and a write that
/// slipped past the counter would serve a stale map. Because the setter is the only way to
/// write, that cannot happen by omission at a call site.
/// </summary>
public sealed class TileGrid
{
    private readonly Tile[,] _tiles;
    private int _version;

    public TileGrid(int width, int height)
    {
        _tiles = new Tile[width, height];
    }

    /// <summary>Incremented on every tile write. Cheap staleness key for derived views.</summary>
    public int Version => _version;

    public Tile this[int x, int y]
    {
        get => _tiles[x, y];
        set
        {
            _tiles[x, y] = value;
            _version++;
        }
    }
}

public class Dungeon
{
    public int Width { get; }
    public int Height { get; }
    public TileGrid Tiles { get; }
    public string Name { get; }
    public string? WanderingTableId { get; set; }
    public string? EncounterTableId { get; set; }
    public int Seed { get; set; }
    public List<RoomInfo> Rooms { get; } = new();

    public Dungeon(int width, int height, string name)
    {
        Width = width;
        Height = height;
        Name = name;
        Tiles = new TileGrid(width, height);

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
        if (border is BorderType.Wall or BorderType.SecretDoor or BorderType.BreakableWall or BorderType.CrackedWall or BorderType.ConcealedCompartment)
            return false;

        var to = from.Move(dir);
        if (!IsValidPosition(to)) return false;
        var targetTile = Tiles[to.X, to.Y];
        return targetTile.IsWalkable;
    }

    public Tile GetTile(Position pos) =>
        IsValidPosition(pos) ? Tiles[pos.X, pos.Y] : new Tile(TileType.Empty);

    /// <summary>
    /// Where a party arrives in this dungeon: the tile the stitcher marked
    /// <see cref="TileType.StairsUp"/>, or — for hand-built or stairless maps — the first walkable
    /// tile in scan order. Null only when nothing is walkable.
    /// <para>
    /// Every consumer that needs "the entrance" must come through here. The stairs tile is not
    /// <see cref="TileType.Floor"/>, so an independent scan looking for floor silently answers with
    /// a different room than the one the path classifier and the stairs placer agree on.
    /// </para>
    /// </summary>
    public Position? FindEntrance()
    {
        Position? firstWalkable = null;
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var tile = Tiles[x, y];
                if (!tile.IsWalkable) continue;
                if (tile.Type == TileType.StairsUp) return new Position(x, y);
                firstWalkable ??= new Position(x, y);
            }
        }
        return firstWalkable;
    }
}

public class RoomInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public Position Min { get; set; }
    public Position Max { get; set; }
    public List<Position> Exits { get; set; } = new();
}
