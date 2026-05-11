# Phase 1: Core Loop

**Goal:** Does the dungeon crawl feel good? Is combat satisfying? Is 3D navigation readable?

**Scope:** One dungeon template (Broken Engine), 4 classes, party of 4 (2+2 formation), menu-based hub town, 3-dungeon linear questline. No factions, no synergies, no overworld.

> **Party size decision (C4):** Phase 1 stays at 4 characters with 2+2 formation. Combat balance numbers in this phase are throwaway — the goal is validating feel (movement, UI responsiveness, combat flow), not numerical balance. Balance tuning begins in Phase 1.5 with the real 3+3 formation.

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

---

## Task Detail Blocks

Detail for high-risk or ambiguous Phase 1 tasks. Tasks not listed below stay as table-row scope.

### Task 2: WebSocket handshake

**Subtasks:**
1. .NET host exposes `ws://localhost:<port>/game` (port from KDL settings, default 5174).
2. Single-client guard: reject second connection with `error.already_connected`.
3. Handshake sequence: client connects → server sends `hello { protocolVersion, sessionId }` → client sends `ready` → server sends initial `state` snapshot.
4. Heartbeat: server pings every 5s; client must respond within 2s or socket closes.
5. Reconnect: client retries with exponential backoff (250ms → 4s cap); on resume server replays last state snapshot, no delta catch-up in Phase 1.
6. Error envelope: every server-side throw becomes `error { code, message, recoverable }`; client displays toast for `recoverable: true`, falls back to main menu otherwise.

**Acceptance criteria:**
- Cold start: client shows "Connected" within 500ms.
- Killing the .NET host triggers reconnect UI; restarting host recovers session within 2s.
- Sending malformed JSON returns `error.malformed_payload` without crashing host.

---

### Task 5: Grid movement system

**Subtasks:**
1. `GridPosition { x: int, y: int, facing: Direction }` immutable struct.
2. Pure function `tryMove(grid, position, action) → MoveResult`: returns new position or rejection reason (`wall`, `door_locked`, `out_of_bounds`).
3. Action enum: `Forward, Back, StrafeLeft, StrafeRight, TurnLeft, TurnRight`.
4. Movement cost: 1 dungeon turn per cardinal move, 0 for turn-in-place (design decision, revisit in 1.5 if pacing off).
5. Encounter trigger evaluation runs AFTER successful move (task 26 dependency).

**Acceptance criteria:**
- 1000 random move sequences against a fixed grid produce deterministic positions.
- Walking into a wall returns rejection; position unchanged.
- Turn-in-place updates `facing` without changing `x/y`.

---

### Task 7: Dungeon assembler

**Subtasks:**
1. Input: dungeon config (template id, segment pool, encounter table, seed).
2. Connectivity graph build: pick start segment → BFS connect segments via matching connection points → ensure critical path reaches exit segment.
3. Dead-end and side-room placement: weighted random fill of unused connection points.
4. Encounter and loot placement: walk grid tiles, tag tiles flagged `encounter_slot` with picks from encounter table; loot slots placed in dead-end and setpiece rooms.
5. Output: 2D grid (see C3 cell schema) + segment-to-tile index for debug.
6. Deterministic given seed; same seed → same dungeon.

**Acceptance criteria:**
- 100 random seeds produce 100 traversable dungeons (BFS from start reaches exit in every case).
- Same seed produces byte-identical grid output across runs.
- Encounter count matches table's expected range ±1.

---

### Task 9: Movement input loop

**Subtasks:**
1. Client captures key event → enqueue input (buffer cap 2 per design doc 10).
2. Send `action.move { direction }` over WebSocket; start optimistic camera animation (200ms).
3. Server receives → validates via task 5 `tryMove` → emits `state.position` (success) or `error.move_rejected { reason }` (fail).
4. Client on success: confirm animation; on fail before animation end: snap back to last confirmed position, flush buffer.
5. Repeat key: 300ms initial delay, 200ms repeat interval.

**Acceptance criteria:**
- Localhost roundtrip < 50ms input-to-render measured across 100 moves.
- Walking into wall: optimistic step starts then snaps back; no visible glitch at < 1ms localhost latency.
- Holding forward against wall does not flood server (buffer + reject loop self-limits).

---

### Task 16: Content pack compiler v1

**Subtasks:**
1. CLI tool `tools/content-pack`: `pack --input content/ --output build/pack.rpk`.
2. Reads all JSON under `content/` matching `<type>/<id>.json`; validates against type-specific JSON Schema in `tools/content-pack/schemas/`.
3. Reference validation: collect all IDs into symbol table, fail on dangling references (e.g., synergy referencing unknown ability).
4. Binary format: length-prefixed FlatBuffers-style blocks per type; .NET reads via `RPC.Content.PackReader` (mmap when possible, fall back to read-into-buffer on Windows).
5. Dev mode: skip compilation, serve JSON directly (per CC2). Flag: `RPC_CONTENT_DEV=1`.
6. Build-time hot reload: file watcher emits WebSocket `content.reload { type, ids }` to running dev host.

**Acceptance criteria:**
- Full Phase 1 content compiles in < 2s.
- Invalid ability cost reference fails build with file + line.
- Dev mode JSON edit → in-game reflection in < 500ms.

---

### Task 17: Combat state machine

**Subtasks:**
1. `CombatState` struct: `combatants[], turnOrder[], currentTurn, round, log[]`.
2. State machine: `Enter → RollInitiative → TurnLoop → Resolve → Exit`.
3. Turn loop: for each combatant in initiative order, request action → resolve → check death/status → advance.
4. Round end: re-roll initiative per design doc 06, reset per-round flags (synergy used set, action economy).
5. Combat ends when: all enemies dead, all party downed/dead, or party flees.
6. RNG seeded per-combat (per CC3); seed stored in combat state for replay.

**Acceptance criteria:**
- Snapshot harness can replay a combat from seed + action sequence and produce identical end state.
- Round counter increments only after every initiative-listed combatant has acted or been skipped.
- Flee action ends combat with `outcome: fled`; survivors return to dungeon position.

---

### Task 19: Action resolution

**Subtasks:**
1. Action dispatch table: `Attack, Defend, Cast, Item, Flee, Wait`.
2. Attack: roll vs target defense, apply damage with tag system (per C9), emit `combat.event.damage`.
3. Cast: validate component cost (deduct from inventory or HP fallback for Bonewarden), apply effect, emit events.
4. Item: validate item in inventory, apply effect (heal/buff/utility), consume.
5. Defend: set `defending: true` flag on combatant, +25% defense until next turn.
6. Flee: 50% base success, +10% per surviving party member; success ends combat, failure consumes turn.
7. Wait: pass without effect; useful for synergy timing in 1.5+ (Phase 1 no-op).

**Acceptance criteria:**
- Bonewarden with 0 bone fragments casts via HP at 2× cost; HP decremented correctly.
- Defended target takes 25% less damage on next incoming attack only.
- Flee success removes combat encounter from `encounter_id` (no re-trigger).

---

### Task 22: Enemy AI

**Subtasks:**
1. Decision tree interface: `decide(combatState, selfId) → Action`.
2. Behavior tags from content: `aggressive_random`, `focus_weakest`, `retreat_low_hp`, `guard_pattern`, `weak_point`.
3. Bloom creature: pick random adjacent target, attack; ignores positioning.
4. Faction soldier: pick lowest-HP party member in valid range, attack; at < 30% HP attempts retreat to long band.
5. Engine construct: cycle guard pattern (defend → attack → ability); has `weak_point` flag that doubles damage when targeted.
6. Pure function: same input always produces same output (uses combat RNG for any rolls).

**Acceptance criteria:**
- Bloom creature combat replay is deterministic given seed.
- Faction soldier at 25% HP retreats on next turn unless blocked.
- Engine construct `weak_point` hit deals 2× damage; logged in `combat.event.damage.tags`.

---

### Task 25a: Secret discovery v1

**Subtasks:**
1. Hidden door type only in Phase 1 (per design doc 12 phasing).
2. Passive proximity detection: on entering tile within range 1 of any secret, roll `detection_chance = base_difficulty + party_bonus`; on success mark `revealed: true`.
3. Explicit Search action: bound to Space when facing a wall; consumes 1 dungeon turn; reveals secret type and unlock requirement.
4. Reveal triggers wall type change in grid (`hidden → door`); server emits `state.dungeon.cell_changed`; client rebuilds affected geometry.
5. Save secret state in dungeon save (per-campaign flag).

**Acceptance criteria:**
- Walking past a hidden door with `easy` difficulty reveals it most of the time (20%+ base).
- Search action on hidden door 4 times raises cumulative success (failure bonus +5% per attempt, cap +25%).
- Reload preserves `revealed` flags.

---

### Task 26: Encounter triggers

**Subtasks:**
1. Tile-tagged encounter slots: cell `encounter_id: string | null` set during dungeon assembly (task 7).
2. On successful move into tagged tile, check encounter state (`pending | active | resolved`); if `pending`, transition to `active` and trigger combat.
3. Server emits `combat.start { encounterId, enemies, partyState }`; client switches to combat renderer.
4. On combat end: mark encounter `resolved`; do not re-trigger if party re-enters tile.
5. Fleeing leaves encounter as `pending`; re-entering re-triggers.

**Acceptance criteria:**
- Walking onto encounter tile starts combat within 200ms.
- Winning combat clears the trigger; walking back over tile does nothing.
- Fleeing leaves trigger active; re-entering re-prompts combat.

---

### Task 31: Save/load

**Subtasks:**
1. Save schema v1 (see appendix): party, current dungeon grid, exploration state, action log, RNG seeds, settings ref.
2. Save trigger: town only (F5 quick save, town menu Save button).
3. Save format: JSON for dev (`%APPDATA%/TheReach/saves/slot1.json`), gzip-wrapped for release (`slot1.json.gz`).
4. Load: read file → schema version check → deserialize → bootstrap server state → push initial WebSocket state to client.
5. Single save slot in Phase 1; multi-slot deferred to Phase 2.

**Acceptance criteria:**
- Save round-trip: state byte-identical after save → load → save.
- Save file written atomically (write to `slot1.json.tmp` → rename).
- Mismatched schema version shows clear error: "Save from version X not compatible with current Y".

---

### Task 31a: Action log infrastructure

**Subtasks:**
1. `ActionLog` struct: append-only `List<ActionEvent>`.
2. `ActionEvent` per design doc 11 schema: `turn, act, category, type, payload`.
3. Phase 1 categories enabled: `combat` (encounter_started, encounter_won, encounter_fled, character_downed, character_died, synergy_triggered placeholder), `dungeon` (dungeon_entered, dungeon_completed, secret_discovered).
4. Emit points wired into combat state machine (task 17), encounter triggers (task 26), secret reveal (task 25a), dungeon load/exit.
5. Persisted as part of save file (task 31).
6. Privacy: no free-text payloads in Phase 1.

**Acceptance criteria:**
- Completing a dungeon emits `dungeon_entered` at start and `dungeon_completed` at exit, in correct order.
- Killing all enemies in encounter emits `encounter_started` + `encounter_won` with matching `encounterId`.
- Save/load preserves full event ordering.

---

## Appendix A — WebSocket Protocol (Phase 1)

Resolves C1. JSON envelopes; binary deferred.

**Outer envelope:**
```json
{ "v": 1, "type": "<message-type>", "seq": 42, "payload": { ... } }
```
- `v`: protocol version (Phase 1 = 1).
- `seq`: monotonic per direction; client increments for outbound, server for outbound. Used for input ack and replay.

**Client → server:**
| Type | Payload | When |
|---|---|---|
| `ready` | `{}` | After receiving `hello` |
| `action.move` | `{ direction: "forward\|back\|strafe_left\|strafe_right\|turn_left\|turn_right" }` | Player input in dungeon |
| `action.search` | `{}` | Player triggers Search facing a wall |
| `action.interact` | `{ targetId: string }` | Player presses interact on tagged object |
| `combat.action` | `{ actor: charId, type, targetId?, abilityId?, itemId? }` | Player picks combat action |
| `settings.update` | `{ patch: { ... } }` | Settings UI change |
| `save.request` | `{ slot: int }` | Town save trigger |
| `heartbeat.pong` | `{ pingSeq: int }` | Reply to server ping |

**Server → client:**
| Type | Payload | When |
|---|---|---|
| `hello` | `{ protocolVersion: 1, sessionId: string }` | Connection open |
| `state.snapshot` | full game state | After `ready`, on reconnect |
| `state.position` | `{ partyPosition }` | After successful move |
| `state.dungeon.cell_changed` | `{ cells: [{ x, y, ... }] }` | Secret reveal, door open |
| `combat.start` | `{ combatState }` | Encounter trigger |
| `combat.state` | `{ combatState }` | Each combat state change |
| `combat.event` | `{ event }` | Per-action events (damage, death, etc.) for client animation |
| `combat.end` | `{ outcome, rewards }` | Combat resolution |
| `content.reload` | `{ type, ids }` | Dev mode content hot reload |
| `error` | `{ code, message, recoverable: bool }` | Any validation/internal failure |
| `heartbeat.ping` | `{ pingSeq: int }` | Every 5s |

**Sequencing rules:**
- Client `action.*` messages are acknowledged by the next `state.*` or `error` for the same `seq`.
- Server `state.snapshot` always replaces; deltas are additive.
- Out-of-order delivery is impossible on a single WebSocket; no reorder buffer.

---

## Appendix B — Room Segment JSON Schema (Phase 1)

```json
{
  "id": "broken-engine-corridor-01",
  "template": "broken-engine",
  "size": { "w": 4, "h": 6 },
  "tiles": [
    { "x": 0, "y": 0, "floor": true, "walls": { "n": "solid", "s": "open", "e": "solid", "w": "solid" } }
  ],
  "connections": [
    { "id": "north", "tile": { "x": 0, "y": 5 }, "facing": "north", "kind": "corridor" }
  ],
  "encounterSlots": [
    { "id": "slot-1", "tile": { "x": 2, "y": 3 }, "weight": 1.0 }
  ],
  "interactables": [],
  "secrets": []
}
```

- `walls.*`: `solid | open | door | locked_door | hidden | destructible` per C3.
- `connections.kind`: `corridor | chamber | dead_end | setpiece`. Assembler matches kinds to keep visual consistency.
- `secrets` follows design doc 12 schema.

---

## Appendix C — Save Schema v1

```json
{
  "schemaVersion": 1,
  "createdAt": "ISO-8601",
  "campaignId": "uuid",
  "settings": { "ref": "settings.kdl" },
  "rng": { "world": "uint64", "combat": "uint64" },
  "party": { "members": [...], "formation": { "front": [...], "back": [...] } },
  "currentLocation": { "kind": "town\|dungeon", "id": "string" },
  "dungeonState": {
    "grid": { ... per C3 schema ... },
    "encounters": { "<encounterId>": "pending\|resolved" },
    "secrets": { "<secretId>": { "revealed": bool } }
  },
  "actionLog": [ { "turn": 0, "category": "dungeon", "type": "dungeon_entered", ... } ],
  "questState": { "currentMission": "string", "completedMissions": [...] },
  "inventory": { "expedition": [...], "town": [...] }
}
```

- `schemaVersion: 1` per CC1. Phase 1.5+ saves are v2+; v1 saves migrate or break per release decision.
- Write order: serialize → write `.tmp` → fsync → rename. Reject load on missing required fields.
