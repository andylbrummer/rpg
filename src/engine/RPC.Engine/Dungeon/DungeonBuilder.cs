using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Dungeons;

public class DungeonBuilder
{
    private readonly List<RoomSegment> _segments = new();
    private readonly GameRandom _random;
    private readonly int _seed;

    public DungeonBuilder(int seed)
    {
        _seed = seed;
        _random = new GameRandom(seed);
    }

    public void AddSegment(RoomSegment segment)
    {
        _segments.Add(segment);
    }

    public Dungeon Build(string name, int targetRooms = 10, EncounterTableRegistry? encounterTables = null, string? encounterTableId = null)
    {
        // Simple dungeon generation: place rooms and connect with corridors
        var dungeon = new Dungeon(64, 64, name);
        dungeon.Seed = _seed;
        var placedRooms = new List<PlacedRoom>();

        // Place entrance
        var entrance = _segments.FirstOrDefault(s => s.Tags.Contains("entrance"))
            ?? _segments.First();
        var entrancePos = new Position(32, 32);
        var placedEntrance = PlaceRoom(dungeon, entrance, entrancePos, 0);
        placedRooms.Add(placedEntrance);

        int roomId = 1;
        int attempts = 0;

        while (placedRooms.Count < targetRooms && attempts < targetRooms * 10)
        {
            attempts++;

            // Pick a random placed room and one of its exits
            var parentRoom = placedRooms[_random.Next(placedRooms.Count)];
            if (!parentRoom.Exits.Any()) continue;

            var exit = parentRoom.Exits[_random.Next(parentRoom.Exits.Count)];

            // Pick a random segment to place
            var segment = _segments[_random.Next(_segments.Count)];

            // Try to place it adjacent to the exit
            var newRoom = TryPlaceRoom(dungeon, segment, exit.Position, exit.Direction, roomId);
            if (newRoom != null)
            {
                placedRooms.Add(newRoom);
                roomId++;
            }
        }

        // Derive borders for all walkable tiles
        DeriveBorders(dungeon);

        // Tag encounter slots
        TagEncounterSlots(dungeon, placedRooms, encounterTables, encounterTableId);

        return dungeon;
    }

    private PlacedRoom PlaceRoom(Dungeon dungeon, RoomSegment segment, Position position, int roomId)
    {
        var exits = new List<RoomExit>();

        foreach (var tile in segment.Tiles)
        {
            var worldPos = new Position(position.X + tile.X, position.Y + tile.Y);
            if (dungeon.IsValidPosition(worldPos))
            {
                var placedTile = new Tile(
                    tile.Type,
                    tile.North ?? BorderType.None,
                    tile.South ?? BorderType.None,
                    tile.East ?? BorderType.None,
                    tile.West ?? BorderType.None,
                    roomId
                );
                dungeon.Tiles[worldPos.X, worldPos.Y] = placedTile;

                if (tile.IsExit)
                {
                    exits.Add(new RoomExit(worldPos, tile.ExitDirection!.Value));
                }
            }
        }

        return new PlacedRoom
        {
            Id = roomId,
            Segment = segment,
            Position = position,
            Exits = exits
        };
    }

    private PlacedRoom? TryPlaceRoom(Dungeon dungeon, RoomSegment segment, Position atPosition, Direction fromDirection, int roomId)
    {
        // Calculate offset based on entrance direction
        // The room should be placed so that one of its entrances is adjacent to atPosition
        var entrance = segment.Tiles.FirstOrDefault(t => t.IsExit && t.ExitDirection == Opposite(fromDirection));

        if (entrance == null)
        {
            // No matching entrance, try any exit
            entrance = segment.Tiles.FirstOrDefault(t => t.IsExit);
            if (entrance == null) return null;
        }

        // Place the entrance tile adjacent to the parent's exit (one step in the connection direction)
        var entranceWorldPos = atPosition.Move(fromDirection);
        var offset = new Position(entranceWorldPos.X - entrance.X, entranceWorldPos.Y - entrance.Y);

        // Check if placement is valid (no overlaps with existing walkable tiles)
        foreach (var tile in segment.Tiles)
        {
            var worldPos = new Position(offset.X + tile.X, offset.Y + tile.Y);
            if (!dungeon.IsValidPosition(worldPos))
                return null;
            if (dungeon.Tiles[worldPos.X, worldPos.Y].Type != TileType.Empty)
                return null;
        }

        return PlaceRoom(dungeon, segment, offset, roomId);
    }

    private void DeriveBorders(Dungeon dungeon)
    {
        for (int x = 0; x < dungeon.Width; x++)
        {
            for (int y = 0; y < dungeon.Height; y++)
            {
                var tile = dungeon.Tiles[x, y];
                if (!tile.IsWalkable) continue;

                // North
                if (tile.North == BorderType.None)
                {
                    var ny = y - 1;
                    if (ny < 0 || !dungeon.Tiles[x, ny].IsWalkable)
                        tile = tile.WithBorder(Direction.North, BorderType.Wall);
                }

                // South
                if (tile.South == BorderType.None)
                {
                    var sy = y + 1;
                    if (sy >= dungeon.Height || !dungeon.Tiles[x, sy].IsWalkable)
                        tile = tile.WithBorder(Direction.South, BorderType.Wall);
                }

                // East
                if (tile.East == BorderType.None)
                {
                    var ex = x + 1;
                    if (ex >= dungeon.Width || !dungeon.Tiles[ex, y].IsWalkable)
                        tile = tile.WithBorder(Direction.East, BorderType.Wall);
                }

                // West
                if (tile.West == BorderType.None)
                {
                    var wx = x - 1;
                    if (wx < 0 || !dungeon.Tiles[wx, y].IsWalkable)
                        tile = tile.WithBorder(Direction.West, BorderType.Wall);
                }

                dungeon.Tiles[x, y] = tile;
            }
        }
    }

    private void TagEncounterSlots(Dungeon dungeon, List<PlacedRoom> placedRooms, EncounterTableRegistry? encounterTables, string? encounterTableId)
    {
        foreach (var placedRoom in placedRooms)
        {
            var forcedId = placedRoom.Segment.Tags
                .FirstOrDefault(t => t.StartsWith("encounter:"))?
                .Substring("encounter:".Length);

            if (forcedId != null)
            {
                foreach (var exit in placedRoom.Exits)
                {
                    var pos = exit.Position;
                    if (!dungeon.IsValidPosition(pos)) continue;
                    var tile = dungeon.Tiles[pos.X, pos.Y];
                    if (tile.IsWalkable)
                        dungeon.Tiles[pos.X, pos.Y] = tile with { EncounterId = forcedId };
                }
            }
            else if (placedRoom.Segment.Tags.Contains("encounter_slot") && encounterTables != null && !string.IsNullOrEmpty(encounterTableId))
            {
                foreach (var exit in placedRoom.Exits)
                {
                    var pos = exit.Position;
                    if (!dungeon.IsValidPosition(pos)) continue;
                    var tile = dungeon.Tiles[pos.X, pos.Y];
                    if (!tile.IsWalkable) continue;
                    var rng = new GameRandom(_random.NextInt());
                    var enc = encounterTables.RollEncounter(encounterTableId, rng);
                    dungeon.Tiles[pos.X, pos.Y] = tile with { EncounterId = enc.Id };
                }
            }
        }
    }

    private static Direction Opposite(Direction dir) => dir switch
    {
        Direction.North => Direction.South,
        Direction.South => Direction.North,
        Direction.East => Direction.West,
        Direction.West => Direction.East,
        _ => dir
    };
}

public class PlacedRoom
{
    public int Id { get; set; }
    public RoomSegment Segment { get; set; } = null!;
    public Position Position { get; set; }
    public List<RoomExit> Exits { get; set; } = new();
}

public class RoomExit
{
    public Position Position { get; }
    public Direction Direction { get; }

    public RoomExit(Position position, Direction direction)
    {
        Position = position;
        Direction = direction;
    }
}
