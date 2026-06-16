using RPC.Engine;
using RPC.Engine.Models.Dungeons;

namespace RPC.Host.Web.Presenters;

public record ExplorationViewModel(
    object Player,
    List<object> Tiles,
    List<object> Explored,
    bool HasDungeon,
    string? DungeonType,
    List<object> DetectedSecrets);

public static class ExplorationPresenter
{
    public static ExplorationViewModel Present(GameState state)
    {
        var tiles = new List<object>();
        var explored = new List<object>();
        var detectedSecrets = new List<object>();
        var collected = state.Exploration.CollectedLoot;

        if (state.CurrentDungeon != null)
        {
            var px = state.Player.Position.X;
            var py = state.Player.Position.Y;
            const int sendRadius = 8;

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

            foreach (var key in state.ExploredTiles)
            {
                var parts = key.Split(',');
                var x = int.Parse(parts[0]);
                var y = int.Parse(parts[1]);
                var tile = state.CurrentDungeon.Tiles[x, y];
                explored.Add(SerializeTile(x, y, tile, collected));
            }

            // Cartographer-detected-but-unrevealed secrets: the client automap marks these "?".
            foreach (var secret in state.Secrets.All)
            {
                if (secret.X is not int sx || secret.Y is not int sy) continue;
                if (!state.Journal.IsDetected(secret.Id) || state.Journal.IsDiscovered(secret.Id)) continue;
                detectedSecrets.Add(new { id = secret.Id, x = sx, y = sy, wall = secret.Wall });
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
            detectedSecrets);
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
