using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Dungeons;

public class DungeonGenerator : IDungeonGenerator
{
    // Default boss encounter id used when a template does not specify one. Shared so the build
    // paths and the loot classifier identify the same tile and cannot drift.
    private const string DefaultBossEncounterId = "boss-encounter-1";

    private readonly List<RoomSegment> _segments;
    private readonly Dictionary<string, DungeonTemplate> _dungeonTemplates;
    private readonly EncounterTableRegistry? _encounterTables;
    private readonly DungeonLootTableRegistry? _lootTables;

    public DungeonGenerator(List<RoomSegment> segments, Dictionary<string, DungeonTemplate>? dungeonTemplates = null, EncounterTableRegistry? encounterTables = null, DungeonLootTableRegistry? lootTables = null)
    {
        _segments = segments;
        _dungeonTemplates = dungeonTemplates ?? new Dictionary<string, DungeonTemplate>();
        _encounterTables = encounterTables;
        _lootTables = lootTables;
    }

    /// <summary>Classify rooms and place loot. No-op when no loot table resolves.</summary>
    private void Decorate(Dungeon dungeon, string dungeonType, DungeonTemplate? template, int effectiveSeed)
    {
        if (_lootTables is null) return;
        var lootTableId = template?.LootTableId ?? dungeonType;
        var table = _lootTables.Get(lootTableId) ?? _lootTables.Get("default");
        if (table is null) return;

        // The stitcher/builder tag each tile with a RoomId but do not populate dungeon.Rooms;
        // the classifier works off Rooms, so derive room bounds from the tile RoomIds first.
        EnsureRooms(dungeon);

        // Identify the boss by its specific encounter id (same value the build paths tag with), so the
        // classifier does not latch onto an arbitrary pacer-tagged encounter on the procedural path.
        var bossId = template?.BossEncounterId ?? DefaultBossEncounterId;
        var roles = new DungeonPathClassifier().Classify(dungeon, bossId);
        new DungeonLootPlacer().Place(dungeon, roles, table, effectiveSeed);
    }

    /// <summary>Derive <see cref="Dungeon.Rooms"/> from per-tile RoomIds when not already populated.</summary>
    private static void EnsureRooms(Dungeon dungeon)
    {
        if (dungeon.Rooms.Count > 0) return;

        var bounds = new Dictionary<int, (int minX, int minY, int maxX, int maxY)>();
        for (int x = 0; x < dungeon.Width; x++)
            for (int y = 0; y < dungeon.Height; y++)
            {
                var tile = dungeon.Tiles[x, y];
                if (!tile.IsWalkable || tile.RoomId < 0) continue;
                if (bounds.TryGetValue(tile.RoomId, out var b))
                    bounds[tile.RoomId] = (Math.Min(b.minX, x), Math.Min(b.minY, y),
                        Math.Max(b.maxX, x), Math.Max(b.maxY, y));
                else
                    bounds[tile.RoomId] = (x, y, x, y);
            }

        foreach (var (id, b) in bounds.OrderBy(kv => kv.Key))
            dungeon.Rooms.Add(new RoomInfo
            {
                Id = id,
                Min = new Position(b.minX, b.minY),
                Max = new Position(b.maxX, b.maxY)
            });
    }

    public DungeonGenerationResult Generate(DungeonGenerationRequest request)
    {
        var effectiveSeed = request.Seed ?? StableHash(request.DungeonType);
        var dungeon = Build(request.DungeonType, effectiveSeed);
        var identity = new DungeonGenerationIdentity(request.DungeonType, effectiveSeed, request.ContentHash);
        return new DungeonGenerationResult(dungeon, identity);
    }

    public Dungeon Generate(string dungeonType, int? seed = null) =>
        Generate(new DungeonGenerationRequest(dungeonType, seed)).Dungeon;

    private Dungeon Build(string dungeonType, int effectiveSeed)
    {
        // A hand-authored config means there is a registered template for this dungeon type.
        _dungeonTemplates.TryGetValue(dungeonType, out var template);
        if (template is not null)
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
        Decorate(dungeon, template.Id, template, effectiveSeed);
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
        var bossEncounterId = template?.BossEncounterId ?? DefaultBossEncounterId;
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

        Decorate(dungeon, dungeonType, template, effectiveSeed);

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
