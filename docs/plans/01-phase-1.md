# Phase 1: Core Loop

**Status: ✅ Complete (G1–G5 shipped).** Vertical slice playable. 18 Playwright e2e tests pass. See `docs/dartboard/plan.md` for per-task status.

**Goal:** Does the dungeon crawl feel good? Is combat satisfying? Is 3D navigation readable?

**Scope:** One dungeon template (Broken Engine), 4 classes, party of 4 (2+2 formation), menu-based hub town, 3-dungeon linear questline. No factions, no synergies, no overworld.

> **Party size decision (C4):** Phase 1 stays at 4 characters with 2+2 formation. Combat balance numbers in this phase are throwaway — the goal is validating feel (movement, UI responsiveness, combat flow), not numerical balance. Balance tuning begins in Phase 1.5 with the real 3+3 formation.

> **As-built note.** Phase 1 shipped against the table-row spec below. Detail blocks and appendices originally drafted here moved to Phase 1.5 as **retrofit specs + build learnings** — Phase 1.5 must close the gap between as-designed and as-built. See Phase 1.5 Group 5.5 + Appendix Y/Z.

## Group 1: Skeleton

Get something on screen. Prove every layer of the stack works end-to-end.

| # | Task | Layer | Output |
|---|---|---|---|
| 1 | Photino shell | .NET | Host boots, opens webview, loads Vite dev server (dev) / bundle (release) |
| 2 | WebSocket handshake | Both | Client connects, server sends heartbeat, client displays status |
| 3 | Empty Three.js scene | Client | Camera renders a lit floor plane in the webview |
| 3a | Audio hook system | Client | Stub audio manager with placeholder gain nodes. No assets yet, but the pipeline exists. |
| 4 | REST content endpoint | Both | Server serves a hardcoded room segment JSON, client fetches and logs it |

**Validation:** A Photino window shows a 3D floor plane with "Connected" in the corner.

## Group 2: Dungeon Navigation

The core feel test. If this doesn't feel right, nothing else matters.

| # | Task | Layer | Output |
|---|---|---|---|
| 5 | Grid movement system | Engine | Player position on 2D grid, cardinal movement, 90-degree turns. Pure state function. |
| 6 | Room segment loader | Engine | Reads JSON room segments, builds connectivity graph. Broken Engine template, 10-15 segments. |
| 7 | Dungeon assembler | Engine | Connects segments into a dungeon with a critical path. Output: tile grid with wall/floor/door data. |
| 8 | Three.js dungeon renderer | Client | Receives dungeon grid over WebSocket, renders low-poly walls/floors/ceilings. First-person camera locked to grid. |
| 9 | Movement input loop | Both | Arrow keys → WebSocket → server validates → new state → renderer updates camera. **The moment you know if it feels good.** |
| 9a | Settings system v1 | Both | KDL loader, localStorage mirror, hardcoded keybindings UI. Display defaults. |
| 10 | Automap | Client | Svelte 2D minimap panel. Updates as player moves. Shows explored tiles, doors, current position. |

**Validation:** Walk through a procedurally assembled Broken Engine dungeon. Movement feels responsive (< 50ms input-to-render). Automap tracks correctly. Secret passages exist but aren't revealed until found (passive detection + explicit search; see design doc 12).

### Content needed
- 10-15 room segments for Broken Engine template (corridors, chambers, dead ends, one setpiece)
- Room segment JSON files following the format in design doc 07

## Group 3: Characters & Inventory

The things you bring into combat.

| # | Task | Layer | Output |
|---|---|---|---|
| 11 | Character data model | Engine | Stats, HP, class, level, equipment slots, inventory. Pure structs. |
| 12 | Party system | Engine | 4 characters, front/back row assignment (2+2 in Phase 1). |
| 13 | Content: 4 classes | Content | Bonewarden, Stillblade, Cauterist, Hollow. One branch each, abilities as data. |
| 14 | Content: items & equipment | Content | Weapons, armor, potions, bone fragments, cautery supplies. Enough for 3 dungeons. |
| 15 | Inventory UI | Client | Equip/unequip, use items, view stats. Party status bar (HP/status for all 4). |
| 16 | Content pack compiler v1 | Tools | Reads content/ JSON, writes binary pack. .NET loads from pack. |

**Validation:** Create a party of 4, equip them, view stats. Inventory operations feel snappy. Content loads from binary pack, not raw JSON.

### Content needed
- 4 class definitions with stats, abilities, level-up table (cap 5)
- 15-20 items (weapons, armor, consumables, components)

## Group 4: Combat

The second feel test.

| # | Task | Layer | Output |
|---|---|---|---|
| 17 | Combat state machine | Engine | Enter → initiative roll → turn loop → resolution. Pure function: state + action → new state. |
| 18 | Initiative system | Engine | Speed + class mod + gear + random. Visible turn order. Re-rolls each round. |
| 19 | Action resolution | Engine | Attack (damage, hit/miss), defend, cast (ability effect, component cost), use item, flee, wait. |
| 20 | Range bands | Engine | Melee/short/long. Front row at melee, back row behind. Enemy placement by encounter data. |
| 21 | Enemy data | Content | One bloom creature, one faction soldier, one Engine construct. Stats, abilities, AI behavior tags. |
| 22 | Enemy AI | Engine | Decision tree per type. Bloom: aggressive random. Soldier: focus weakest, retreat low HP. Construct: guard pattern, weak point. |
| 23 | Combat renderer | Client | Transition from dungeon view. Enemy groups at range bands. Simple animations: swing, projectile, flash. Damage numbers. |
| 24 | Combat UI | Client | Initiative bar, action menu, targeting selector, HP bars, status effects with icons/duration. |
| 25 | Snapshot test harness | Tests | Feed combat state + action sequence → assert final state. First batch: 10 scenarios. |

**Validation:** Combat feels tactical even without synergies. Initiative bar helps planning. Range bands create positioning decisions. Resource costs (bone fragments, HP for blood magic, cautery supplies) create tension. 10 snapshot tests pass.

### Balance targets (from design doc 06)
- Front-liner HP at level 1: ~30. Back-liner: ~18.
- Basic melee: 5-8 damage. Strong ability: 10-15.
- Trash encounter: 3-4 rounds. Standard: 5-7.
- Cauterist healing budget: ~150% of one front-liner's max HP per expedition.

## Group 5: The Loop

Connect everything into a playable game.

| # | Task | Layer | Output |
|---|---|---|---|
| 25a | Secret discovery system v1 | Engine + Client | Passive proximity detection + explicit search action. Hidden doors only. Breakable walls deferred to Phase 1.5. |
| 26 | Encounter triggers | Engine | Tiles tagged with encounter data. Step on tile → combat with that encounter's enemies. |
| 27 | Dungeon-combat-dungeon flow | Both | Exit combat → return to dungeon, same position. Dead enemies don't re-trigger. Resources persist. |
| 28 | Hub town | Client | Menu-based Svelte UI. Tavern (recruit from fixed roster), market (buy/sell), mission board (next dungeon). |
| 29 | 3-dungeon questline | Content + Engine | Three Broken Engine configs, escalating difficulty. Linear: complete one → town → next mission. |
| 30 | Leveling | Engine | XP from combat + exploration (new tiles). Level up at town. Cap 5. |
| 31 | Save/load | Host | Serialize full game state to disk. Single save slot. Save at town only. |
| 31a | Action log infrastructure | Engine | Append-only event stream per design doc 11. Server-emitted only. Schema: `turn`, `act`, `category`, `type`, `payload`. Phase 1 categories: `combat`, `dungeon`. Serialized inside save file. |
| 32 | Playwright smoke tests | Tests | Launch Photino, navigate room, open inventory, enter/exit combat, save/reload. 5-6 tests. |

**Validation:** Play through all 3 dungeons. Session takes 1-2 hours. Combat attrition creates real decisions by dungeon 2. Leveling feels rewarding. Save/load round-trips without data loss. All Playwright smoke tests pass.

### Content needed
- 3 dungeon configurations (segment selection + encounter placement + loot tables)
- 4-6 recruitable characters in tavern roster
- Market inventory and pricing

## Dependency Graph

```
Group 1 (Skeleton)
  └─► Group 2 (Navigation)
        ├─► Group 3 (Characters)
        │     └─► Group 4 (Combat)
        │           └─► Group 5 (Loop)
        └─► Group 5 (Loop) [automap feeds into exploration XP]
```

Groups are sequential. Within each group, tasks can be parallelized across .NET and client work:
- Group 2: tasks 5-7 (.NET) parallel with task 8 (client), converge at task 9
- Group 3: tasks 11-14 (.NET/content) parallel with task 15 (client)
- Group 4: tasks 17-22 (.NET/content) parallel with tasks 23-24 (client), converge at combat flow

