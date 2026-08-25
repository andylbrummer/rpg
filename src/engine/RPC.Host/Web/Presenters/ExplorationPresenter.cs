using System.Text.Json;
using System.Text.Json.Serialization;
using RPC.Engine;
using RPC.Engine.Models.Dungeons;

namespace RPC.Host.Web.Presenters;

public record ExplorationViewModel(
    object Player,
    List<object> Tiles,
    RawJson Explored,
    bool HasDungeon,
    string? DungeonType,
    List<object> DetectedSecrets,
    List<object> BreakableWalls);

/// <summary>
/// Builds the exploration slice of a state snapshot: the tiles immediately around the party, the
/// full explored-tile automap, and the secret/breakable-wall overlays.
///
/// The explored automap dominates the snapshot — on a fully explored map it is ~92% of the
/// payload and, rebuilt from scratch, most of the presentation cost, because every entry is a
/// "x,y" key that has to be split and parsed before the tile can be looked up. It is also almost
/// entirely stable between frames: it changes only when a tile is newly explored, a border is
/// altered (a wall broken, a secret door opened), or loot is picked up. So it is memoised and
/// rebuilt only when one of those inputs actually moves. This is per-presenter state, so the
/// presenter is instantiated per server rather than being static.
///
/// It is memoised as encoded JSON rather than as an object graph. Caching the graph alone is a
/// wash: it saves the rebuild but leaves the serializer walking a long-lived, scattered heap
/// every frame, which measured roughly equal to what the rebuild had cost. Caching the bytes
/// skips both, so a frame that does not move the automap pays essentially nothing for it.
/// </summary>
public sealed class ExplorationPresenter
{
    /// <summary>
    /// Options for the memoised fragment. They mirror the host's outbound options so a fragment
    /// serialized here is byte-identical to one the enclosing document would have produced.
    /// </summary>
    private static readonly JsonSerializerOptions FragmentOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private Dungeon? _cachedDungeon;
    private int _cachedTileVersion = -1;
    private int _cachedExploredVersion = -1;
    private int _cachedCollectedLootCount = -1;
    private RawJson _cachedExplored = RawJson.EmptyJsonArray;

    /// <summary>
    /// How many times the explored automap has actually been rebuilt. Instrumentation: it makes
    /// "the cache is still hitting" a deterministic assertion instead of a timing measurement,
    /// and tells you at a glance whether an invalidation input is churning every frame.
    /// </summary>
    public int ExploredRebuildCount { get; private set; }

    public ExplorationViewModel Present(GameState state)
    {
        var tiles = new List<object>();
        var detectedSecrets = new List<object>();
        var breakableWalls = new List<object>();
        var explored = RawJson.EmptyJsonArray;

        if (state.CurrentDungeon != null)
        {
            var px = state.Player.Position.X;
            var py = state.Player.Position.Y;
            const int sendRadius = 8;
            var collected = state.Exploration.CollectedLoot;

            for (int x = Math.Max(0, px - sendRadius); x < Math.Min(state.CurrentDungeon.Width, px + sendRadius + 1); x++)
            {
                for (int y = Math.Max(0, py - sendRadius); y < Math.Min(state.CurrentDungeon.Height, py + sendRadius + 1); y++)
                {
                    var tile = state.CurrentDungeon.Tiles[x, y];
                    if (tile.Type != TileType.Empty)
                    {
                        tiles.Add(SerializeTile(x, y, tile, collected));
                    }
                }
            }

            explored = PresentExplored(state);

            // Cartographer-detected-but-unrevealed secrets: the client automap marks these "?".
            foreach (var secret in state.Secrets.All)
            {
                if (secret.X is not int sx || secret.Y is not int sy) continue;
                if (!state.Journal.IsDetected(secret.Id) || state.Journal.IsDiscovered(secret.Id)) continue;
                detectedSecrets.Add(new { id = secret.Id, x = sx, y = sy, wall = secret.Wall });
            }

            // Discovered, still-intact breakable walls: these are exactly the walls the engine will
            // accept a break action for (ExplorationService.BreakWall requires IsDiscovered). Disjoint
            // from detectedSecrets (the "?" search set) by JournalState's detected/discovered split.
            // A wall whose border has already been opened (None) — or is no longer breakable material —
            // drops out so the Break affordance disappears once the wall is breached.
            foreach (var secret in state.Secrets.All)
            {
                if (secret.Type != "breakable_wall") continue;
                if (secret.X is not int sx || secret.Y is not int sy) continue;
                if (!state.Journal.IsDiscovered(secret.Id)) continue;
                if (!Enum.TryParse<Direction>(secret.Wall, ignoreCase: true, out var dir)) continue;

                var border = state.CurrentDungeon.GetTile(new Position(sx, sy)).GetBorder(dir);
                if (border is not (BorderType.BreakableWall or BorderType.CrackedWall)) continue;

                breakableWalls.Add(new { id = secret.Id, x = sx, y = sy, wall = secret.Wall });
            }
        }

        return new ExplorationViewModel(
            new
            {
                x = state.Player.Position.X,
                y = state.Player.Position.Y,
                facing = state.Player.Facing.ToString()
            },
            tiles,
            explored,
            state.CurrentDungeon != null,
            state.CurrentDungeonType,
            detectedSecrets,
            breakableWalls);
    }

    /// <summary>
    /// Returns the explored automap, rebuilding it only when one of its inputs has moved.
    ///
    /// The key covers every way the rendered result can change: which dungeon is loaded, any tile
    /// write (border and type changes both flow through TileGrid.Version), any change to the
    /// explored set (BoundedTileSet.Version, which also moves on the evicting add that leaves
    /// Count untouched), and loot pickups. Collected loot is only ever added to within a run —
    /// it is cleared only alongside the explored set, whose version then moves — so its count is
    /// a sound key component.
    /// </summary>
    private RawJson PresentExplored(GameState state)
    {
        var dungeon = state.CurrentDungeon!;
        var exploredTiles = state.ExploredTiles;
        var collected = state.Exploration.CollectedLoot;

        if (ReferenceEquals(_cachedDungeon, dungeon)
            && _cachedTileVersion == dungeon.Tiles.Version
            && _cachedExploredVersion == exploredTiles.Version
            && _cachedCollectedLootCount == collected.Count)
        {
            return _cachedExplored;
        }

        var explored = new List<object>(exploredTiles.Count);
        foreach (var key in exploredTiles)
        {
            if (!TryParseTileKey(key, out var x, out var y)) continue;
            // Bounds-check rather than indexing blind: a save restored against a differently
            // sized dungeon carries keys that would otherwise throw IndexOutOfRange here and
            // take down the whole snapshot.
            if (x < 0 || y < 0 || x >= dungeon.Width || y >= dungeon.Height) continue;
            explored.Add(SerializeTile(x, y, dungeon.Tiles[x, y], collected));
        }

        _cachedDungeon = dungeon;
        _cachedTileVersion = dungeon.Tiles.Version;
        _cachedExploredVersion = exploredTiles.Version;
        _cachedCollectedLootCount = collected.Count;
        _cachedExplored = RawJson.Serialize(explored, FragmentOptions);
        ExploredRebuildCount++;
        return _cachedExplored;
    }

    /// <summary>
    /// Parses an "x,y" explored-tile key without allocating the intermediate string array that
    /// Split would. Malformed keys are rejected rather than throwing.
    /// </summary>
    private static bool TryParseTileKey(string key, out int x, out int y)
    {
        x = 0;
        y = 0;
        var comma = key.IndexOf(',');
        if (comma <= 0 || comma == key.Length - 1) return false;

        return int.TryParse(key.AsSpan(0, comma), out x)
            && int.TryParse(key.AsSpan(comma + 1), out y);
    }

    private static object SerializeTile(int x, int y, Tile tile, HashSet<string> collected)
    {
        bool hasLoot = tile.LootId != null && !collected.Contains($"{x},{y}");
        return new
        {
            x, y,
            type = tile.Type.ToString(),
            north = tile.North.ToString(), south = tile.South.ToString(),
            east = tile.East.ToString(), west = tile.West.ToString(),
            hasLoot,
            lootName = hasLoot ? tile.LootId : null
        };
    }
}
