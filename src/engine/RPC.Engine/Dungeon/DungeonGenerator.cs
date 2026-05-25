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
        var effectiveSeed = seed ?? StableHash(dungeonType);

        // A hand-authored config means there is a registered template for this dungeon type.
        if (_dungeonTemplates.TryGetValue(dungeonType, out var template))
        {
            var authored = BuildFromTemplate(template, effectiveSeed);
            // Validate before use: a hand-authored layout should be fully connected, but if a bad
            // segment set produced an unreachable pocket, drop to the procedural fallback instead
            // of shipping a broken dungeon.
            if (DungeonConnectivityValidator.IsFullyConnected(authored))
                return authored;
        }

        // No hand-authored config (e.g. an LLM-generated campaign referenced an unknown dungeon),
        // or the authored build came out disconnected: procedurally stitch a fallback.
        return BuildProcedural(dungeonType, effectiveSeed, template);
    }

    private Dungeon BuildFromTemplate(DungeonTemplate template, int effectiveSeed)
    {
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

    /// <summary>
    /// Procedurally stitch a dungeon for a type with no usable hand-authored config. Uses the
    /// template's segment pool when one exists, otherwise every loaded segment, then paces
    /// encounters by depth. The stitcher guarantees connectivity; we re-validate before returning.
    /// </summary>
    private Dungeon BuildProcedural(string dungeonType, int effectiveSeed, DungeonTemplate? template)
    {
        var name = template?.Name ?? dungeonType;
        var encounterTableId = template?.EncounterTableId ?? dungeonType;
        var bossEncounterId = template?.BossEncounterId ?? "boss-encounter-1";
        var targetRooms = template?.TargetRooms ?? 8;

        // Restrict to the template pool when given; otherwise stitch from all available segments.
        IEnumerable<RoomSegment> pool = _segments;
        if (template is not null)
        {
            var allowed = template.SegmentPool.ToHashSet();
            var filtered = _segments.Where(s => allowed.Contains(s.Id)).ToList();
            if (filtered.Count > 0) pool = filtered;
        }

        var stitcher = new SegmentStitcher(pool, effectiveSeed);
        var dungeon = stitcher.Stitch(name, targetRooms);
        dungeon.WanderingTableId = template?.WanderingTableId ?? encounterTableId;
        dungeon.EncounterTableId = encounterTableId;

        // Pace encounters across the assembled geometry (depth-scaled difficulty curve).
        if (_encounterTables is not null && _encounterTables.Get(encounterTableId) is not null)
        {
            var pacer = new DungeonPacer();
            var plan = pacer.Plan(dungeon);
            pacer.Apply(dungeon, plan, _encounterTables, encounterTableId, new GameRandom(effectiveSeed));
        }

        TagBossTile(dungeon, bossEncounterId);

        // Connectivity is contractually guaranteed by the stitcher; assert it before use so a future
        // regression surfaces loudly rather than shipping an unreachable dungeon.
        if (!DungeonConnectivityValidator.IsFullyConnected(dungeon))
            throw new InvalidOperationException(
                $"Procedural fallback for '{dungeonType}' produced a disconnected dungeon.");

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
