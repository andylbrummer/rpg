# Dungeon Assembly — Design Spec
Date: 2026-05-10
Status: design — formalizes informal `docs/design/07` + extends current `DungeonBuilder.cs`
Depends on: content-pipeline (segment schema), encounter-generation (table assignment), determinism-replay (DungeonRng)
Scope: segment selection, connection algorithm, critical-path enforcement, encounter placement, loot placement, secret rooms, validation. Phase 1 = Broken Engine template; Phase 2 = 4 templates; Phase 3 = LLM arrangement.

## 1. Inputs

```csharp
public record AssemblyInput(
    string DungeonType,           // e.g., "broken_engine"
    int DungeonLevel,             // 1..10
    int Seed,                     // for DungeonRng
    int PartyLevel,
    Dictionary<string,FactionPhase> FactionPhases,
    Dictionary<string,object> WorldFlags,
    string? MissionId,            // active mission steering content
    List<string> RequiredTags,    // mission-required segments (e.g., setpiece-control)
    int TargetSize                // S=10-15 segments, M=15-25, L=25-40
);
```

Output: `Dungeon` (existing record) with tiles, walls, encounter placements, interactables, secret-door states.

## 2. Segment pool

Per `DungeonType`, segments tagged in their JSON:

```json
{
  "id": "broken-engine-corridor-A",
  "template": "broken_engine",
  "size": "small",
  "category": "corridor",
  "connections": { "north":"open", "south":"open" },
  "geometry": {...},
  "encounters": { "table":"broken-engine-low" },
  "tags": ["corridor","faction-evidence-slot"]
}
```

Categories per `docs/design/07`:

| Category | Role | Count target |
|---|---|---|
| `entrance` | one per dungeon, fixed start | 1 |
| `corridor` | filler, connectors | many |
| `chamber` | mid-size rooms, encounter slots | several |
| `dead-end` | branches with loot or secrets | several |
| `puzzle` | interactable challenges | 1-2 |
| `treasure` | loot caches | 1-3 |
| `setpiece` | unique authored rooms | 1 (mission boss) |

## 3. Algorithm

Two-phase: **layout** (geometric placement) then **decoration** (encounters, loot, secrets).

### 3.1 Layout

```
1. Pick entrance segment (deterministic by template).
2. Place at origin.
3. Open frontier = set of (segment, side, position) ready to extend.
4. While remaining segments < TargetSize:
   a. Pop random frontier entry (DungeonRng weighted by connection openness).
   b. Pick candidate segment whose opposite-side connection is compatible.
   c. Apply rotation if segment is rotatable.
   d. Collision-check against existing placed segments.
   e. If valid: place, add new frontier entries from its other open sides.
   f. Else: skip (retry up to 50 times before giving up on that frontier entry).
5. If too few segments placed: emit warning + accept smaller dungeon.
```

### 3.2 Critical path

After layout:

```
6. Find shortest path from entrance to a setpiece-tagged segment.
7. If none: append setpiece by force-replacing a leaf chamber.
8. Mark segments on critical path. Non-critical = side branches.
```

Side branches host secret rooms + treasure + optional encounters.

### 3.3 Decoration

```
9. Place required-tag segments first (mission-required).
10. Distribute encounter trigger tiles:
    - Critical path: every 3-5 segments, place encounter trigger.
    - Side branches: 1-2 triggers per branch.
    - Setpiece: 1 fixed encounter (mission boss).
11. Distribute loot:
    - Dead-ends: 60% chance treasure.
    - Side branches: 30% chance.
    - Critical chambers: 20% chance.
12. Place interactables:
    - Each puzzle segment: its authored interactable.
    - Each setpiece: its authored interactable.
13. Place secret doors:
    - Identify candidate connections (perpendicular to critical path).
    - Authored `connections.east:"hidden"` etc. respected.
    - 50% chance hidden door connects to a side branch.
14. Compute initial fog-of-war (existing ExploredTiles).
```

## 4. Connection types

Per `docs/design/07`:

| Type | Behavior |
|---|---|
| `open` | walkable doorway |
| `door` | requires interact to open (no key) |
| `locked` | requires key, lockpick (Hollow Filch), or break |
| `hidden` | invisible until detected (Marcher Pathfinder reveal or click-search) |
| `barred` | one-way from other side |
| `closed` | walled off, never opens this run |

Detection mechanics:
- Hidden doors revealed by tile-front interaction (player walks adjacent and presses interact) with `(d20 + INT_mod ≥ DC)` roll.
- Pathfinder branch: passive reveal within 2 tiles.
- Inkblood Cartographer: passive reveal within 3 tiles.

## 5. Determinism

DungeonRng = sub-rng derived from RootSeed + dungeonType + seed (per determinism-replay spec §2). Same input → same dungeon every time. Phase 3 LLM arrangement bakes campaign rolls into seed.

## 6. Engine

Extend `src/engine/RPC.Engine/Dungeon/DungeonBuilder.cs`:

```csharp
public class DungeonBuilder {
    public Dungeon Assemble(AssemblyInput input) {
        var layout = LayoutPhase(input);
        var decorated = DecoratePhase(layout, input);
        ValidateOrThrow(decorated);
        return decorated;
    }
}
```

Validators:
- Critical path exists from entrance to setpiece.
- All required-tag segments present.
- No orphaned segments (unreachable).
- Tile grid covers placed segments without overlaps.
- Every encounter trigger references a valid encounter table.

## 7. Persistence

Dungeon state at exploration time persists in `GameState.CurrentDungeon`. Save-format spec covers serialization. Mid-dungeon save = full dungeon state preserved.

Re-entering same dungeon (e.g., Underway per `docs/design/07`) re-assembles if `dungeonType.reassembleEachVisit = true`. Persistent dungeons keep prior state in `WorldFlags`.

## 8. Phase content

Phase 1: Broken Engine pool of ~15 segments. 3 difficulty tiers via segment selection weighting + encounter table tier.

Phase 1.5: Bloom Site added (4-6 segments shared; rest unique).

Phase 2: Contested Ruin, Underway. Underway uses `reassembleEachVisit: true` with persistent junctions.

Phase 3: full 8 templates per `docs/design/07`.

## 9. Performance

Target: dungeon assembly <500 ms for `TargetSize = 20` segments. Profile shows current `DungeonBuilder` runs ~15 ms for current 4-segment hardcoded test. Algorithmic growth ~O(N × frontier-size); acceptable to N=40.

If exceeded Phase 2: pre-bake layout shape templates per dungeon type with hot-swap segment skinning.

## 10. Tests

- xUnit: same seed → same dungeon (deterministic).
- xUnit: critical path exists in all generated dungeons across 100 seeds.
- xUnit: required-tag segments always placed when requested.
- xUnit: no segment overlaps in tile grid.
- xUnit: encounter triggers reference valid tables.
- Manual: visual inspection across 20 seeds per dungeon type.

## 11. Out of scope

- 3D vertical level transitions (stairs touched in content schema; multi-level Phase 2).
- Real-time procedural shifting (Underway re-assembles between visits, not in-visit).
- Player-built dungeons.
- Boss arena scaling beyond authored setpieces.
