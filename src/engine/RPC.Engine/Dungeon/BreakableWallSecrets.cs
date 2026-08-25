using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Dungeons;

/// <summary>
/// Registers a discoverable secret for every breakable wall a generated dungeon actually contains.
/// <para>
/// Segments author <c>BreakableWall</c> borders, and the stitcher places them wherever the layout
/// puts that segment. Everything that finds or opens such a wall — the Cartographer's proximity
/// detection, an explicit search, the area-damage reveal, the break action — addresses a
/// <see cref="SecretDef"/> carrying a tile position, and nothing ever created one for a stitched
/// wall. The authored walls were therefore indistinguishable from solid ones, and the whole
/// secret-detection subsystem had nothing to detect in a real dungeon.
/// </para>
/// </summary>
public static class BreakableWallSecrets
{
    /// <summary>The hint shown for a wall discovered from the layout rather than authored by hand.</summary>
    public const string Hint = "The mortar here is split, and the stone rings hollow behind it.";

    /// <summary>Deterministic id for the wall on one side of a tile, stable across save and reload.</summary>
    public static string IdFor(int x, int y, Direction wall) => $"breakable_wall_{x}_{y}_{wall}";

    /// <summary>
    /// Registers one secret per breakable wall in <paramref name="dungeon"/>.
    /// <para>
    /// Only the north and west borders of each tile are scanned. A wall sits between two tiles and
    /// is recorded on both — the north border of (x, y) is the south border of (x, y-1) — so
    /// scanning two of the four directions covers every wall exactly once instead of registering
    /// each one twice under two different ids.
    /// </para>
    /// <para>
    /// A wall already covered by an authored secret at the same tile and side is left to it, so
    /// hand-written hints and bloodline gates keep precedence over the generated fallback.
    /// </para>
    /// </summary>
    public static void RegisterFrom(SecretRegistry secrets, Dungeon dungeon)
    {
        var authored = secrets.All
            .Where(s => s.X is int && s.Y is int && !string.IsNullOrEmpty(s.Wall))
            .Select(s => (s.X!.Value, s.Y!.Value, s.Wall!))
            .ToHashSet();

        for (int x = 0; x < dungeon.Width; x++)
        {
            for (int y = 0; y < dungeon.Height; y++)
            {
                var tile = dungeon.Tiles[x, y];
                Register(x, y, Direction.North, tile.North);
                Register(x, y, Direction.West, tile.West);
            }
        }

        void Register(int x, int y, Direction wall, BorderType border)
        {
            if (border is not (BorderType.BreakableWall or BorderType.CrackedWall)) return;
            if (authored.Contains((x, y, wall.ToString()))) return;

            secrets.Register(new SecretDef(
                IdFor(x, y, wall), "breakable_wall", DocLinkId: null, Hint: Hint,
                BloodlineRequirement: null, X: x, Y: y, Wall: wall.ToString()));
        }
    }
}
