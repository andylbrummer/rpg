using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Dungeons;

public class DungeonBuilder
{
    private readonly List<RoomSegment> _segments = new();
    private readonly Random _random;

    public DungeonBuilder(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public void AddSegment(RoomSegment segment)
    {
        _segments.Add(segment);
    }

    public Dungeon Build(string name, int targetRooms = 10)
    {
        // Simple dungeon generation: place rooms and connect with corridors
        var dungeon = new Dungeon(64, 64, name);
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
            
            // Try to place it at the exit
            var newRoom = TryPlaceRoom(dungeon, segment, exit.Position, exit.Direction, roomId);
            if (newRoom != null)
            {
                placedRooms.Add(newRoom);
                roomId++;
                
                // Connect with corridor/door
                dungeon.Tiles[exit.Position.X, exit.Position.Y] = new Tile(TileType.Door, newRoom.Id);
            }
        }
        
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
                dungeon.Tiles[worldPos.X, worldPos.Y] = new Tile(tile.Type, roomId);
                
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
        // The room should be placed so that one of its entrances connects to atPosition
        var entrance = segment.Tiles.FirstOrDefault(t => t.IsExit && t.ExitDirection == Opposite(fromDirection));
        
        if (entrance == null)
        {
            // No matching entrance, try any exit
            entrance = segment.Tiles.FirstOrDefault(t => t.IsExit);
            if (entrance == null) return null;
        }
        
        var offset = new Position(atPosition.X - entrance.X, atPosition.Y - entrance.Y);
        
        // Check if placement is valid (no overlaps)
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
