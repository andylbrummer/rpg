using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Dungeons;

/// <summary>
/// Places loot on tiles, biased by room role, deterministically from a seed. Mutates the dungeon's
/// tiles in place (sets <see cref="Tile.LootId"/>). Pure w.r.t. (dungeon geometry, roles, table, seed).
/// </summary>
public class DungeonLootPlacer
{
    private static int ChanceFor(RoomRole role) => role switch
    {
        RoomRole.DeadEnd => 60,
        RoomRole.SideBranch => 30,
        RoomRole.Critical => 20,
        _ => 0, // Entrance, Boss
    };

    public void Place(Dungeon dungeon, IReadOnlyDictionary<int, RoomRole> roles,
        DungeonLootTableDef table, int seed)
    {
        var rng = new GameRandom(seed);

        // Deterministic room order by id so the RNG stream is stable.
        foreach (var room in dungeon.Rooms.OrderBy(r => r.Id))
        {
            if (!roles.TryGetValue(room.Id, out var role)) continue;
            int chance = ChanceFor(role);
            if (chance <= 0) continue;

            // Roll placement, THEN roll item — keep both rolls inside the loop so a no-place room
            // still consumes exactly one placement roll (stable stream regardless of layout).
            bool place = rng.Roll(1, 100) <= chance;
            if (!place) continue;

            var tile = LowestFloorTile(dungeon, room.Id);
            if (tile is null) continue;

            int total = table.Entries.Sum(e => e.Weight);
            if (total <= 0) continue;
            int roll = rng.Roll(1, total);
            int cumulative = 0;
            string itemId = table.Entries[^1].ItemId;
            foreach (var entry in table.Entries)
            {
                cumulative += entry.Weight;
                if (roll <= cumulative) { itemId = entry.ItemId; break; }
            }

            var p = tile.Value;
            // Never decorate entrance/boss tiles even if a room somehow contains one.
            var t = dungeon.Tiles[p.X, p.Y];
            if (t.Type == TileType.StairsUp || t.EncounterId != null) continue;
            dungeon.Tiles[p.X, p.Y] = t with { LootId = itemId };
        }
    }

    private static Position? LowestFloorTile(Dungeon dungeon, int roomId)
    {
        Position? best = null;
        for (int y = 0; y < dungeon.Height; y++)
            for (int x = 0; x < dungeon.Width; x++)
            {
                var t = dungeon.Tiles[x, y];
                if (t.RoomId != roomId || t.Type != TileType.Floor) continue;
                if (t.EncounterId != null) continue; // skip boss-tagged
                best ??= new Position(x, y);
            }
        return best;
    }
}
