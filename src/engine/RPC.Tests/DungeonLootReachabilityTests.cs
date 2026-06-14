using System.Text.Json;
using System.Text.Json.Serialization;
using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using Xunit;
using Xunit.Abstractions;

namespace RPC.Tests;

/// <summary>
/// End-to-end engine validation: mirrors the host's real content loading and generates each
/// dungeon template with the SAME default seed the live game uses (DungeonGenerator hashes the
/// dungeon type when no seed is supplied). Asserts every dungeon a player can actually enter
/// places at least one loot tile that is REACHABLE from the entrance — the property the live
/// `window.__rpg` harness found violated for `broken_engine`.
///
/// The existing DungeonGeneratorLootTests only checks "≥1 loot across 30 random seeds", which
/// masks a default-seed dungeon that carries no loot. This test pins the live default.
/// </summary>
public class DungeonLootReachabilityTests
{
    private readonly ITestOutputHelper _out;
    public DungeonLootReachabilityTests(ITestOutputHelper output) => _out = output;

    private const string ContentRoot = "../../../../../../content";

    // Same directory set the host's GameServer.LoadSegments walks.
    private static readonly string[] SegmentDirs =
    {
        "segments", "segments/broken-engine", "segments/bloom-site", "segments/boneyard",
        "segments/sealed-vault", "segments/settlement-gone-wrong", "segments/ossuary",
        "segments/contested-ruin", "segments/underway",
    };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static List<RoomSegment> AllSegments()
    {
        var segments = new List<RoomSegment>();
        foreach (var dir in SegmentDirs)
            segments.AddRange(SegmentLoader.LoadFromDirectory(Path.Combine(ContentRoot, dir)));
        return segments;
    }

    private static Dictionary<string, DungeonTemplate> AllTemplates()
    {
        var templates = new Dictionary<string, DungeonTemplate>();
        var dir = Path.Combine(ContentRoot, "campaigns/dungeons");
        if (!Directory.Exists(dir)) return templates;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            var t = JsonSerializer.Deserialize<DungeonTemplate>(File.ReadAllText(file), JsonOpts);
            if (t is not null) templates[t.Id] = t;
        }
        return templates;
    }

    private static DungeonLootTableRegistry AllLootTables()
    {
        var reg = new DungeonLootTableRegistry();
        var dir = Path.Combine(ContentRoot, "loot");
        if (Directory.Exists(dir))
            foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
                reg.LoadFromJson(Path.GetFileNameWithoutExtension(file), File.ReadAllText(file));
        return reg;
    }

    private static EncounterTableRegistry AllEncounterTables()
    {
        var reg = new EncounterTableRegistry();
        var dir = Path.Combine(ContentRoot, "encounters");
        if (Directory.Exists(dir))
            foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
                reg.LoadFromJson(Path.GetFileNameWithoutExtension(file), File.ReadAllText(file));
        return reg;
    }

    private static readonly Direction[] Dirs = { Direction.North, Direction.East, Direction.South, Direction.West };

    private static (int walkable, int rooms, int loot, int reachableLoot) Analyze(Dungeon d)
    {
        // BFS reachable set from the entrance (StairsUp, else first walkable).
        Position? entrance = null, first = null;
        for (int x = 0; x < d.Width; x++)
            for (int y = 0; y < d.Height; y++)
            {
                if (!d.Tiles[x, y].IsWalkable) continue;
                first ??= new Position(x, y);
                if (d.Tiles[x, y].Type == TileType.StairsUp) entrance ??= new Position(x, y);
            }
        entrance ??= first;

        var reachable = new HashSet<Position>();
        if (entrance is not null)
        {
            var q = new Queue<Position>();
            q.Enqueue(entrance.Value); reachable.Add(entrance.Value);
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                foreach (var dir in Dirs)
                    if (d.CanMoveTo(p, dir))
                    {
                        var n = p.Move(dir);
                        if (reachable.Add(n)) q.Enqueue(n);
                    }
            }
        }

        int walkable = 0, loot = 0, reachableLoot = 0;
        for (int x = 0; x < d.Width; x++)
            for (int y = 0; y < d.Height; y++)
            {
                var t = d.Tiles[x, y];
                if (t.IsWalkable) walkable++;
                if (t.LootId != null)
                {
                    loot++;
                    if (reachable.Contains(new Position(x, y))) reachableLoot++;
                }
            }
        return (walkable, d.Rooms.Count, loot, reachableLoot);
    }

    [Fact]
    public void Every_template_dungeon_places_reachable_loot_at_default_seed()
    {
        var gen = new DungeonGenerator(AllSegments(), AllTemplates(), AllEncounterTables(), AllLootTables());
        var templates = AllTemplates();
        Assert.NotEmpty(templates);

        var failures = new List<string>();
        foreach (var id in templates.Keys.OrderBy(k => k))
        {
            var d = gen.Generate(id); // default seed == live game's StableHash(type)
            var (walkable, rooms, loot, reachableLoot) = Analyze(d);
            _out.WriteLine($"{id,-24} {d.Width}x{d.Height}  walkable={walkable,-4} rooms={rooms,-3} loot={loot,-3} reachableLoot={reachableLoot}");
            if (reachableLoot < 1)
                failures.Add($"{id}: walkable={walkable} rooms={rooms} loot={loot} reachableLoot={reachableLoot}");
        }

        Assert.True(failures.Count == 0,
            "Dungeons with no reachable loot at the live default seed:\n  " + string.Join("\n  ", failures));
    }
}
