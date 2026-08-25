# Dungeon Decoration — Critical Path + Loot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a playable vertical slice of dungeon decoration — deterministic critical-path classification + role-biased loot placement, rendered in 3D and pickable into the expedition cache.

**Architecture:** Two new pure engine modules (`DungeonPathClassifier`, `DungeonLootPlacer`) mirror the existing `DungeonPacer` shape — stateless, seeded, independently tested. They run inside `DungeonGenerator` after stitching/pacing. Loot is tile data (`Tile.LootId`); collected-state is runtime/save data (`ExplorationState.CollectedLoot`). A new `PickupCommand` grants loot to the existing expedition cache. The client renders a glint marker and a "Take" prompt.

**Tech Stack:** C# / .NET 9 engine (xUnit tests), Svelte 5 + Three.js client, JSON content validated by `tools/content-pack`.

**Spec:** `docs/superpowers/specs/2026-06-14-dungeon-decoration-loot-design.md`

**Build/test commands (run from repo root unless noted):**
- Engine build: `dotnet build src/engine --configuration Release -nodeReuse:false`
- Engine test (one): `dotnet test src/engine --configuration Release -nodeReuse:false --filter "FullyQualifiedName~<TestClass>"`
- Engine test (all): `dotnet test src/engine --configuration Release -nodeReuse:false`
- Content validate: `dotnet run --project tools/content-pack -nodeReuse:false -- content /tmp/cp-out`
- Client typecheck: `cd src/client && npm run check`

> Always pass `-nodeReuse:false` to dotnet here — this box accumulates orphan MSBuild nodes otherwise.

---

## Task 1: Add `Tile.LootId` field and `RoomRole` enum

**Files:**
- Modify: `src/engine/RPC.Engine/Models/Dungeon.cs:24-31`
- Create: `src/engine/RPC.Engine/Dungeon/RoomRole.cs`

- [ ] **Step 1: Add `LootId` to the `Tile` record struct**

In `src/engine/RPC.Engine/Models/Dungeon.cs`, change the `Tile` record header (lines 24-31) to add a final optional parameter:

```csharp
public readonly record struct Tile(
    TileType Type,
    BorderType North = BorderType.None,
    BorderType South = BorderType.None,
    BorderType East = BorderType.None,
    BorderType West = BorderType.None,
    int RoomId = -1,
    string? EncounterId = null,
    string? LootId = null)
```

(Body unchanged — `IsWalkable`, `GetBorder`, `WithBorder` stay as-is. `with { LootId = ... }` works automatically.)

- [ ] **Step 2: Create the `RoomRole` enum**

Create `src/engine/RPC.Engine/Dungeon/RoomRole.cs`:

```csharp
namespace RPC.Engine.Dungeons;

/// <summary>
/// Structural role of a room within an assembled dungeon, derived from the critical
/// path (entrance → boss). Drives loot bias and (later) interactable/secret placement.
/// </summary>
public enum RoomRole
{
    Entrance,
    Boss,
    Critical,
    SideBranch,
    DeadEnd
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/engine --configuration Release -nodeReuse:false`
Expected: Build succeeded, 0 errors. (Existing tests untouched.)

- [ ] **Step 4: Commit**

```bash
git add src/engine/RPC.Engine/Models/Dungeon.cs src/engine/RPC.Engine/Dungeon/RoomRole.cs
git commit -m "feat(dungeon): add Tile.LootId and RoomRole enum"
```

---

## Task 2: `DungeonPathClassifier` — classify rooms by critical path

**Files:**
- Create: `src/engine/RPC.Engine/Dungeon/DungeonPathClassifier.cs`
- Test: `src/engine/RPC.Tests/DungeonPathClassifierTests.cs`

**Background:** `DungeonPacer.ComputeDepths` (in `src/engine/RPC.Engine/Dungeon/DungeonPacer.cs:158-199`) shows the BFS walk pattern: entrance = first `StairsUp` tile (else first walkable); neighbors via `dungeon.CanMoveTo(pos, dir)`. The boss tile is tagged with an `EncounterId` by `DungeonGenerator.TagBossTile`. Rooms come from `dungeon.Rooms` (`RoomInfo` with `Id`, `Min`, `Max`). A tile's room is `tile.RoomId`.

- [ ] **Step 1: Write the failing test**

Create `src/engine/RPC.Tests/DungeonPathClassifierTests.cs`. Build a small hand-made dungeon: a straight corridor entrance→boss (rooms on the path) plus one dead-end branch.

```csharp
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using Xunit;

namespace RPC.Tests;

public class DungeonPathClassifierTests
{
    // Layout (x →, y ↓), all Floor, doors between adjacent room tiles:
    //   (0,0)=StairsUp room0   (1,0)=room1   (2,0)=boss room2
    //                                  (1,1)=room3 dead-end (branch off room1)
    private static Dungeon BuildLine()
    {
        var d = new Dungeon(3, 2, "test");
        void Floor(int x, int y, int roomId) =>
            d.Tiles[x, y] = new Tile(x == 0 && y == 0 ? TileType.StairsUp : TileType.Floor, RoomId: roomId);

        Floor(0, 0, 0);
        Floor(1, 0, 1);
        Floor(2, 0, 2);
        Floor(1, 1, 3);

        // Open doors both directions between connected tiles.
        d.Tiles[0, 0] = d.Tiles[0, 0] with { East = BorderType.Door };
        d.Tiles[1, 0] = d.Tiles[1, 0] with { West = BorderType.Door, East = BorderType.Door, South = BorderType.Door };
        d.Tiles[2, 0] = d.Tiles[2, 0] with { West = BorderType.Door };
        d.Tiles[1, 1] = d.Tiles[1, 1] with { North = BorderType.Door };

        // Boss tile tagged like TagBossTile does.
        d.Tiles[2, 0] = d.Tiles[2, 0] with { EncounterId = "boss-1" };

        d.Rooms.Add(new RoomInfo { Id = 0, Min = new Position(0, 0), Max = new Position(0, 0) });
        d.Rooms.Add(new RoomInfo { Id = 1, Min = new Position(1, 0), Max = new Position(1, 0) });
        d.Rooms.Add(new RoomInfo { Id = 2, Min = new Position(2, 0), Max = new Position(2, 0) });
        d.Rooms.Add(new RoomInfo { Id = 3, Min = new Position(1, 1), Max = new Position(1, 1) });
        return d;
    }

    [Fact]
    public void Classify_assigns_entrance_boss_critical_and_deadend()
    {
        var roles = new DungeonPathClassifier().Classify(BuildLine());

        Assert.Equal(RoomRole.Entrance, roles[0]);
        Assert.Equal(RoomRole.Critical, roles[1]); // on path entrance→boss
        Assert.Equal(RoomRole.Boss, roles[2]);
        Assert.Equal(RoomRole.DeadEnd, roles[3]); // single connection, off path
    }

    [Fact]
    public void Classify_is_deterministic()
    {
        var a = new DungeonPathClassifier().Classify(BuildLine());
        var b = new DungeonPathClassifier().Classify(BuildLine());
        Assert.Equal(a, b);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/engine --configuration Release -nodeReuse:false --filter "FullyQualifiedName~DungeonPathClassifierTests"`
Expected: FAIL — `DungeonPathClassifier` does not exist (compile error).

- [ ] **Step 3: Implement `DungeonPathClassifier`**

Create `src/engine/RPC.Engine/Dungeon/DungeonPathClassifier.cs`:

```csharp
using RPC.Engine.Models.Dungeons;

namespace RPC.Engine.Dungeons;

/// <summary>
/// Classifies every room in an assembled dungeon by its relationship to the critical path
/// (entrance → boss). Pure and RNG-free: output depends only on geometry.
/// </summary>
public class DungeonPathClassifier
{
    private static readonly Direction[] Directions =
        { Direction.North, Direction.East, Direction.South, Direction.West };

    public IReadOnlyDictionary<int, RoomRole> Classify(Dungeon dungeon)
    {
        var result = new Dictionary<int, RoomRole>();
        if (dungeon.Rooms.Count == 0) return result;

        var entrance = FindEntrance(dungeon);
        var boss = FindBoss(dungeon);
        var criticalTiles = entrance is null || boss is null
            ? new HashSet<Position>()
            : ShortestPath(dungeon, entrance.Value, boss.Value);

        int entranceRoom = entrance is null ? -1 : dungeon.Tiles[entrance.Value.X, entrance.Value.Y].RoomId;
        int bossRoom = boss is null ? -1 : dungeon.Tiles[boss.Value.X, boss.Value.Y].RoomId;

        foreach (var room in dungeon.Rooms)
        {
            if (room.Id == entranceRoom) { result[room.Id] = RoomRole.Entrance; continue; }
            if (room.Id == bossRoom) { result[room.Id] = RoomRole.Boss; continue; }

            bool onPath = RoomTiles(dungeon, room.Id).Any(criticalTiles.Contains);
            if (onPath) { result[room.Id] = RoomRole.Critical; continue; }

            int connections = CountConnections(dungeon, room.Id);
            result[room.Id] = connections <= 1 ? RoomRole.DeadEnd : RoomRole.SideBranch;
        }

        return result;
    }

    private static IEnumerable<Position> RoomTiles(Dungeon dungeon, int roomId)
    {
        for (int x = 0; x < dungeon.Width; x++)
            for (int y = 0; y < dungeon.Height; y++)
                if (dungeon.Tiles[x, y].RoomId == roomId && dungeon.Tiles[x, y].IsWalkable)
                    yield return new Position(x, y);
    }

    // A "connection" is a walkable step from a room tile into a tile belonging to a different room.
    private static int CountConnections(Dungeon dungeon, int roomId)
    {
        var seen = new HashSet<int>();
        int count = 0;
        foreach (var pos in RoomTiles(dungeon, roomId))
        {
            foreach (var dir in Directions)
            {
                if (!dungeon.CanMoveTo(pos, dir)) continue;
                var next = pos.Move(dir);
                int otherRoom = dungeon.Tiles[next.X, next.Y].RoomId;
                if (otherRoom != roomId && seen.Add(otherRoom)) count++;
            }
        }
        return count;
    }

    private static Position? FindEntrance(Dungeon dungeon)
    {
        Position? first = null;
        for (int x = 0; x < dungeon.Width; x++)
            for (int y = 0; y < dungeon.Height; y++)
            {
                var t = dungeon.Tiles[x, y];
                if (!t.IsWalkable) continue;
                first ??= new Position(x, y);
                if (t.Type == TileType.StairsUp) return new Position(x, y);
            }
        return first;
    }

    private static Position? FindBoss(Dungeon dungeon)
    {
        for (int x = 0; x < dungeon.Width; x++)
            for (int y = 0; y < dungeon.Height; y++)
                if (dungeon.Tiles[x, y].EncounterId != null && dungeon.Tiles[x, y].IsWalkable)
                    return new Position(x, y);
        return null;
    }

    // BFS shortest path; returns the set of tiles on one shortest entrance→boss path.
    private static HashSet<Position> ShortestPath(Dungeon dungeon, Position start, Position goal)
    {
        var prev = new Dictionary<Position, Position>();
        var visited = new HashSet<Position> { start };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            if (pos == goal) break;
            foreach (var dir in Directions)
            {
                if (!dungeon.CanMoveTo(pos, dir)) continue;
                var next = pos.Move(dir);
                if (visited.Add(next)) { prev[next] = pos; queue.Enqueue(next); }
            }
        }

        var path = new HashSet<Position>();
        if (start != goal && !prev.ContainsKey(goal)) return path; // unreachable
        var cur = goal;
        path.Add(cur);
        while (cur != start && prev.TryGetValue(cur, out var p)) { cur = p; path.Add(cur); }
        return path;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/engine --configuration Release -nodeReuse:false --filter "FullyQualifiedName~DungeonPathClassifierTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/engine/RPC.Engine/Dungeon/DungeonPathClassifier.cs src/engine/RPC.Tests/DungeonPathClassifierTests.cs
git commit -m "feat(dungeon): classify rooms by critical path"
```

---

## Task 3: `DungeonLootTable` record + registry

**Files:**
- Create: `src/engine/RPC.Engine/Dungeon/DungeonLootTable.cs`
- Test: `src/engine/RPC.Tests/DungeonLootTableRegistryTests.cs`

**Background:** Mirror `EncounterTableRegistry` (`src/engine/RPC.Engine/Combat/EncounterTable.cs`): JSON load into a dictionary, weighted roll via `GameRandom.Roll(1, totalWeight)`.

- [ ] **Step 1: Write the failing test**

Create `src/engine/RPC.Tests/DungeonLootTableRegistryTests.cs`:

```csharp
using RPC.Engine;
using RPC.Engine.Dungeons;
using Xunit;

namespace RPC.Tests;

public class DungeonLootTableRegistryTests
{
    private const string Json = """
    { "id": "broken_engine", "entries": [
        { "itemId": "scrap_plating", "weight": 3 },
        { "itemId": "engine_tech_manual", "weight": 1 }
    ] }
    """;

    [Fact]
    public void Roll_is_deterministic_for_same_seed()
    {
        var reg = new DungeonLootTableRegistry();
        reg.LoadFromJson("broken_engine", Json);

        var a = reg.Roll("broken_engine", new GameRandom(42));
        var b = reg.Roll("broken_engine", new GameRandom(42));
        Assert.Equal(a, b);
        Assert.NotNull(a);
    }

    [Fact]
    public void Roll_unknown_table_returns_null()
    {
        var reg = new DungeonLootTableRegistry();
        Assert.Null(reg.Roll("nope", new GameRandom(1)));
    }

    [Fact]
    public void Get_returns_loaded_table_with_entries()
    {
        var reg = new DungeonLootTableRegistry();
        reg.LoadFromJson("broken_engine", Json);
        var table = reg.Get("broken_engine");
        Assert.NotNull(table);
        Assert.Equal(2, table!.Entries.Length);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/engine --configuration Release -nodeReuse:false --filter "FullyQualifiedName~DungeonLootTableRegistryTests"`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Implement the table + registry**

Create `src/engine/RPC.Engine/Dungeon/DungeonLootTable.cs`:

```csharp
using System.Text.Json;
using RPC.Engine.Content;

namespace RPC.Engine.Dungeons;

public record DungeonLootEntry(string ItemId, int Weight);

public record DungeonLootTableDef(string Id, DungeonLootEntry[] Entries);

public class DungeonLootTableRegistry
{
    private readonly Dictionary<string, DungeonLootTableDef> _tables = new();

    public void LoadFromJson(string id, string json)
    {
        var def = JsonSerializer.Deserialize<DungeonLootTableDef>(json, ContentJsonOptions.CaseInsensitive);
        if (def is not null)
            _tables[id] = def;
    }

    public DungeonLootTableDef? Get(string id)
        => _tables.TryGetValue(id, out var def) ? def : null;

    /// <summary>Roll one item id from the table, or null if the table is missing/empty.</summary>
    public string? Roll(string id, GameRandom rng)
    {
        var table = Get(id);
        if (table is null || table.Entries.Length == 0) return null;

        var total = table.Entries.Sum(e => e.Weight);
        if (total <= 0) return null;
        var roll = rng.Roll(1, total);
        var cumulative = 0;
        foreach (var entry in table.Entries)
        {
            cumulative += entry.Weight;
            if (roll <= cumulative) return entry.ItemId;
        }
        return table.Entries[^1].ItemId;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/engine --configuration Release -nodeReuse:false --filter "FullyQualifiedName~DungeonLootTableRegistryTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/engine/RPC.Engine/Dungeon/DungeonLootTable.cs src/engine/RPC.Tests/DungeonLootTableRegistryTests.cs
git commit -m "feat(dungeon): add dungeon loot table + registry"
```

---

## Task 4: `DungeonLootPlacer` — role-biased deterministic placement

**Files:**
- Create: `src/engine/RPC.Engine/Dungeon/DungeonLootPlacer.cs`
- Test: `src/engine/RPC.Tests/DungeonLootPlacerTests.cs`

**Background:** Bias from spec §4: DeadEnd 60%, SideBranch 30%, Critical 20%, Entrance/Boss 0%. One seeded `GameRandom`. Eligible tile per room = lowest `(y, x)` `Floor` tile (no RNG for the pick). `GameRandom.NextDouble()` exists (used elsewhere); if not, use `rng.Roll(1, 100) <= pct`. Use the `Roll(1,100)` form to stay integer-deterministic.

- [ ] **Step 1: Write the failing test**

Create `src/engine/RPC.Tests/DungeonLootPlacerTests.cs`:

```csharp
using RPC.Engine;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using Xunit;

namespace RPC.Tests;

public class DungeonLootPlacerTests
{
    private static Dungeon BuildRooms()
    {
        // room0 entrance, room1 critical, room2 boss, room3 dead-end (4 separate floor tiles).
        var d = new Dungeon(4, 1, "t");
        d.Tiles[0, 0] = new Tile(TileType.StairsUp, East: BorderType.Door, RoomId: 0);
        d.Tiles[1, 0] = new Tile(TileType.Floor, West: BorderType.Door, East: BorderType.Door, RoomId: 1);
        d.Tiles[2, 0] = new Tile(TileType.Floor, West: BorderType.Door, East: BorderType.Door, RoomId: 2, EncounterId: "boss-1");
        d.Tiles[3, 0] = new Tile(TileType.Floor, West: BorderType.Door, RoomId: 3);
        for (int i = 0; i < 4; i++) d.Rooms.Add(new RoomInfo { Id = i, Min = new Position(i, 0), Max = new Position(i, 0) });
        return d;
    }

    private static DungeonLootTableRegistry Reg()
    {
        var r = new DungeonLootTableRegistry();
        r.LoadFromJson("t", """{ "id":"t", "entries":[ {"itemId":"gold","weight":1} ] }""");
        return r;
    }

    [Fact]
    public void Place_is_deterministic_for_same_seed()
    {
        var roles = new DungeonPathClassifier().Classify(BuildRooms());
        var d1 = BuildRooms(); new DungeonLootPlacer().Place(d1, roles, Reg().Get("t")!, 7);
        var d2 = BuildRooms(); new DungeonLootPlacer().Place(d2, roles, Reg().Get("t")!, 7);

        for (int x = 0; x < 4; x++)
            Assert.Equal(d1.Tiles[x, 0].LootId, d2.Tiles[x, 0].LootId);
    }

    [Fact]
    public void Place_never_puts_loot_on_entrance_or_boss()
    {
        var roles = new DungeonPathClassifier().Classify(BuildRooms());
        // try many seeds; entrance (x0) and boss (x2) must always stay null
        for (int seed = 0; seed < 50; seed++)
        {
            var d = BuildRooms();
            new DungeonLootPlacer().Place(d, roles, Reg().Get("t")!, seed);
            Assert.Null(d.Tiles[0, 0].LootId);
            Assert.Null(d.Tiles[2, 0].LootId);
        }
    }

    [Fact]
    public void DeadEnds_receive_loot_more_often_than_critical_rooms()
    {
        var roles = new DungeonPathClassifier().Classify(BuildRooms());
        int deadEndHits = 0, criticalHits = 0;
        for (int seed = 0; seed < 200; seed++)
        {
            var d = BuildRooms();
            new DungeonLootPlacer().Place(d, roles, Reg().Get("t")!, seed);
            if (d.Tiles[3, 0].LootId != null) deadEndHits++;   // dead-end
            if (d.Tiles[1, 0].LootId != null) criticalHits++;  // critical
        }
        Assert.True(deadEndHits > criticalHits,
            $"dead-end {deadEndHits} should exceed critical {criticalHits}");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/engine --configuration Release -nodeReuse:false --filter "FullyQualifiedName~DungeonLootPlacerTests"`
Expected: FAIL — `DungeonLootPlacer` does not exist.

- [ ] **Step 3: Implement `DungeonLootPlacer`**

Create `src/engine/RPC.Engine/Dungeon/DungeonLootPlacer.cs`:

```csharp
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/engine --configuration Release -nodeReuse:false --filter "FullyQualifiedName~DungeonLootPlacerTests"`
Expected: PASS (3 tests).

> If `GameRandom.Roll(int,int)` is inclusive on both ends (it is — see `EncounterTable.RollEncounter` using `Roll(1, totalWeight)`), the weighted pick above is correct.

- [ ] **Step 5: Commit**

```bash
git add src/engine/RPC.Engine/Dungeon/DungeonLootPlacer.cs src/engine/RPC.Tests/DungeonLootPlacerTests.cs
git commit -m "feat(dungeon): role-biased deterministic loot placement"
```

---

## Task 5: Wire classifier + placer into `DungeonGenerator`

**Files:**
- Modify: `src/engine/RPC.Engine/Dungeon/DungeonGenerator.cs`
- Modify: `src/engine/RPC.Engine/Dungeon/DungeonTemplate.cs` (add `LootTableId`)
- Test: `src/engine/RPC.Tests/DungeonGeneratorLootTests.cs`

**Background:** `DungeonGenerator` ctor (`DungeonGenerator.cs:12-17`) takes segments, templates, encounter tables. Add an optional `DungeonLootTableRegistry`. Decorate AFTER `TagBossTile` in both `BuildFromTemplate` and `BuildProcedural`. `DungeonTemplate` is a record — find it and add `string? LootTableId = null`.

- [ ] **Step 1: Add `LootTableId` to `DungeonTemplate`**

Open `src/engine/RPC.Engine/Dungeon/DungeonTemplate.cs`. Add `LootTableId` as an optional property/parameter (match the existing record style). Example if it's a record:

```csharp
public record DungeonTemplate(
    string Id,
    string Name,
    string[] SegmentPool,
    string[] SegmentPriority,
    int TargetRooms,
    string? BossEncounterId = null,
    string? EncounterTableId = null,
    string? WanderingTableId = null,
    string? LootTableId = null);
```

(If property names differ, add only `string? LootTableId = null` as a new optional member — do not reorder existing required members.)

- [ ] **Step 2: Write the failing test**

Create `src/engine/RPC.Tests/DungeonGeneratorLootTests.cs`:

```csharp
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using Xunit;

namespace RPC.Tests;

public class DungeonGeneratorLootTests
{
    private static (DungeonGenerator gen, DungeonLootTableRegistry loot) MakeGen()
    {
        var segments = TestSegments.LoadAll(); // see note below
        var loot = new DungeonLootTableRegistry();
        loot.LoadFromJson("broken_engine", """{ "id":"broken_engine", "entries":[ {"itemId":"scrap_plating","weight":1} ] }""");
        var gen = new DungeonGenerator(segments, dungeonTemplates: null, encounterTables: null, lootTables: loot);
        return (gen, loot);
    }

    [Fact]
    public void Generated_dungeon_places_at_least_one_loot_when_table_available()
    {
        var (gen, _) = MakeGen();
        var d = gen.Generate("broken_engine", seed: 123);
        int lootCount = CountLoot(d);
        Assert.True(lootCount >= 1, "expected at least one loot tile");
    }

    [Fact]
    public void Loot_layout_is_deterministic_for_same_seed()
    {
        var (gen, _) = MakeGen();
        var a = gen.Generate("broken_engine", seed: 99);
        var b = gen.Generate("broken_engine", seed: 99);
        Assert.Equal(LootSignature(a), LootSignature(b));
    }

    private static int CountLoot(Dungeon d)
    {
        int n = 0;
        for (int x = 0; x < d.Width; x++)
            for (int y = 0; y < d.Height; y++)
                if (d.Tiles[x, y].LootId != null) n++;
        return n;
    }

    private static string LootSignature(Dungeon d)
    {
        var parts = new List<string>();
        for (int x = 0; x < d.Width; x++)
            for (int y = 0; y < d.Height; y++)
                if (d.Tiles[x, y].LootId is { } id) parts.Add($"{x},{y}={id}");
        return string.Join("|", parts);
    }
}
```

> **Note on `TestSegments.LoadAll()`:** the existing fallback tests (`DungeonGeneratorFallbackTests.cs`) already load segments from disk. Reuse their exact loading approach — open that file and copy the segment-loading helper (e.g. `SegmentLoader.LoadFromDirectory("../../../../../../content/segments")`). If they use an inline helper, replicate it here rather than inventing `TestSegments`.

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test src/engine --configuration Release -nodeReuse:false --filter "FullyQualifiedName~DungeonGeneratorLootTests"`
Expected: FAIL — `DungeonGenerator` has no `lootTables` parameter.

- [ ] **Step 4: Add loot registry to ctor + decorate in both build paths**

In `src/engine/RPC.Engine/Dungeon/DungeonGenerator.cs`:

Add field + ctor param:

```csharp
    private readonly EncounterTableRegistry? _encounterTables;
    private readonly DungeonLootTableRegistry? _lootTables;

    public DungeonGenerator(List<RoomSegment> segments, Dictionary<string, DungeonTemplate>? dungeonTemplates = null, EncounterTableRegistry? encounterTables = null, DungeonLootTableRegistry? lootTables = null)
    {
        _segments = segments;
        _dungeonTemplates = dungeonTemplates ?? new Dictionary<string, DungeonTemplate>();
        _encounterTables = encounterTables;
        _lootTables = lootTables;
    }
```

Add a private decorate helper:

```csharp
    /// <summary>Classify rooms and place loot. No-op when no loot table resolves.</summary>
    private void Decorate(Dungeon dungeon, string dungeonType, DungeonTemplate? template, int effectiveSeed)
    {
        if (_lootTables is null) return;
        var lootTableId = template?.LootTableId ?? dungeonType;
        var table = _lootTables.Get(lootTableId) ?? _lootTables.Get("default");
        if (table is null) return;

        var roles = new DungeonPathClassifier().Classify(dungeon);
        new DungeonLootPlacer().Place(dungeon, roles, table, effectiveSeed);
    }
```

In `BuildFromTemplate`, after `TagBossTile(dungeon, template.BossEncounterId);` add:

```csharp
        Decorate(dungeon, template.Id, template, effectiveSeed);
        return dungeon;
```

In `BuildProcedural`, after `TagBossTile(dungeon, bossEncounterId);` (before the connectivity assert) add:

```csharp
        Decorate(dungeon, dungeonType, template, effectiveSeed);
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test src/engine --configuration Release -nodeReuse:false --filter "FullyQualifiedName~DungeonGeneratorLootTests"`
Expected: PASS (2 tests).

- [ ] **Step 6: Run the full dungeon test set to check no regressions**

Run: `dotnet test src/engine --configuration Release -nodeReuse:false --filter "FullyQualifiedName~Dungeon"`
Expected: PASS (all dungeon tests including the prior fallback/pacer/classifier/placer suites).

- [ ] **Step 7: Commit**

```bash
git add src/engine/RPC.Engine/Dungeon/DungeonGenerator.cs src/engine/RPC.Engine/Dungeon/DungeonTemplate.cs src/engine/RPC.Tests/DungeonGeneratorLootTests.cs
git commit -m "feat(dungeon): decorate generated dungeons with loot"
```

---

## Task 6: Loot content + content-pack validation

**Files:**
- Create: `content/loot/broken_engine.json`
- Create: `content/loot/default.json`
- Modify: `tools/content-pack/Program.cs`
- Test: manual content-pack run (validator has no xUnit suite; it returns exit codes)

**Background:** content-pack pre-pass collects `itemIds` (`Program.cs:44,76`). Dispatch by path (`Program.cs:92-150`). Add a `/loot/` branch that validates every `itemId` resolves in `itemIds`. Mirror `ValidateEncounter`'s structure.

- [ ] **Step 1: Author the loot tables**

Create `content/loot/broken_engine.json` (use item ids that exist — confirm against `content/items/*.json`; `scrap_plating` / `engine_tech_manual` are examples, replace with real ids):

```json
{
  "id": "broken_engine",
  "entries": [
    { "itemId": "rat_tail", "weight": 4 },
    { "itemId": "bone_fragment", "weight": 2 }
  ]
}
```

Create `content/loot/default.json` (generic fallback for procedural/unknown dungeon types):

```json
{
  "id": "default",
  "entries": [
    { "itemId": "rat_tail", "weight": 1 }
  ]
}
```

> Before finalizing, grep real item ids: `grep -rh '"id"' content/items/*.json | head -40` and pick a handful of thematically-plausible component/consumable ids. Every `itemId` MUST exist or content-pack will fail (by design).

- [ ] **Step 2: Add the validation branch + function**

In `tools/content-pack/Program.cs`, add to the dispatch chain (after the `/items/` branch, near line 129):

```csharp
            else if (relativePath.Contains("/loot/") || relativePath.StartsWith("loot/"))
            {
                result = ValidateLootTable(file, json, itemIds);
            }
```

Add the validator method (near `ValidateEncounter`):

```csharp
    static int ValidateLootTable(string filePath, string json, HashSet<string> itemIds)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        try
        {
            var def = JsonSerializer.Deserialize<DungeonLootTableDef>(json, options);
            if (def == null || string.IsNullOrWhiteSpace(def.Id))
            {
                Console.WriteLine($"FAIL: {filePath} - Missing id");
                return 1;
            }
            if (def.Entries == null || def.Entries.Length == 0)
            {
                Console.WriteLine($"FAIL: {filePath} - Loot table has no entries");
                return 1;
            }
            foreach (var e in def.Entries)
            {
                if (string.IsNullOrWhiteSpace(e.ItemId) || !itemIds.Contains(e.ItemId))
                {
                    Console.WriteLine($"FAIL: {filePath} - Unknown itemId '{e.ItemId}'");
                    return 1;
                }
                if (e.Weight <= 0)
                {
                    Console.WriteLine($"FAIL: {filePath} - itemId '{e.ItemId}' weight must be positive");
                    return 1;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {filePath} - {ex.Message}");
            return 1;
        }
        return 0;
    }
```

> `DungeonLootTableDef` lives in `RPC.Engine.Dungeons`. Confirm `tools/content-pack` references `RPC.Engine` (it deserializes `ClassDef`, `ItemDef`, etc., so it does) and add `using RPC.Engine.Dungeons;` at the top of `Program.cs`.

- [ ] **Step 3: Run the validator — expect PASS**

Run: `dotnet run --project tools/content-pack -nodeReuse:false -- content /tmp/cp-out`
Expected: exit 0; no `FAIL: content/loot/...` lines. (Pre-existing boss-encounter WARN lines are unrelated.)

- [ ] **Step 4: Verify the failure path**

Temporarily edit `content/loot/default.json` to reference `"itemId": "does_not_exist"`, re-run the validator, confirm it prints `FAIL: content/loot/default.json - Unknown itemId 'does_not_exist'` and exits non-zero. Then revert the edit.

- [ ] **Step 5: Commit**

```bash
git add content/loot tools/content-pack/Program.cs
git commit -m "feat(content): dungeon loot tables + content-pack validation"
```

---

## Task 7: Load loot tables in the host + pass to generator

**Files:**
- Modify: `src/engine/RPC.Host/Web/GameServer.cs`

**Background:** `GameServer` builds registries from the content catalog (`LoadEncounterTables` at `GameServer.cs:153-161`) and constructs `DungeonGenerator`. Add a `LoadLootTables` mirroring `LoadEncounterTables`, then pass it into the generator ctor.

- [ ] **Step 1: Add the loader method**

In `src/engine/RPC.Host/Web/GameServer.cs`, mirror `LoadEncounterTables`:

```csharp
    private static DungeonLootTableRegistry LoadLootTables(IContentCatalog catalog)
    {
        var registry = new DungeonLootTableRegistry();
        foreach (var file in catalog.EnumerateFiles("loot", "*.json"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var json = catalog.GetString(file) ?? catalog.GetString($"loot/{Path.GetFileName(file)}");
            if (json != null)
                registry.LoadFromJson(id, json);
        }
        return registry;
    }
```

Add `using RPC.Engine.Dungeons;` if not already present.

- [ ] **Step 2: Construct + pass the registry**

Find where `_dungeonGenerator` / the `DungeonGenerator` is constructed (search `new DungeonGenerator` in `GameServer.cs`). Add a field/local `var lootTables = LoadLootTables(_catalog);` near the other registry loads (~line 57), and pass it as the new `lootTables:` argument to the `DungeonGenerator` constructor.

```csharp
        var lootTables = LoadLootTables(_catalog);
        // ... wherever DungeonGenerator is built:
        _dungeonGenerator = new DungeonGenerator(_segments, _dungeonTemplates, _encounterTables, lootTables);
```

- [ ] **Step 3: Build the host**

Run: `dotnet build src/engine --configuration Release -nodeReuse:false`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/engine/RPC.Host/Web/GameServer.cs
git commit -m "feat(host): load dungeon loot tables and wire into generator"
```

---

## Task 8: Runtime collected-loot state + save persistence

**Files:**
- Modify: `src/engine/RPC.Engine/Exploration/ExplorationState.cs`
- Modify: `src/engine/RPC.Engine/Save/SaveData.cs`, `SaveSystem.cs`, `SaveRestorer.cs`
- Test: `src/engine/RPC.Tests/CollectedLootSaveTests.cs`

**Background:** `ExplorationState` (`ExplorationState.cs`) holds per-dungeon runtime state and a `Reset()`. Add a collected-loot set, reset it with the rest. Save round-trip mirrors how `ExpeditionCache` persists (`SaveData.cs:29`, `SaveSystem.cs:249`, `SaveRestorer.cs:234`).

- [ ] **Step 1: Add `CollectedLoot` to `ExplorationState`**

In `src/engine/RPC.Engine/Exploration/ExplorationState.cs`:

```csharp
    public HashSet<string> CollectedLoot { get; private set; } = new();
```

In `Reset()`, add:

```csharp
        CollectedLoot.Clear();
```

> Key format: `"x,y"` (same as `ExploredTiles` keys) so it serializes as a simple string list.

- [ ] **Step 2: Write the failing save round-trip test**

Create `src/engine/RPC.Tests/CollectedLootSaveTests.cs`. Model it on an existing save round-trip test (search `src/engine/RPC.Tests` for a test that saves + restores `ExpeditionCache` or `ExploredTiles` and copy its harness). The assertion:

```csharp
using Xunit;
// + usings matching the existing save round-trip test harness

namespace RPC.Tests;

public class CollectedLootSaveTests
{
    [Fact]
    public void CollectedLoot_survives_save_and_restore()
    {
        var state = /* build a GameState via the same helper the other save tests use */;
        state.Exploration.CollectedLoot.Add("5,7");

        var data = SaveSystem.ToSaveData(state);          // use the real method name from SaveSystem
        var restored = /* new GameState */;
        SaveRestorer.Restore(restored, data);             // use the real method name from SaveRestorer

        Assert.Contains("5,7", restored.Exploration.CollectedLoot);
    }
}
```

> Open `SaveSystem.cs` / `SaveRestorer.cs` to get the exact entry-point method names and the existing test harness; match them. Do NOT invent method names.

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test src/engine --configuration Release -nodeReuse:false --filter "FullyQualifiedName~CollectedLootSaveTests"`
Expected: FAIL — `CollectedLoot` is empty after restore (not yet persisted).

- [ ] **Step 4: Persist `CollectedLoot`**

In `src/engine/RPC.Engine/Save/SaveData.cs`, add a field near `ExpeditionCache`:

```csharp
    public string[] CollectedLoot { get; set; } = Array.Empty<string>();
```

In `SaveSystem.cs` where the save object is built (near line 249), add:

```csharp
            CollectedLoot = state.Exploration.CollectedLoot.ToArray(),
```

In `SaveRestorer.cs` (near line 234), restore:

```csharp
        state.Exploration.CollectedLoot.Clear();
        foreach (var key in data.CollectedLoot ?? Array.Empty<string>())
            state.Exploration.CollectedLoot.Add(key);
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test src/engine --configuration Release -nodeReuse:false --filter "FullyQualifiedName~CollectedLootSaveTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/engine/RPC.Engine/Exploration/ExplorationState.cs src/engine/RPC.Engine/Save/ src/engine/RPC.Tests/CollectedLootSaveTests.cs
git commit -m "feat(exploration): persist collected-loot set"
```

---

## Task 9: `PickupCommand` — grant tile loot to expedition cache

**Files:**
- Modify: `src/engine/RPC.Engine/Commands/CommandTypes.cs`
- Modify: `src/engine/RPC.Engine/Commands/CommandDispatcher.cs`
- Modify: `src/engine/RPC.Engine/Commands/GameCommandHandler.cs`
- Modify: `src/engine/RPC.Engine/Inventory/ComponentInventorySystem.cs` (add `AddToExpeditionCache`)
- Modify: `src/engine/RPC.Engine/GameState.cs` (add `TryPickupLoot`)
- Test: `src/engine/RPC.Tests/PickupLootTests.cs`

**Background:** Commands are records (`CommandTypes.cs`), parsed by string (`CommandDispatcher.cs`), handled in a switch (`GameCommandHandler.cs:29`). Cache add: `ComponentInventorySystem.AddComponent(party.ExpeditionCache, itemId, count, PartyState.MaxExpeditionCacheSlots)` returns a new array; assign back to `party.ExpeditionCache`.

- [ ] **Step 1: Write the failing test**

Create `src/engine/RPC.Tests/PickupLootTests.cs`:

```csharp
using RPC.Engine;
using RPC.Engine.Models.Dungeons;
using Xunit;

namespace RPC.Tests;

public class PickupLootTests
{
    private static GameState StateOnLootTile(string itemId)
    {
        var gs = new GameState(seed: 1); // match the ctor other tests use
        var d = new Dungeon(3, 3, "t");
        d.Tiles[1, 1] = new Tile(TileType.Floor, RoomId: 0, LootId: itemId);
        gs.CurrentDungeon = d;
        gs.CurrentDungeonType = "t";
        gs.Player.Position = new Position(1, 1); // place player on the loot tile
        return gs;
    }

    [Fact]
    public void Pickup_adds_item_to_cache_and_marks_collected()
    {
        var gs = StateOnLootTile("rat_tail");
        bool changed = gs.TryPickupLoot();

        Assert.True(changed);
        Assert.Contains(gs.Party.ExpeditionCache, s => s.ItemId == "rat_tail" && s.Count >= 1);
        Assert.Contains("1,1", gs.Exploration.CollectedLoot);
    }

    [Fact]
    public void Second_pickup_on_same_tile_is_noop()
    {
        var gs = StateOnLootTile("rat_tail");
        Assert.True(gs.TryPickupLoot());
        Assert.False(gs.TryPickupLoot()); // already collected
    }

    [Fact]
    public void Pickup_on_empty_tile_is_noop()
    {
        var gs = new GameState(seed: 1);
        var d = new Dungeon(3, 3, "t");
        d.Tiles[1, 1] = new Tile(TileType.Floor, RoomId: 0);
        gs.CurrentDungeon = d;
        gs.Player.Position = new Position(1, 1);
        Assert.False(gs.TryPickupLoot());
    }
}
```

> Confirm the `GameState` ctor signature + how `Player.Position` is set by reading an existing exploration/movement test. Match it exactly. If `Player.Position` is read-only, set position via the same path those tests use.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/engine --configuration Release -nodeReuse:false --filter "FullyQualifiedName~PickupLootTests"`
Expected: FAIL — `TryPickupLoot` does not exist.

- [ ] **Step 3: Add `AddToExpeditionCache` helper**

In `src/engine/RPC.Engine/Inventory/ComponentInventorySystem.cs`, add:

```csharp
    /// <summary>Add an item directly to the expedition cache (e.g. dungeon loot). Returns the party.</summary>
    public static PartyState AddToExpeditionCache(PartyState party, string itemId, int count)
    {
        party.ExpeditionCache = AddComponent(
            party.ExpeditionCache, itemId, count, PartyState.MaxExpeditionCacheSlots);
        return party;
    }
```

- [ ] **Step 4: Add `TryPickupLoot` to `GameState`**

In `src/engine/RPC.Engine/GameState.cs`, add a method (near the other exploration-mutating methods; `Exploration` and `Party` are accessible there — see the `EnterDungeon`/move methods):

```csharp
    /// <summary>
    /// Pick up loot on the player's current tile into the expedition cache. No-op (returns false)
    /// if there is no loot, it was already collected, or the cache is full.
    /// </summary>
    public bool TryPickupLoot()
    {
        if (CurrentDungeon is null) return false;
        var pos = Player.Position;
        if (!CurrentDungeon.IsValidPosition(pos)) return false;

        var tile = CurrentDungeon.Tiles[pos.X, pos.Y];
        if (tile.LootId is not { } itemId) return false;

        var key = $"{pos.X},{pos.Y}";
        if (Exploration.CollectedLoot.Contains(key)) return false;

        if (!ComponentInventorySystem.CanAddComponent(
                Party.ExpeditionCache, itemId, 1, PartyState.MaxExpeditionCacheSlots))
            return false; // cache full — leave loot on the floor

        ComponentInventorySystem.AddToExpeditionCache(Party, itemId, 1);
        Exploration.CollectedLoot.Add(key);

        EmitActionLog("dungeon", "loot_collected", new Dictionary<string, string>
        {
            { "itemId", itemId },
            { "x", pos.X.ToString() },
            { "y", pos.Y.ToString() }
        });
        return true;
    }
```

> Confirm `EmitActionLog` signature by grepping its other call sites in `GameState.cs` (used earlier in this codebase as `EmitActionLog("branch", "branch_chosen", dict)`). Match it. Add `using RPC.Engine.Inventory;` / `using RPC.Engine.Party;` if needed.

- [ ] **Step 5: Add the command + dispatch + handler**

In `CommandTypes.cs` under the "Dungeon & Exploration" section:

```csharp
public record PickupLootCommand : ICommand;
```

In `CommandDispatcher.cs`, add to the switch (near the movement cases):

```csharp
            "pickup_loot" => new PickupLootCommand(),
```

In `GameCommandHandler.cs`, add a case (near `MoveForwardCommand`):

```csharp
            case PickupLootCommand:
                stateChanged = _gameState.TryPickupLoot();
                break;
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test src/engine --configuration Release -nodeReuse:false --filter "FullyQualifiedName~PickupLootTests"`
Expected: PASS (3 tests).

- [ ] **Step 7: Commit**

```bash
git add src/engine/RPC.Engine/Commands/ src/engine/RPC.Engine/Inventory/ComponentInventorySystem.cs src/engine/RPC.Engine/GameState.cs src/engine/RPC.Tests/PickupLootTests.cs
git commit -m "feat(dungeon): pickup loot into expedition cache"
```

---

## Task 10: Serialize loot to the client

**Files:**
- Modify: `src/engine/RPC.Host/Web/Presenters/ExplorationPresenter.cs`
- Modify: `src/engine/RPC.Host/Web/Presenters/PartyPresenter.cs` or wherever item display names resolve (read-only lookup)
- Test: `src/engine/RPC.Tests/ExplorationPresenterLootTests.cs`

**Background:** `ExplorationPresenter.SerializeTile` (`ExplorationPresenter.cs:61-62`) currently emits type + borders. Add `hasLoot` + `lootName`. `hasLoot` is true only if `LootId != null` AND `"x,y"` not in `state.Exploration.CollectedLoot`. `lootName` resolves via the item registry — `SerializeTile` is static and has no registry; pass the collected-set and an item-name lookup into `Present`/`SerializeTile`.

- [ ] **Step 1: Write the failing test**

Create `src/engine/RPC.Tests/ExplorationPresenterLootTests.cs`. Build a `GameState` with a dungeon, one loot tile, player nearby, then call `ExplorationPresenter.Present(state)` and inspect the serialized tile. Because tiles are anonymous objects, assert via reflection or `System.Text.Json` round-trip:

```csharp
using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Models.Dungeons;
using RPC.Host.Web.Presenters;
using Xunit;

namespace RPC.Tests;

public class ExplorationPresenterLootTests
{
    [Fact]
    public void Loot_tile_serializes_hasLoot_true_until_collected()
    {
        var gs = new GameState(seed: 1);
        var d = new Dungeon(5, 5, "t");
        d.Tiles[2, 2] = new Tile(TileType.Floor, RoomId: 0, LootId: "rat_tail");
        gs.CurrentDungeon = d;
        gs.Player.Position = new Position(2, 2);

        var json = JsonSerializer.Serialize(ExplorationPresenter.Present(gs));
        Assert.Contains("\"hasLoot\":true", json);

        gs.Exploration.CollectedLoot.Add("2,2");
        var json2 = JsonSerializer.Serialize(ExplorationPresenter.Present(gs));
        Assert.Contains("\"hasLoot\":false", json2);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/engine --configuration Release -nodeReuse:false --filter "FullyQualifiedName~ExplorationPresenterLootTests"`
Expected: FAIL — no `hasLoot` field yet.

- [ ] **Step 3: Emit `hasLoot` / `lootName`**

In `ExplorationPresenter.cs`, thread the collected-set + an item-name resolver through. Minimal version (name resolution optional — can be the item id if no registry is wired here):

```csharp
    public static ExplorationViewModel Present(GameState state)
    {
        var collected = state.Exploration.CollectedLoot;
        // ... existing loop, replace SerializeTile(x, y, tile) calls with:
        tiles.Add(SerializeTile(x, y, tile, collected));
        // and in the explored loop likewise:
        explored.Add(SerializeTile(x, y, tile, collected));
        // ...
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
```

> `lootName` = item id for now (the HUD can show the id or a prettified form). A registry-backed display name is a follow-up; keep this task scoped.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/engine --configuration Release -nodeReuse:false --filter "FullyQualifiedName~ExplorationPresenterLootTests"`
Expected: PASS.

- [ ] **Step 5: Run the full engine suite**

Run: `dotnet test src/engine --configuration Release -nodeReuse:false`
Expected: PASS — previous total (990) plus all new tests, 0 failures.

- [ ] **Step 6: Commit**

```bash
git add src/engine/RPC.Host/Web/Presenters/ExplorationPresenter.cs src/engine/RPC.Tests/ExplorationPresenterLootTests.cs
git commit -m "feat(host): serialize tile loot to client"
```

---

## Task 11: Client — render loot marker, "Take" prompt, pickup action

**Files:**
- Modify: `src/client/src/shared/types/game.ts:14-22` (Tile interface)
- Modify: `src/client/src/renderer/DungeonRenderer.ts` (loot marker mesh)
- Modify: `src/client/src/features/exploration/ExplorationHUD.svelte` (Take prompt)
- Modify: `src/client/src/app/App.svelte` (wire pickup handler → `sendAction({ type: 'pickup_loot' })`)

**Background:** `renderTiles` (`DungeonRenderer.ts:781-854`) builds floor/border meshes keyed in `this.tileMeshes`. Add loot markers with key `loot:x,y`, cleaned up like borders when no longer visible. `ExplorationHUD` already takes `gameState` + action callbacks; add a `onPickup` prop and a prompt shown when the player's tile `hasLoot`.

- [ ] **Step 1: Add loot fields to the client `Tile` type**

In `src/client/src/shared/types/game.ts` (lines 14-22):

```typescript
export interface Tile {
  x: number;
  y: number;
  type: 'Empty' | 'Floor' | 'StairsUp' | 'StairsDown' | 'IllusoryFloor';
  north: BorderType;
  south: BorderType;
  east: BorderType;
  west: BorderType;
  hasLoot?: boolean;
  lootName?: string | null;
}
```

- [ ] **Step 2: Render loot marker meshes**

In `src/client/src/renderer/DungeonRenderer.ts` `renderTiles`:

In the visible-keys pass (near line 785), add:

```typescript
      if (tile.hasLoot) visibleKeys.add(`loot:${tile.x},${tile.y}`);
```

In the cleanup loop (line 793-801) the existing `!visibleKeys.has(key)` check already removes stale `loot:` meshes — no change needed there as long as the key is included above.

In the add/update loop (after the border blocks, before the closing brace of the `for (const tile of tiles)` loop, ~line 852), add:

```typescript
      // Loot glint marker
      const lootKey = `loot:${tile.x},${tile.y}`;
      if (tile.hasLoot && !this.tileMeshes.has(lootKey)) {
        const geo = new THREE.OctahedronGeometry(this.tileSize * 0.18);
        const mat = new THREE.MeshStandardMaterial({
          color: 0xffcc44,
          emissive: 0xaa7711,
          emissiveIntensity: 0.8,
          metalness: 0.6,
          roughness: 0.3,
        });
        const marker = new THREE.Mesh(geo, mat);
        marker.position.set(tile.x * this.tileSize, this.tileSize * 0.35, tile.y * this.tileSize);
        this.tileMeshes.set(lootKey, marker);
        this.scene.add(marker);
      } else if (!tile.hasLoot && this.tileMeshes.has(lootKey)) {
        const m = this.tileMeshes.get(lootKey)!;
        this.scene.remove(m);
        m.geometry.dispose();
        (m.material as THREE.Material).dispose();
        this.tileMeshes.delete(lootKey);
      }
```

> Confirm `this.tileSize` and `THREE` import exist in this file (they do — used by `createBaseMesh`). Match the existing axis convention: floor meshes use `x * tileSize` for X and `y * tileSize` for Z (see lines 807-808).

- [ ] **Step 3: Add the "Take" prompt to `ExplorationHUD`**

In `src/client/src/features/exploration/ExplorationHUD.svelte`, add an `onPickup` prop and derive whether the player stands on a loot tile. The HUD already receives `gameState`. Add to the `Props` interface:

```typescript
    onPickup: () => void;
```

Destructure `onPickup` in the `$props()` call. Add a derived check + prompt markup (place it in the `hud-center` block, after the position div):

```svelte
  {#if currentTileHasLoot}
    <button class="hud-btn take-btn" onclick={onPickup}>
      Take {currentLootName}
    </button>
  {/if}
```

In the `<script>`, derive the values from `gameState` tiles at the player's position:

```typescript
  const currentTile = $derived(
    gameState?.tiles?.find(
      t => t.x === gameState?.player?.x && t.y === gameState?.player?.y
    )
  );
  const currentTileHasLoot = $derived(!!currentTile?.hasLoot);
  const currentLootName = $derived(currentTile?.lootName ?? 'item');
```

> Confirm the gameState field that holds the tile list as seen by the client (it is `tiles` per `game.ts:270`) and the player field (`player.x` / `player.y`). Adjust the access path to match the actual `GameState` client type.

Add a `.take-btn` style mirroring the existing `.hud-btn` accent rules (e.g. `border-color: #d4a84b; color: #ffcc44;`).

- [ ] **Step 4: Wire the pickup handler in `App.svelte`**

In `src/client/src/app/App.svelte`, find where `ExplorationHUD` is rendered (~line 611) and add the `onPickup` prop:

```svelte
        <ExplorationHUD
          gameState={gameState}
          onMoveForward={handleMoveForward}
          onTurnLeft={handleTurnLeft}
          onTurnRight={handleTurnRight}
          onReturnToTown={handleReturnToTown}
          onRest={handleRest}
          onSave={handleSave}
          onPickup={handlePickup}
        />
```

Add the handler near the other `handle*` exploration handlers:

```typescript
  function handlePickup() {
    sendAction({ type: 'pickup_loot' });
  }
```

> Match the existing `sendAction` import/usage pattern used by `handleMoveForward` etc.

- [ ] **Step 5: Typecheck the client**

Run: `cd src/client && npm run check`
Expected: `0 ERRORS`.

- [ ] **Step 6: Commit**

```bash
git add src/client/src/shared/types/game.ts src/client/src/renderer/DungeonRenderer.ts src/client/src/features/exploration/ExplorationHUD.svelte src/client/src/app/App.svelte
git commit -m "feat(client): render dungeon loot + take prompt"
```

---

## Task 12: Full verification + manual playtest

**Files:** none (verification only)

- [ ] **Step 1: Full engine test suite**

Run: `dotnet test src/engine --configuration Release -nodeReuse:false`
Expected: 0 failures; count ≥ 990 + new tests.

- [ ] **Step 2: Content validation**

Run: `dotnet run --project tools/content-pack -nodeReuse:false -- content /tmp/cp-out`
Expected: exit 0; no `FAIL` lines for `content/loot/`.

- [ ] **Step 3: Client typecheck**

Run: `cd src/client && npm run check`
Expected: 0 errors.

- [ ] **Step 4: Restart the running backend + reload, manual playtest**

Use the agnt proc tools (not raw kill): `proc restart rpg-981d:backend`, wait for rebuild, reload the page. Enter a dungeon, confirm:
- A glint marker appears on loot tiles (visible down corridors).
- Walking onto a loot tile shows the "Take" prompt.
- Clicking Take removes the marker + the item lands in the expedition cache (check character sheet cache).
- Re-entering the tile shows no prompt (collected).

- [ ] **Step 5: Kill any orphan build processes**

Run: `ps -eo pid,pgid,cmd | grep -iE "MSBuild|VBCSCompiler" | grep -v grep` and stop orphans whose leader is dead.

- [ ] **Step 6: Final commit (if any verification fixups were needed)**

```bash
git add -A
git commit -m "test(dungeon): verify decoration loot slice end-to-end"
```

---

## Self-Review Notes

- **Spec coverage:** §3 critical path → Task 2; §4 loot placement → Task 4; §5 data model → Tasks 1, 8; §6 content + validation → Tasks 6, 7; §7 pickup → Task 9; §8 serialization → Task 10; §9 client → Task 11; §10 validation/tests → tests in each task + Task 12; §11 determinism → Tasks 4, 5 determinism tests.
- **Type consistency:** `RoomRole`, `DungeonLootTableDef`/`DungeonLootEntry`, `DungeonLootTableRegistry.Roll`, `DungeonLootPlacer.Place(dungeon, roles, table, seed)`, `DungeonPathClassifier.Classify(dungeon)`, `GameState.TryPickupLoot()`, `PickupLootCommand` / `"pickup_loot"`, `Tile.LootId`, `ExplorationState.CollectedLoot`, `hasLoot`/`lootName` — names used identically across tasks.
- **Known verification points flagged inline** (must confirm against real code during execution, not invent): segment-loading helper in `DungeonGeneratorFallbackTests`, `GameState` ctor + `Player.Position` setter, `SaveSystem`/`SaveRestorer` method names, `EmitActionLog` signature, client `GameState` tile/player field paths.
