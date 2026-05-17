using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Dungeons;

public class DungeonGenerator : IDungeonGenerator
{
    private readonly List<RoomSegment> _segments;
    private readonly Dictionary<string, DungeonTemplate> _dungeonTemplates;
    private readonly EncounterTableRegistry? _encounterTables;

    public DungeonGenerator(List<RoomSegment> segments, Dictionary<string, DungeonTemplate>? dungeonTemplates = null, EncounterTableRegistry? encounterTables = null)
    {
        _segments = segments;
        _dungeonTemplates = dungeonTemplates ?? new Dictionary<string, DungeonTemplate>();
        _encounterTables = encounterTables;
    }

    public Dungeon Generate(string dungeonType, int? seed = null)
    {
        var template = _dungeonTemplates.GetValueOrDefault(dungeonType) ?? new DungeonTemplate(
            dungeonType,
            dungeonType,
            new[] { "entrance", "corridor", "chamber", "dead_end", "boss_room" },
            new[] { "entrance", "corridor", "chamber", "dead_end", "boss_room" },
            8,
            "boss-encounter-1",
            dungeonType,
            dungeonType);

        var effectiveSeed = seed ?? StableHash(dungeonType);
        var builder = new DungeonBuilder(effectiveSeed);

        var pool = template.SegmentPool.ToHashSet();
        var ordered = template.SegmentPriority
            .Select(id => _segments.FirstOrDefault(s => s.Id == id))
            .Where(s => s != null)
            .Cast<RoomSegment>()
            .Concat(_segments.Where(s => pool.Contains(s.Id) && !template.SegmentPriority.Contains(s.Id)))
            .ToList();

        foreach (var segment in ordered)
        {
            builder.AddSegment(segment);
        }

        var dungeon = builder.Build(template.Name, template.TargetRooms, _encounterTables, template.EncounterTableId);
        dungeon.WanderingTableId = template.WanderingTableId ?? template.EncounterTableId;
        dungeon.EncounterTableId = template.EncounterTableId;
        TagBossTile(dungeon, template.BossEncounterId);
        return dungeon;
    }

    private static int StableHash(string input)
    {
        // FNV-1a 32-bit hash for stable cross-platform deterministic hashing
        uint hash = 2166136261;
        foreach (var c in input)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return (int)hash;
    }

    private static void TagBossTile(Dungeon dungeon, string encounterId)
    {
        for (int x = 0; x < dungeon.Width; x++)
        {
            for (int y = 0; y < dungeon.Height; y++)
            {
                if (dungeon.Tiles[x, y].Type == TileType.Floor)
                {
                    var entrance = new Position(x, y);
                    var neighbors = new[]
                    {
                        entrance.Move(Direction.South),
                        entrance.Move(Direction.North),
                        entrance.Move(Direction.East),
                        entrance.Move(Direction.West)
                    };
                    foreach (var n in neighbors)
                    {
                        if (dungeon.IsValidPosition(n) && dungeon.Tiles[n.X, n.Y].Type == TileType.Floor)
                        {
                            dungeon.Tiles[n.X, n.Y] = dungeon.Tiles[n.X, n.Y] with { EncounterId = encounterId };
                            return;
                        }
                    }
                }
            }
        }
    }
}
