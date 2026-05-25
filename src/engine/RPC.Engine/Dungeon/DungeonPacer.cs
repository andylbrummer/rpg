using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Dungeons;

/// <summary>
/// Difficulty/pacing curve over an already-assembled dungeon. Walks the tile graph from the
/// entrance (reusing the same BFS-depth notion as <see cref="DungeonConnectivityValidator"/>) and
/// produces a pacing plan: encounters spaced out along the depth axis, breather rooms in the gaps,
/// a danger-rating curve that ramps from the entrance to the boss, and a boss-distance check.
/// </summary>
public class DungeonPacer
{
    public record PacingConfig(
        int EncounterSpacing = 2,   // minimum BFS-depth gap between consecutive encounters
        int MinDanger = 1,          // danger rating at the entrance
        int MaxDanger = 10,         // danger rating at the boss
        double BossDistanceFraction = 0.75) // boss must sit at least this fraction of max depth away
    {
        public static PacingConfig Default { get; } = new();
    }

    public record EncounterPlacement(Position Position, int Depth, int DangerRating);

    public record PacingPlan(
        IReadOnlyList<EncounterPlacement> Encounters,
        IReadOnlyList<Position> BreatherRooms,
        Position? Boss,
        int BossDepth,
        int MaxDepth,
        bool BossDistanceMet);

    private static readonly Direction[] Directions =
        { Direction.North, Direction.East, Direction.South, Direction.West };

    /// <summary>Analyze an assembled dungeon and produce a pacing plan. Pure: does not mutate the dungeon.</summary>
    public PacingPlan Plan(Dungeon dungeon, PacingConfig? config = null)
    {
        config ??= PacingConfig.Default;

        var depths = ComputeDepths(dungeon, out var entrance);
        if (depths.Count == 0 || entrance is null)
            return new PacingPlan(Array.Empty<EncounterPlacement>(), Array.Empty<Position>(), null, 0, 0, true);

        int maxDepth = depths.Values.Max();

        // Boss sits on the down-stairs if present, otherwise the deepest floor tile.
        Position? boss = depths.Keys
            .Where(p => dungeon.Tiles[p.X, p.Y].Type == TileType.StairsDown)
            .OrderByDescending(p => depths[p]).ThenBy(p => p.X).ThenBy(p => p.Y)
            .Cast<Position?>()
            .FirstOrDefault()
            ?? depths.Keys
                .Where(p => dungeon.Tiles[p.X, p.Y].Type == TileType.Floor)
                .OrderByDescending(p => depths[p]).ThenBy(p => p.X).ThenBy(p => p.Y)
                .Cast<Position?>()
                .FirstOrDefault();

        int bossDepth = boss is null ? 0 : depths[boss.Value];
        int bossTarget = (int)Math.Ceiling(maxDepth * config.BossDistanceFraction);
        bool bossDistanceMet = bossDepth >= bossTarget;

        // Encounter candidates: ordinary floor tiles, excluding the entrance and the boss tile.
        var candidates = depths.Keys
            .Where(p => dungeon.Tiles[p.X, p.Y].Type == TileType.Floor)
            .Where(p => p != entrance.Value && (boss is null || p != boss.Value))
            .OrderBy(p => depths[p]).ThenBy(p => p.X).ThenBy(p => p.Y)
            .ToList();

        // Greedily pick encounters spaced at least EncounterSpacing apart in depth — the tiles left
        // between picks become breather rooms.
        var encounters = new List<EncounterPlacement>();
        int spacing = Math.Max(1, config.EncounterSpacing);
        // Start one full spacing back so the first eligible candidate is always selected
        // (avoids int overflow from comparing against int.MinValue).
        int lastDepth = -spacing;
        foreach (var pos in candidates)
        {
            int d = depths[pos];
            if (d - lastDepth < spacing) continue;
            lastDepth = d;
            encounters.Add(new EncounterPlacement(pos, d, DangerForDepth(d, maxDepth, config)));
        }

        var breathers = SelectBreathers(depths, encounters, entrance.Value, boss);

        return new PacingPlan(encounters, breathers, boss, bossDepth, maxDepth, bossDistanceMet);
    }

    /// <summary>Apply a plan by tagging encounter tiles with depth-scaled rolls from the table.</summary>
    public void Apply(Dungeon dungeon, PacingPlan plan, EncounterTableRegistry tables, string tableId, GameRandom rng)
    {
        foreach (var placement in plan.Encounters)
        {
            var pos = placement.Position;
            if (!dungeon.IsValidPosition(pos)) continue;
            var tile = dungeon.Tiles[pos.X, pos.Y];
            if (!tile.IsWalkable) continue;
            var enc = tables.RollEncounter(tableId, rng, placement.DangerRating);
            dungeon.Tiles[pos.X, pos.Y] = tile with { EncounterId = enc.Id };
        }
    }

    private static int DangerForDepth(int depth, int maxDepth, PacingConfig config)
    {
        if (maxDepth <= 0) return config.MinDanger;
        double t = Math.Clamp((double)depth / maxDepth, 0, 1);
        int value = (int)Math.Round(config.MinDanger + t * (config.MaxDanger - config.MinDanger));
        return Math.Clamp(value, config.MinDanger, config.MaxDanger);
    }

    /// <summary>One breather tile per gap between consecutive encounter depths, nearest the midpoint.</summary>
    private static List<Position> SelectBreathers(
        IReadOnlyDictionary<Position, int> depths,
        IReadOnlyList<EncounterPlacement> encounters,
        Position entrance,
        Position? boss)
    {
        var breathers = new List<Position>();
        if (encounters.Count < 2) return breathers;

        var used = new HashSet<Position>(encounters.Select(e => e.Position)) { entrance };
        if (boss is not null) used.Add(boss.Value);

        for (int i = 0; i + 1 < encounters.Count; i++)
        {
            int lo = encounters[i].Depth;
            int hi = encounters[i + 1].Depth;
            if (hi - lo < 2) continue; // no room for a breather between adjacent depths
            double mid = (lo + hi) / 2.0;

            Position? best = null;
            double bestScore = double.MaxValue;
            foreach (var (pos, d) in depths)
            {
                if (d <= lo || d >= hi) continue;
                if (used.Contains(pos)) continue;
                double score = Math.Abs(d - mid);
                if (best is null || score < bestScore ||
                    (score == bestScore && (pos.X < best.Value.X || (pos.X == best.Value.X && pos.Y < best.Value.Y))))
                {
                    bestScore = score;
                    best = pos;
                }
            }

            if (best is not null)
            {
                breathers.Add(best.Value);
                used.Add(best.Value);
            }
        }

        return breathers;
    }

    /// <summary>BFS depth from the entrance for every reachable walkable tile.</summary>
    private static Dictionary<Position, int> ComputeDepths(Dungeon dungeon, out Position? entrance)
    {
        entrance = null;
        Position? first = null;
        for (int x = 0; x < dungeon.Width; x++)
        {
            for (int y = 0; y < dungeon.Height; y++)
            {
                var tile = dungeon.Tiles[x, y];
                if (!tile.IsWalkable) continue;
                var pos = new Position(x, y);
                first ??= pos;
                if (tile.Type == TileType.StairsUp && entrance is null)
                    entrance = pos;
            }
        }
        entrance ??= first;

        var depths = new Dictionary<Position, int>();
        if (entrance is null) return depths;

        depths[entrance.Value] = 0;
        var queue = new Queue<Position>();
        queue.Enqueue(entrance.Value);
        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            int d = depths[pos];
            foreach (var dir in Directions)
            {
                if (!dungeon.CanMoveTo(pos, dir)) continue;
                var next = pos.Move(dir);
                if (!depths.ContainsKey(next))
                {
                    depths[next] = d + 1;
                    queue.Enqueue(next);
                }
            }
        }

        return depths;
    }
}
