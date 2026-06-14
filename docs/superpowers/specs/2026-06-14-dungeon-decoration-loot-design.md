# Dungeon Decoration — Critical Path + Loot (Vertical Slice)

Date: 2026-06-14
Status: design — approved
Depends on: `2026-05-10-dungeon-assembly-design.md` (decoration phase, unbuilt half)
Scope: a playable end-to-end slice of the decoration pipeline — critical-path
classification + deterministic loot placement + 3D render + pickup-to-cache.

## 1. Motivation

Current dungeon generation (commits `b428b88`, `f1ad106`) stitches segments,
validates connectivity, and paces encounters. Dungeons feel *connected* but not
*designed*: every room is equivalent, there is no critical path, and there is no
loot. The assembly spec's decoration phase is designed but unbuilt.

This slice builds the spine (critical-path classification) plus one full
decoration type (loot) all the way through to a player picking an item up. It is
deliberately narrow so it lands complete and playable in one pass.

Out of scope (deferred to follow-up specs): interactables (consoles, switches),
secret doors / discovery mechanics, fog-of-war, richer `AssemblyInput`.

## 2. Architecture

Two new pure engine modules, both mirroring the existing `DungeonPacer` shape
(stateless, seeded, independently testable):

```
DungeonGenerator.Generate / BuildProcedural
  └─ (existing) SegmentStitcher → DungeonPacer → TagBossTile → connectivity
  └─ (new)      DungeonPathClassifier → DungeonLootPlacer → loot validation
```

- `DungeonPathClassifier` — derives a `RoomRole` for every room. No RNG.
- `DungeonLootPlacer` — assigns loot to tiles, role-biased, seeded.

Generation output (the `Dungeon`) gains placed loot as tile data. Runtime
collected-state lives separately in exploration state so the generated dungeon
remains a pure function of its inputs.

## 3. Critical-path classification

`DungeonPathClassifier.Classify(Dungeon dungeon) -> IReadOnlyDictionary<int, RoomRole>`

Algorithm:
1. Identify entrance tile (`StairsUp`) and boss tile (tagged by `TagBossTile`;
   fall back to deepest tile by BFS depth if no boss tile exists).
2. BFS the walkable tile graph from entrance to boss; the shortest path's tiles
   are the **critical tiles**.
3. For each room in `Dungeon.Rooms`, derive connection count (number of
   door/open borders leading out of the room's tiles) and assign:

| RoomRole   | Condition |
|------------|-----------|
| `Entrance` | room contains the entrance tile |
| `Boss`     | room contains the boss tile |
| `Critical` | contains ≥1 critical tile (and not Entrance/Boss) |
| `DeadEnd`  | off critical path, exactly 1 connection |
| `SideBranch` | off critical path, >1 connection |

Deterministic and RNG-free — pure function of dungeon geometry.

New enum `RoomRole { Entrance, Boss, Critical, SideBranch, DeadEnd }` in
`src/engine/RPC.Engine/Dungeon/`.

## 4. Loot placement

`DungeonLootPlacer.Place(Dungeon dungeon, IReadOnlyDictionary<int, RoomRole> roles,
DungeonLootTable table, int seed) -> Dungeon` (returns dungeon with `LootId`s set)

Algorithm (single seeded `GameRandom`):
1. For each room, roll placement against role-biased probability:

| RoomRole     | Loot chance |
|--------------|-------------|
| `DeadEnd`    | 60% |
| `SideBranch` | 30% |
| `Critical`   | 20% |
| `Entrance`   | 0% |
| `Boss`       | 0% |

2. On a hit, deterministically pick one eligible floor tile in the room (lowest
   `(y, x)` ordering — stable, no RNG needed for the pick).
3. Roll one item id from the weighted `DungeonLootTable`.
4. Set `tile.LootId = itemId`.

Bias values come from `2026-05-10-dungeon-assembly-design.md §7`. Seeded from the
dungeon seed → identical seed yields identical loot layout *and* identical item
rolls.

## 5. Data model (engine)

`src/engine/RPC.Engine/Models/Dungeon.cs`:
- `Tile` gains `LootId: string?` (default `null`). Overlay on a `Floor` tile —
  no new `TileType`.

Runtime collected-state (not in the dungeon):
- Exploration/dungeon runtime state gains `CollectedLoot: HashSet<(int X, int Y)>`.
  Persisted in the save. A tile's loot is "present" iff `LootId != null` and its
  coord is not in `CollectedLoot`.

## 6. Content

New content type `content/loot/<dungeonType>.json`, mirroring `EncounterTable`:

```json
{
  "id": "broken_engine",
  "entries": [
    { "itemId": "scrap_plating", "weight": 5 },
    { "itemId": "engine_tech_manual", "weight": 1 }
  ]
}
```

- Engine: `DungeonLootTable` record + `DungeonLootTableRegistry` (mirrors
  `EncounterTableRegistry`), loaded from `content/loot/`.
- `DungeonTemplate` gains optional `lootTableId` (default = dungeon type id).
- Content-pack validation (`tools/content-pack`): every `itemId` in a loot table
  must resolve in the item registry; unknown id = hard compile error.

Authoring for the slice: one loot table per existing dungeon template, plus a
generic fallback table for procedural/unknown dungeon types.

## 7. Pickup flow

New `PickupCommand` (no parameters — acts on the player's current tile).

Handler (`GameCommandHandler`):
1. Resolve the player's current tile.
2. If `tile.LootId != null` and coord not in `CollectedLoot`:
   - Add the item to the **expedition cache** (reuse the existing
     `transfer_to_cache` path / cache store).
   - Add coord to `CollectedLoot`.
   - Emit action-log `dungeon` / `loot_collected` with item id.
   - Broadcast updated state.
3. If no loot present, the command is a no-op (fail-fast: no error, no phantom item).

Picked items land in the same expedition cache the character sheet already reads
(`expeditionCache`, `transfer_from_cache`).

## 8. Serialization

`ExplorationPresenter.SerializeTile` adds, for visible tiles only:
- `hasLoot: bool` — `LootId != null && coord not collected`.
- `lootName: string?` — item display name (for the HUD prompt), present only when
  `hasLoot`.

Collected loot serializes as `hasLoot: false` so the marker disappears after
pickup.

## 9. Client

- `DungeonRenderer` — render a glint / pickup marker mesh on any visible tile with
  `hasLoot`. Visible down corridors (loot is spottable from distance).
- `ExplorationHUD` — when the player stands on an uncollected loot tile, show a
  "Take {lootName}" prompt/button that sends `PickupCommand`. Marker and prompt
  clear on successful pickup.

## 10. Validation + tests

Generator post-check (extends existing connectivity assertion):
- Critical path entrance → boss exists (already implied by connectivity; assert
  the classifier found a path).
- Loot count ≥ 1 when a loot table is available.
- No loot on entrance or boss tiles.
- Every placed `LootId` resolves in the item registry.

Tests (`src/engine/RPC.Tests/`):
- `DungeonPathClassifier`: entrance/boss/critical/dead-end/side-branch roles on a
  known fixture; RNG-free determinism.
- `DungeonLootPlacer`: same seed → identical loot layout + item ids; different
  seeds differ; role bias respected (dead-ends loot more than critical over N
  seeds); no loot on entrance/boss.
- Pickup: command grants item to cache + marks collected; second pickup on same
  tile is a no-op.
- Serialization: collected loot reports `hasLoot:false`.
- Content-pack: loot table with an unknown item id fails validation.

All existing tests (990) stay green; new tests add to the count.

## 11. Determinism guarantee

Loot layout and item rolls are a pure function of `(dungeon seed, loot table)`.
No `System.Random`, no `Guid.NewGuid`, no `DateTime`. Same campaign seed →
identical dungeon decoration across replays and reloads. This is asserted by the
`DungeonLootPlacer` determinism test.

## 12. File touch list

Engine:
- `Models/Dungeon.cs` — `Tile.LootId`.
- `Dungeon/RoomRole.cs` — new enum.
- `Dungeon/DungeonPathClassifier.cs` — new.
- `Dungeon/DungeonLootPlacer.cs` — new.
- `Dungeon/DungeonLootTable.cs` + registry — new.
- `Dungeon/DungeonTemplate.cs` — `lootTableId`.
- `Dungeon/DungeonGenerator.cs` — wire classifier + placer + validation.
- `Commands/CommandTypes.cs`, `Commands/CommandDispatcher.cs`,
  `Commands/GameCommandHandler.cs` — `PickupCommand`.
- exploration runtime state + save — `CollectedLoot`.
- `Web/Presenters/ExplorationPresenter.cs` — `hasLoot` / `lootName`.

Content + tooling:
- `content/loot/*.json` — loot tables.
- `tools/content-pack/Program.cs` — loot-table validation.

Client:
- `renderer/DungeonRenderer.ts` — loot marker.
- `features/exploration/ExplorationHUD.svelte` — Take prompt.
- `shared/types/game.ts` — `hasLoot` / `lootName` on tile type; `pickup` action.

## 13. Decisions (locked)

- Loot visible from distance (glint down corridors), pickup requires standing on
  the tile.
- Explicit "Take" action, not auto-pickup on enter.
- Loot table is its own content type (`content/loot/`), not derived from item
  files — explicit authoring control, natural content boundary.
- Collected-state in runtime/save, not mutated into the generated dungeon.
