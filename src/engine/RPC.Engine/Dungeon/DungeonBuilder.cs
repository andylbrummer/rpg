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
        var dungeon = new Dungeon(64, 64, name);
        dungeon.Seed = _seed;
        var placedRooms = new List<PlacedRoom>();

        // Place entrance.
        var entrance = _segments.FirstOrDefault(s => s.Tags.Contains("entrance"))
            ?? _segments.First();
        var placedEntrance = PlaceRoom(dungeon, entrance, new Position(32, 32), 0);
        placedRooms.Add(placedEntrance);

        // Classify the pool so growth doesn't dead-end early. A segment is "branching" if it has
        // >1 exit (one is consumed by its own connection, so only branching segments keep the
        // frontier open). "Terminal" segments (chamber/dead-end/boss) cap a passage.
        var branching = _segments.Where(s => !s.Tags.Contains("entrance") && ExitCount(s) >= 2).ToList();
        var nonEntranceNonBoss = _segments.Where(s => !s.Tags.Contains("entrance") && !IsBoss(s)).ToList();
        var anyNonEntrance = _segments.Where(s => !s.Tags.Contains("entrance")).ToList();

        // Frontier of open exits across all placed rooms. An exit leaves the frontier when a room
        // attaches to it (pruning) — so we never waste attempts re-filling an occupied exit.
        var frontier = new List<RoomExit>(placedEntrance.Exits);

        int roomId = 1;
        int attempts = 0;
        int cap = Math.Max(40, targetRooms * 20);

        while (placedRooms.Count < targetRooms && frontier.Count > 0 && attempts < cap)
        {
            attempts++;
            int fi = _random.Next(frontier.Count);
            var exit = frontier[fi];

            var segment = ChooseSegment(placedRooms.Count, targetRooms, frontier.Count,
                branching, nonEntranceNonBoss, anyNonEntrance);
            if (segment == null) break;

            var newRoom = TryPlaceRoom(dungeon, segment, exit.Position, exit.Direction, roomId);
            if (newRoom != null)
            {
                placedRooms.Add(newRoom);
                roomId++;
                frontier.RemoveAt(fi);                 // exit consumed by the new connection
                frontier.AddRange(newRoom.Exits);      // new room's remaining exits open up
            }
            else if (attempts % frontier.Count == 0)
            {
                // This exit has resisted placement for a while (blocked by collisions); drop it so
                // the builder doesn't spin on an unplaceable frontier slot.
                frontier.RemoveAt(fi);
            }
        }

        DeriveBorders(dungeon);
        TagEncounterSlots(dungeon, placedRooms, encounterTables, encounterTableId);
        return dungeon;
    }

    private static int ExitCount(RoomSegment segment) => segment.Tiles.Count(t => t.IsExit);

    private static bool IsBoss(RoomSegment segment) =>
        segment.Tags.Any(t => t == "boss" || t.StartsWith("encounter:boss"));

    /// <summary>
    /// Pick the next segment to place. Keeps the dungeon growing toward the target: when the
    /// frontier is about to run dry and we still need rooms, force a branching segment; otherwise
    /// avoid the boss segment until we're near the target so it doesn't cap a passage too early.
    /// </summary>
    private RoomSegment? ChooseSegment(int placed, int target, int frontierCount,
        List<RoomSegment> branching, List<RoomSegment> nonEntranceNonBoss, List<RoomSegment> anyNonEntrance)
    {
        bool needGrowth = placed < target - 1 && frontierCount <= 1;
        if (needGrowth && branching.Count > 0)
            return branching[_random.Next(branching.Count)];

        if (placed < target - 1 && nonEntranceNonBoss.Count > 0)
            return nonEntranceNonBoss[_random.Next(nonEntranceNonBoss.Count)];

        if (anyNonEntrance.Count > 0)
            return anyNonEntrance[_random.Next(anyNonEntrance.Count)];

        return _segments.Count > 0 ? _segments[_random.Next(_segments.Count)] : null;
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

        var placed = PlaceRoom(dungeon, segment, offset, roomId);
        // The exit used to connect back to the parent is consumed — it must not stay on the
        // frontier (the parent room already occupies that adjacent cell).
        placed.Exits.RemoveAll(e => e.Position == entranceWorldPos);
        return placed;
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
