# Phase 1.5: Minimum Viable Strategy

**Goal:** Does the strategic layer change how players approach the core loop? Validates formation, faction tension, and composition consequences before the full content investment.

**Prerequisite:** Phase 1 complete (✅ shipped). Dungeon navigation and combat feel good.

**Duration estimate:** 5–7 weeks (was 4–6; added 1 week for Group 5.5 Phase 1 retrofits) — 2 engineers + 1 content author.

> **Start here:** **Group 5.5 (Phase 1 Retrofits)** must complete before Groups 6–9 can be safely built on the existing codebase. Retrofits close the gap between what Phase 1 design intended and what shipped. Skipping them creates compounding drift through Phase 2.

---

## Phase 1.5 Entry / Exit Criteria

**Entry (this phase starts when):**
- Phase 1 G1–G5 marked ✅ on dartboard.
- Full Phase 1 Playwright suite green on `build/kimi`.
- `dotnet test` green across all engine projects.
- This plan reviewed and Group 5.5 task tickets created in Dart.

**Exit (this phase ends when):**
- Group 5.5 retrofits ✅ (32a–32g).
- Groups 6–9 + 9.5 tasks ✅.
- Full 15-turn campaign playable end-to-end with 6-character party, Bureau/Convocation factions, 5 synergies discoverable, two-node overworld, both dungeon templates.
- xUnit suite ≥ Phase 1 count + Phase 1.5 deliverables (~30 new unit, ~10 snapshot).
- Playwright suite passes (Phase 1 18 tests + Phase 1.5 ~6 new = ~24 total).
- Action log produces a valid template epilogue stub (template wiring; full epilogue in Phase 2).

---

## Group 5.5: Phase 1 Retrofits

Retrofit tasks bridge the gap between Phase 1 design intent (originally drafted as detail blocks in `docs/plans/01-phase-1.md`) and the shipped Phase 1 build. Each task references the as-built state from Appendix Y and the design gap from Appendix Z. **All seven tasks must complete before Group 7 (Factions) starts; Groups 6/8/9 can begin in parallel with Group 5.5 once their specific blocking retrofit lands (see dependency graph).**

### 32a. Action log infrastructure
**Layer:** Engine
**Owner:** Backend lead

**Subtasks:**
1. `ActionLog` append-only `List<ActionEvent>` per design doc 11 schema: `turn, act, category, type, payload`.
2. Phase 1 categories backfilled: `combat` (encounter_started, encounter_won, encounter_fled, character_downed, character_died), `dungeon` (dungeon_entered, dungeon_completed, secret_discovered).
3. Emit points wired into existing systems:
   - `GameState.TryMoveForward` (dungeon_entered fires on first move into new dungeon).
   - `CombatEngine` resolution end → `encounter_won` / `encounter_fled`.
   - `GameState.TriggerEncounter` → `encounter_started`.
4. Persisted as part of save (extends `SaveData` to v2; see task 32d).
5. Phase 1.5 categories added by tasks 51e + this task: `faction`, `overworld`.
6. Privacy: no free-text in payloads (matches design doc 11).

**Acceptance criteria:**
- Completing a dungeon emits `dungeon_entered` then `dungeon_completed` in order.
- Killing all enemies emits `encounter_started` + `encounter_won` with matching `encounterId`.
- Save/load preserves full event ordering. Old v1 saves are not loadable — Phase 1.5 break is acceptable per CC1.

**Tests:** xUnit `ActionLogTests` (emit ordering, payload shape, serialization round-trip). 1 Playwright `action-log-persists.spec.ts` (run combat, save, reload, inspect log via debug endpoint).

**Depends on:** Task 32d (save schema v2).

---

### 32b. WebSocket protocol envelope (v2)
**Layer:** Engine + Client
**Owner:** Both leads

**Background:** Phase 1 shipped flat `{ "type": "...", ... }` messages. Plan called for envelope `{ v, type, seq, payload }`. Phase 1.5 introduces enough new message types (formation, branch, mission, vendor, travel, journal, keybinding) that a structured envelope becomes load-bearing.

**Subtasks:**
1. Replace flat protocol with envelope: `{ "v": 2, "type": "...", "seq": int, "payload": {...} }`. No compat shim — old client breaks, ship matching client in same release.
2. Server emits `hello { protocolVersion: 2, sessionId }` on connection. Client must reply `ready` before any other message.
3. `seq` monotonic per direction; client-sent `seq` echoed in matching ack (`state.*` or `error`).
4. Heartbeat: server pings every 5s with `heartbeat.ping { pingSeq }`; client must `heartbeat.pong { pingSeq }` within 2s.
5. Error envelope: every server throw → `error { code, message, recoverable: bool }`. Replace `Console.WriteLine($"Error handling message: {ex}")` with client-facing errors carrying code mapping.

**Acceptance criteria:**
- Client sends `action.move` with `seq=42`; server responds with state carrying matching `seq`.
- Killing server triggers reconnect; on reconnect, client receives full `state.snapshot`, no delta catch-up.
- Malformed JSON → `error.malformed_payload` with `recoverable: true`; client surfaces toast.

**Tests:** xUnit `ProtocolEnvelopeTests` (seq monotonicity, hello sequencing, error code mapping). Playwright `ws-reconnect.spec.ts` (server bounce + heartbeat timeout).

**Depends on:** None (foundational).

---

### 32c. Strafe + cancel input
**Layer:** Engine + Client
**Owner:** Both leads

**Background:** Phase 1 ships `move_forward`, `turn_left`, `turn_right` only. Plan (and design doc 10 keybinding table) specifies six cardinal actions plus cancel. Phase 1.5 formation UI needs cancel; Phase 2 a11y settings expect full input surface.

**Subtasks:**
1. Add `move_back`, `strafe_left`, `strafe_right` to `GameState` and protocol.
2. Add input buffer (2-slot, design doc 10) on client.
3. Repeat delay 300ms initial / 200ms repeat per design doc 10.
4. Server validates each direction against `Dungeon.CanMoveTo`.
5. Cancel action: clears input buffer and closes top modal; bound to Escape per design doc 10.

**Acceptance criteria:**
- Holding W against a wall does not flood server (buffer + reject loop self-limits).
- Strafe left/right moves perpendicular to facing without changing facing.
- Escape during combat targeting cancels the targeting state, not the combat.

**Tests:** xUnit `GridMovementTests` (strafe direction math, 6-direction coverage). Playwright `strafe-and-cancel.spec.ts` (key chord rebind + strafe + escape in combat).

**Depends on:** Task 32b (envelope).

---

### 32d. Save schema v2 (replace v1, no migration)
**Layer:** Engine
**Owner:** Backend lead

**Background:** Phase 1 ships `SaveData.Version = "1"` (string) with hard reject on mismatch. Phase 1.5 needs new fields (action log, formation, reputation). Per CC1, break v1 saves — players expect schema breaks during development.

**Subtasks:**
1. Replace `Version` string with int `schemaVersion = 2`. Delete the v1 reader path.
2. v2 fields:
   - `party` (3+3 formation array of 6 slots, empty slots = null).
   - `player` (position + facing).
   - `actionLog` (per design doc 11).
   - `formation` (front[3] + back[3]).
   - `reputation` (per design doc 04 map).
   - `exploredTiles`, `mode`, `dungeonType`, `settings` (KDL ref hash).
3. Load: if file is not v2, log and delete. Player starts new game.
4. Atomic write: serialize → `.tmp` → fsync → rename. Phase 1 currently does direct write (race risk).

**Acceptance criteria:**
- v1 save file is deleted with a clear log message; new game starts fresh.
- Save round-trip on v2 yields byte-identical state.
- Power-cut simulation (kill -9 mid-write) leaves either intact prior v2 or intact new v2, never half-written.

**Tests:** xUnit `SaveSchemaV2Tests` (round-trip, v1 detect-and-delete, atomic write under fault injection). No Playwright (file-system level).

**Depends on:** Task 32a (action log feeds schema).

---

### 32e. Encounter trigger system — tile-tagged path
**Layer:** Engine
**Owner:** Backend lead

**Background:** Plan (Phase 1 task 26) specified tile-tagged encounters: assembler tags tiles with `encounter_id`, stepping on tile triggers that exact encounter. Build shipped probabilistic per-step trigger (`encounterChance = 0.05 + 0.08 * stepsSinceEncounter`) drawing from one dungeon-wide encounter table. Both modes have merit; Phase 1.5 needs both:
- Authored set-piece encounters (tile-tagged) for boss rooms, narrative beats.
- Wandering encounters (probabilistic) for filler.

**Subtasks:**
1. Add `encounterId: string | null` to `Dungeon` cells.
2. Dungeon assembler tags `encounter_slot` connection-point tiles with picks from encounter table at build time.
3. On step, check tile tag first; if present and unresolved, trigger that encounter and mark resolved. If no tag, fall back to probabilistic roll.
4. Wandering encounter table separate from tile-tagged authored set: `dungeon.wanderingTableId` vs `dungeon.encounterTableId` (current single field).
5. Document the encounter probability formula (`0.05 + 0.08 * stepsSinceEncounter`) as a balance knob in design doc 06 appendix; expose for content tuning.

**Acceptance criteria:**
- Boss tile tagged with `boss-encounter-1` always triggers that exact encounter on entry.
- Walking corridor tiles still triggers wandering encounters at the existing rate.
- Resolving a tagged encounter does not re-trigger; fleeing leaves it pending.

**Tests:** xUnit `TileTaggedEncounterTests` (tag respect, wandering fallback, resolve/flee state). Playwright `setpiece-encounter.spec.ts` (walk into tagged tile → expected enemies appear).

**Depends on:** Phase 1 tasks 7, 26 (✅ shipped).

---

### 32f. Hub town → in-engine state machine
**Layer:** Engine + Client
**Owner:** Backend lead

**Background:** Phase 1 town is `GameMode.Menu` with all logic in Svelte (`TownMenu.svelte`). Phase 1.5 introduces faction contacts, vendor stock, mission acceptance, downtime allocation — these need server authority. Pure client state will diverge from save/load and break multi-client testing. **This task ships empty stubs for vendor/mission/contact slots; Group 7 (Factions, tasks 38–42) fills them.**

**Subtasks:**
1. Move town state to `GameState.Town`: `currentTownId, availableMissions[], vendorStock[], factionContacts[], tavernRoster[]`. All collections empty in this task — populated by Group 7 work.
2. Client `TownMenu` reads from `state.town` via WebSocket, sends actions (`mission.accept`, `vendor.purchase`, `tavern.recruit`).
3. Tavern recruit roster server-generated (Phase 1.5 has a fixed roster of 6 candidates; faction-gated recruits land in Group 7).
4. Resolves the `NaN guard in TownMenu` quality fix (commit c451dbd) at the root — server never sends NaN if party empty since recruitment happens server-side.

**Acceptance criteria:**
- Refreshing the client mid-town shows same recruits, same mission offers (server authoritative).
- Saving in town and reloading restores exact town state including which missions were viewed.
- Empty `availableMissions: []` and `vendorStock: []` render without errors (Group 7 fills later).

**Tests:** xUnit `TownStateTests` (recruit generation determinism, save round-trip). Playwright `town-server-authoritative.spec.ts` (refresh keeps roster).

**Depends on:** Task 32b (envelope).

---

### 32g. Dungeon segment authoring — JSON loader path
**Layer:** Engine + Content
**Owner:** Backend lead + Content author

**Background:** Phase 1 ships `DungeonBuilder.AddSegment(CreateEntranceRoom())` — segments hardcoded in C#. Plan (task 6) specified JSON segment loader. Phase 1.5 Bloom Site (task 51) and all Phase 2 dungeon templates require JSON authoring; cannot scale via C# code-gen.

**Subtasks:**
1. Define segment JSON schema (already in Appendix Y).
2. `SegmentLoader.LoadFromDirectory(string dir)` reads all `*.json` under `content/segments/<template>/`.
3. Replace `Create*Room()` C# methods with JSON files under `content/segments/broken-engine/`.
4. Content pack compiler validates each segment: connection-point sanity, tile shape, no orphan tiles.
5. Dev mode reload: file watcher → `content.reload` over WebSocket.

**Acceptance criteria:**
- Existing Phase 1 dungeons assemble identically from JSON.
- Adding a new segment to `content/segments/broken-engine/` reflects in next dungeon without recompile.
- Invalid segment (orphan tile) fails content pack build with file + line.

**Tests:** xUnit `SegmentLoaderTests` (parse, schema validation, dungeon-assembly equivalence against Phase 1 hardcoded segments). Content-pack CLI integration test (invalid segment fails). Playwright `content-hot-reload.spec.ts` (touch JSON → in-game change).

**Depends on:** Phase 1 task 16 (content pack compiler, ✅ shipped).

---



## Group 6: Full Formation

### 33. Expand party to 6
**Layer:** Engine + Client  
**Owner:** Backend lead

**Subtasks:**
1. Update `PartyState` struct: `frontRow: Character[3]`, `backRow: Character[3]` (was `Character[2]` each).
2. Update combat engine: 3 enemy slots per range band (was 2).
3. Update encounter tables: enemy group counts scale to 3+3 (max 4 groups, up to 3 enemies per group).
4. Update action economy: adjust standard encounter duration target from 5–7 rounds to 6–8 rounds (more combatants).
5. Update save schema: v2 has front[3] + back[3] directly (no migration from v1; per task 32d, v1 saves are deleted).

**Acceptance criteria:**
- Combat snapshot tests with 6 characters pass.
- Encounter with 3 enemy groups in melee band renders correctly.
- Save from Phase 1 loads without crash; empty slots are fillable from tavern.

**Depends on:** Phase 1 Group 5 (combat, party, save systems)

---

### 34. Row-dependent abilities
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Add `requiredRow: "front" | "back" | null` to ability definition schema.
2. In combat action validation, reject abilities whose `requiredRow` doesn't match the actor's current row.
3. Add `rowChangeCost: "action" | "quick_action" | "free"` to ability schema (some abilities let you swap row as part of the action).
4. Update UI state message: include `availableAbilities` per character, filtered by row.

**Acceptance criteria:**
- A melee-only ability (e.g., Stillblade basic attack) is greyed out when the character is in back row.
- A character moved to back row loses access to front-row abilities immediately.
- Snapshot test: party with 3 front-liners vs party with 2 front + 1 back produces different damage output.

**Depends on:** Task 33

---

### 35. Add Fieldwright + Inkblood content
**Layer:** Content  
**Owner:** Content author

**Deliverables:**
1. **Fieldwright (Artificer branch)** — 6 abilities:
   - 2 basic attacks (melee wrench, ranged gadget toss)
   - 2 deployables (turret, shield drone) — deployables occupy a party slot as a summoned ally
   - 2 utility (Overcharge ally device, Repair construct)
   - Ability costs: Engine Charges (carried component)
2. **Inkblood (Cartographer branch)** — 6 abilities:
   - 2 map/reveal abilities (secret detection aura, automap fill)
   - 2 lore abilities (identify enemy weakness, decipher inscription)
   - 2 combat abilities (memory-cost debuff, knowledge bolt)
   - Ability costs: Ink Vials (carried component), memory (temp stat loss)
3. Level-up tables for both classes: levels 1–5 (cap unchanged from Phase 1).
4. Starting stats: Fieldwright (front-liner HP ~28, medium armor); Inkblood (back-liner HP ~20, light armor).

**Content format:** Same JSON schema as Phase 1 classes. Re-use existing item/component definitions; add `engine_charge` and `ink_vial` components to the component table.

**Acceptance criteria:**
- Both classes appear in tavern roster.
- Fieldwright deployables appear as allies in combat with their own initiative.
- Inkblood memory costs show as temporary stat reductions in character sheet.

**Depends on:** Phase 1 Group 3 (character data model, content pack compiler)

---

### 35a. Branch choice UI (level 3)
**Layer:** Client  
**Owner:** Frontend lead

**Subtasks:**
1. Design branch choice modal: shows 2 branch options with name, description, ability preview, and faction gate warning (if applicable).
2. Implement modal triggered at town level-up (not mid-dungeon).
3. Add "permanent choice" confirmation with undo disabled.
4. Send `branch.choose` WebSocket message; server validates (level ≥ 3, no prior branch choice).

**Acceptance criteria:**
- Modal appears when a level-2 character gains enough XP to hit level 3 in town.
- Modal blocks all other town UI until resolved.
- Choice is persisted in save file; reloading the game preserves the branch.

**Depends on:** Task 35b

---

### 35b. Branch system engine
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Add `branchChoice: string | null` to `CharacterState`.
2. Add `availableBranches: string[]` to class definition schema.
3. At level-up, if level == 3 and `branchChoice == null`, flag character as `awaitingBranchChoice`.
4. Town state machine checks `awaitingBranchChoice`; blocks town exit until resolved.
5. On branch choice, unlock branch-specific abilities in character's ability list.

**Acceptance criteria:**
- Character at level 3 without a branch choice cannot leave town.
- Choosing a branch adds the correct abilities to the character's combat action list.
- Snapshot test: same character with different branches has different combat output.

**Depends on:** Task 33

---

### 36. Formation UI
**Layer:** Client  
**Owner:** Frontend lead

**Subtasks:**
1. Drag-and-drop row assignment: 3 slots front row, 3 slots back row.
2. Visual distinction: front row slots have a shield icon backdrop; back row slots have a bow icon.
3. Combat renderer: front-row characters rendered at melee band; back-row at short band.
4. Targeting UI: melee abilities can only target melee-band enemies; ranged abilities target all bands.

**Acceptance criteria:**
- Dragging a character from front to back row updates formation immediately.
- Combat renderer shows 3 characters in front row, 3 in back.
- Targeting UI highlights valid targets based on selected ability's range.

**Depends on:** Tasks 33, 34

---

### 37. Update combat renderer for 6
**Layer:** Client  
**Owner:** Frontend lead

**Subtasks:**
1. Resize combat viewport: 3 character portraits per row, scaled to fit without overlap.
2. Enemy group slots: up to 3 enemies per range band (melee/short/long).
3. Initiative bar: expanded to 12 slots (6 party + up to 6 enemies).
4. Status effect icons: shrink from 32×32 to 24×24 to fit more icons per character.

**Acceptance criteria:**
- Combat viewport fits in 1920×1080 without horizontal scroll.
- Initiative bar is readable at a glance with 10+ combatants.
- No visual overlap at max encounter size (3 groups × 3 enemies + 6 party).

**Depends on:** Tasks 33, 36

---

## Group 7: Faction Foundation

### 38. Reputation system
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. `ReputationState` struct: `Map<FactionId, int>` where int is clamped -100..+100.
2. Reputation delta function: `applyDelta(faction, amount, source)` — logs source for debugging.
3. Threshold checking: `getThresholdEffects(faction)` returns unlocked content at current rep.
4. Opposed faction propagation: completing a Bureau mission triggers automatic Convocation penalty (per C6 rates).
5. Save/load: reputation state is part of campaign save.

**Acceptance criteria:**
- Completing a Bureau side mission (+5 Bureau) automatically applies -2 Convocation.
- Reputation cannot exceed ±100; deltas are clamped.
- Save/load round-trip preserves exact reputation values.

**Depends on:** Nothing (new system)

---

### 39. Two factions: Bureau + Convocation
**Layer:** Content  
**Owner:** Content author

**Deliverables:**
1. **Bureau profile:**
   - Vendor inventory: 6 items (Engine-tech tools, official writs, forensic necromancy gear)
   - Reputation thresholds: 25 (vendor access), 50 (exclusive recruit), 75 (patron office upgrades)
   - 2 side missions: "Engine Inspection" (dungeon sweep + document collection), "Quarantine Enforcement" (combat-heavy, bloom creature extermination)
2. **Convocation profile:**
   - Vendor inventory: 6 items (bloom-adapted gear, mutation-resistant armor, research notes)
   - Reputation thresholds: 25 (vendor access), 50 (exclusive recruit), 75 (forbidden knowledge cache)
   - 2 side missions: "Bloom Sample Collection" (exploration + sample retrieval), "Infiltration" (stealth dungeon segment, no combat allowed)
3. Faction identity strings: 3–4 paragraph faction description for UI display.

**Content format:** JSON files in `content/factions/`. Vendor items reference existing item IDs.

**Acceptance criteria:**
- Both factions appear in faction contact UI in town.
- Bureau vendor sells 6 distinct items at 25+ rep.
- Completing a Bureau side mission reduces Convocation rep by defined amount.

**Depends on:** Task 38

---

### 40. Reputation-gated vendor
**Layer:** Client + Engine  
**Owner:** Both leads

**Subtasks:**
1. Engine: `getAvailableVendors(town, reputationState)` filters faction vendors by threshold.
2. Engine: vendor stock is static per campaign (no randomization yet).
3. Client: market UI shows faction vendor as a separate tab or NPC portrait.
4. Client: vendor stock displays rep requirement; locked items show "Requires 25 Bureau reputation".

**Acceptance criteria:**
- At 24 Bureau rep, Bureau vendor tab is invisible or greyed out.
- At 25 Bureau rep, Bureau vendor tab appears with full stock.
- Purchasing from vendor reduces gold; item enters party inventory.

**Depends on:** Tasks 38, 39

---

### 41. Faction contacts in town
**Layer:** Client  
**Owner:** Frontend lead

**Subtasks:**
1. NPC dialogue panel: portrait, name, faction badge, current rep indicator.
2. Dialogue options: greeting (always), mission offer (if missions available), rumor (if rep ≥ 10), special service (if rep meets threshold).
3. Reputation-gated dialogue: low rep options are hostile or dismissive; high rep options are friendly and revealing.
4. Side mission acceptance: clicking "Accept" sends `mission.accept` message; mission appears in quest log.

**Acceptance criteria:**
- Contact with 0 rep only shows greeting and 1 dismissive line.
- Contact with 30 rep shows greeting, 2 mission offers, and 1 friendly rumor.
- Accepting a mission updates quest log and reputation on completion.

**Depends on:** Tasks 38, 39

---

### 42. Reputation consequences
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Mission completion handler: applies primary faction delta + opposed faction penalty.
2. Dialogue choice handler: some dialogue options cost reputation (e.g., insulting a Bureau contact → -5 Bureau).
3. Consequence propagation: if Convocation drops below -25, Convocation contacts refuse all interaction.
4. UI notification: "Convocation reputation decreased" popup on relevant actions.

**Acceptance criteria:**
- Completing Bureau mission A reduces Convocation rep by exactly the defined amount.
- Player can see both faction rep bars shift after mission completion.
- At -25 Convocation, Convocation vendor disappears from market.

**Depends on:** Tasks 38, 39, 40

---

## Group 8: Synergy Spark

### 43. Synergy detection engine
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. `SynergyRegistry` static lookup: `Map<[AbilityId, AbilityId], SynergyEffect>`. Order-independent.
2. Per-round state: `Set<AbilityId> abilitiesUsedThisRound`.
3. On ability resolution: check if any registered synergy pair includes this ability and an already-used ability. If yes, append synergy effect to resolution.
4. Synergy effects are treated as bonus damage, status application, or action modification — resolved immediately after the triggering ability.

**Acceptance criteria:**
- Using Ability A then Ability B in the same round triggers the synergy on B's resolution.
- Using Ability B then Ability A also triggers it.
- Using the same ability twice does NOT trigger a synergy.
- Snapshot test: combat with synergy pair produces different total damage than without.

**Depends on:** Phase 1 Group 4 (combat state machine)

---

### 44. 5 synergies
**Layer:** Content  
**Owner:** Content author

**Deliverables:**
1. One synergy per plausible class pair from the 6 available classes:
   - Bonewarden + Stillblade: Anti-synergy (documented, no positive synergy — teaches composition lesson)
   - Bonewarden + Cauterist: "Bone Link + Pyre" (fire spreads through linked targets)
   - Stillblade + Hollow: "Backstep + Cheap Shot" (guaranteed crit)
   - Fieldwright + Inkblood: "Overcharge + Knowledge Bolt" (empowered ranged attack)
   - Cauterist + Hollow: "Purify + Cheap Shot" (healing debuff + damage combo)
2. Each synergy needs: ability A ID, ability B ID, effect definition (damage mod, status, etc.), hint text for Field Notes.

**Content format:** JSON in `content/synergies/`. Referenced by ability IDs.

**Acceptance criteria:**
- All 5 synergies trigger correctly in combat snapshot tests.
- Field Notes auto-populates with hint text on first trigger.

**Depends on:** Tasks 35 (new classes), 43

---

### 45. Synergy feedback (3-tier)
**Layer:** Client  
**Owner:** Frontend lead

**Subtasks:**
1. **Tier 1 (Immediate):** Flash effect on affected targets + distinct sound cue (placeholder beep in Phase 1.5). Duration: 500ms.
2. **Tier 2 (Field Notes):** Auto-append entry to Field Notes journal with cryptic hint. Entry includes: ability names (hidden as "???" until triggered), hint text, mechanical effect description.
3. **Tier 3 (Replay):** Field Notes entry has "Replay" button that re-renders the synergy animation in a modal combat viewer.

**Acceptance criteria:**
- Synergy trigger is visually distinct from normal ability resolution.
- Field Notes entry appears immediately after combat ends (not during, to avoid UI clutter).
- Replay button renders the synergy animation without requiring the actual combat state.

**Depends on:** Tasks 43, 44

---

### 46. Field Notes journal
**Layer:** Client  
**Owner:** Frontend lead

**Subtasks:**
1. Svelte panel: list view of discovered synergies, sorted by discovery order.
2. Each entry: ability icons (revealed or "???"), hint text, mechanical effect, replay button.
3. Undiscovered synergies: show as locked entries with question marks (teases total count).
4. Accessible from town and dungeon (pause menu).

**Acceptance criteria:**
- Panel opens via J key or UI button.
- Discovered synergies show full details; undiscovered show only "???".
- Replay button works for all discovered entries.

**Depends on:** Tasks 45

---

## Group 9: Minimal Overworld

### 47. Two-node overworld
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. `OverworldState` struct: `nodes: Node[]`, `routes: Route[]`, `currentNode: NodeId`.
2. `Node` definition: id, name, type (town | dungeon_entrance), connected routes.
3. `Route` definition: id, source, destination, distance (turn cost), dangerRating (1–5).
4. Travel action: `travel(routeId)` validates current node == route.source, deducts turns, applies route cost.
5. Town arrival: trigger town UI; dungeon arrival: trigger dungeon load.

**Acceptance criteria:**
- Player at Town A can click route to Town B; turn counter increments by route distance.
- Player cannot travel from Town B to Town A if not at Town B.
- Save/load preserves current node and turn counter.

**Depends on:** Nothing (new system)

---

### 48. Overworld map UI
**Layer:** Client  
**Owner:** Frontend lead

**Subtasks:**
1. Svelte node graph: two nodes connected by a line.
2. Node visuals: town icon (building) or dungeon icon (cave). Current node highlighted.
3. Route line: color-coded by danger (green/yellow/red). Hover shows tooltip: distance, danger, terrain.
4. Click destination → confirmation modal → travel.

**Acceptance criteria:**
- Map fits in a modal panel (not fullscreen yet — fullscreen map is Phase 2).
- Clicking a destination shows travel cost before confirming.
- Map updates current node after travel.

**Depends on:** Task 47

---

### 49. Travel encounters
**Layer:** Engine + Content  
**Owner:** Backend lead + Content author

**Subtasks:**
1. `TravelEncounterTable` schema: list of encounters with weight, danger threshold, and resolution type.
2. Encounter resolution engine:
   - Combat resolution: mini-combat (2–3 enemies, simplified AI)
   - Stat test resolution: roll against party's highest relevant stat
   - Dialogue resolution: Ashmouth/Broker check for diplomatic options
3. One encounter table for the single route: 6 encounters (faction patrol, bloom pocket, merchant, refugees, ambush, environmental hazard).
4. Encounter frequency: 0–2 per trip. Roll per trip, not per segment (simplified for Phase 1.5).

**Acceptance criteria:**
- Traveling from Town A to Town B has 60% chance of 1 encounter, 20% chance of 2, 20% chance of 0.
- Each encounter type resolves correctly: combat loads mini-combat, stat test shows roll UI, dialogue shows options.
- Class alternatives work: Marcher prevents ambush surprise round; Ashmouth gets better merchant prices.

**Depends on:** Tasks 47, 38 (reputation affects patrol encounters)

---

### 50. Turn counter
**Layer:** Engine + Client  
**Owner:** Both leads

**Subtasks:**
1. `CampaignState.turn: int` — increments on travel, dungeon expedition, and downtime actions.
2. Turn cost rules:
   - Travel: route distance
   - Dungeon expedition: 2 turns (entry + exit)
   - Downtime: 0 turns (happens during town visit)
3. Turn limit: 15 turns. At turn 15, campaign ends and score screen displays.
4. UI: turn counter displayed in top-right of town and overworld screens. Color shifts at 10 turns (yellow) and 13 turns (red).

**Acceptance criteria:**
- Turn counter increments correctly after each travel and dungeon.
- At turn 15, player is forced to return to town and campaign ends.
- Save/load preserves exact turn count.

**Depends on:** Task 47

---

### 51. Second dungeon template: Bloom Site
**Layer:** Content + Client  
**Owner:** Content author + Frontend lead

**Subtasks:**
1. **Content author:**
   - 10–15 Bloom Site room segments (organic geometry, corridors, chambers, dead ends, one setpiece).
   - Room segment JSON follows design doc 07 format with `template: "bloom-site"`.
   - 2–3 new bloom creature enemy types: stats, abilities, AI behavior tags.
   - Encounter tables for Bloom Site: 4–6 encounters scaling with party level.
2. **Frontend lead:**
   - Bloom visual theme: green/purple organic color palette, pulsating geometry, particle effects.
   - Bloom creature models: low-poly mutated forms (reuse skeleton rigs with new materials).
   - Bloom ambient audio placeholder: low drone + squelch sounds.

**Acceptance criteria:**
- Bloom Site dungeon assembles and loads correctly.
- Bloom creatures use distinct AI (aggressive random + environmental hazard spawning).
- Visual theme is unmistakably different from Broken Engine.

**Depends on:** Phase 1 Group 2 (dungeon assembler, renderer)

---

## Group 9.5: Quality of Life & Spec Coverage

### 51a. Rebindable keys
**Layer:** Client + Engine
**Owner:** Frontend lead

**Subtasks:**
1. Settings UI panel: Input section with full rebinding table per design doc 10.
2. Click-to-capture key chord; conflict detection blocks duplicate bindings.
3. Persist to KDL on disk via host write; mirror to `localStorage` on client.
4. Reset-to-defaults button per action and global.
5. Validate at startup: missing bindings fall back to defaults, surface a one-shot warning toast.

**Acceptance criteria:**
- Player rebinds Move forward from W → T; reload preserves binding.
- Attempting to assign Space to both Interact and Move forward shows conflict error.
- KDL file is human-editable; manual edits load on next start.

**Depends on:** Phase 1 task 9a (settings v1)

---

### 51b. Breakable walls + Inkblood Cartographer secret detection
**Layer:** Engine + Client + Content
**Owner:** Backend lead + Content author

**Subtasks:**
1. Extend secret data model (design doc 12) with `type: "breakable_wall"`.
2. Breakable wall discovery: explicit search OR area-damage trigger during combat that hits a wall-adjacent tile.
3. Inkblood (Cartographer) passive: reveals all secrets within 2 tiles automatically at end of each movement.
4. Automap "?" markers for Cartographer-detected unrevealed secrets.
5. Add breakable wall renderer state: cracked-wall material when revealed, opens on explicit "break" action (consumes 1 dungeon turn).

**Acceptance criteria:**
- Inkblood Cartographer in party reveals secrets within 2 tiles automatically.
- Breakable wall identified by Inkblood appears as "?" on automap; explicit search reveals type; break action opens it.
- Area-of-effect combat ability hitting wall-adjacent tile damages and reveals breakable wall.

**Depends on:** Phase 1 task 25a (secret v1), Task 35 (Inkblood content)

---

### 51c. Bloom sample decay
**Layer:** Engine + Client
**Owner:** Backend lead

**Subtasks:**
1. Track `dungeonTurnsAlive` per Bloom Sample stack entry. Increments only while party is inside a dungeon node.
2. At threshold 10 dungeon turns, sample is destroyed and removed from inventory.
3. UI notification: "A bloom sample has decayed into inert matter."
4. Stable flag: samples flagged `stabilized: true` skip decay (set by Heretic `Tend Blooms` downtime — placeholder until downtime ships in Phase 2; for Phase 1.5 expose via test hook only).
5. Save/load preserves per-sample turn counters.

**Acceptance criteria:**
- Bloom sample picked up at dungeon turn 0 decays at dungeon turn 10.
- Travel and town turns do not advance decay.
- Stabilized samples persist indefinitely.

**Depends on:** Task 35 (Heretic/Inkblood content with bloom samples)

---

### 51d. Inkblood memory recovery
**Layer:** Engine + Client
**Owner:** Backend lead

**Subtasks:**
1. Per-character `memoryDebt: int` field on `CharacterState`.
2. Inkblood abilities with memory cost apply `memoryDebt += cost`; temporary stat penalty scales with debt.
3. Town arrival: memory debt cleared on next Rest action (Phase 2 downtime; for Phase 1.5 auto-clear on town entry).
4. Over-cast guard: ability submission blocked when `memoryDebt + cost > maxMemoryCapacity`; UI warning at ≥ 80%.
5. Memory penalties persist across combats within a dungeon.

**Acceptance criteria:**
- Inkblood casting two memory-cost abilities accumulates penalty without resetting per-combat.
- Entering town clears memory debt.
- Over-cast attempt is blocked with a clear UI message.

**Depends on:** Task 35 (Inkblood content)

---

### 51e. Action log expansion
**Layer:** Engine
**Owner:** Backend lead

**Subtasks:**
1. Add categories per design doc 11: `faction` (rep_changed, vendor_unlocked, mission_completed/failed), `overworld` (travel_started, travel_encounter_resolved, town_reached), and Phase 1.5 `dungeon` additions (secret_discovered for breakable walls, settlement_fate_chosen placeholder for Phase 2).
2. Emit events from existing systems: reputation deltas (task 38), travel resolutions (task 49), mission completion (task 42).
3. Save schema bump: action log size cap warning at 1000 events for dev tracking.

**Acceptance criteria:**
- Completing a Bureau side mission emits `mission_completed` + `rep_changed` (Bureau and Convocation) + `vendor_unlocked` (if threshold crossed).
- Travel from Town A to Town B emits `travel_started`, `travel_encounter_resolved` (per encounter), `town_reached`.

**Depends on:** Phase 1 task 31a, Tasks 38, 49

---

## Content Pipeline

### Authoring Workflow

All content is authored in JSON and validated at build time.

```
content/
├── factions/
│   ├── bureau.json
│   └── convocation.json
├── synergies/
│   └── phase-15/
│       ├── bonewarden-cauterist.json
│       ├── stillblade-hollow.json
│       ├── fieldwright-inkblood.json
│       ├── cauterist-hollow.json
│       └── anti-stillblade-bonewarden.json
├── encounters/
│   └── travel/
│       └── route-ashmark-to-bridge.json
└── segments/
    └── bloom-site/
        └── (10-15 segment files)
```

**Validation:** Content pack compiler (Phase 1 task 16) extended to validate:
- All ability IDs referenced by synergies exist in class definitions.
- All item IDs referenced by faction vendors exist in item library.
- All encounter IDs referenced by travel tables exist in encounter library.
- All segment connection points are valid (no dangling references).

### Content Schedule

| Week | Deliverable |
|---|---|
| 1 | Fieldwright + Inkblood class definitions, branch choice UI mockups |
| 2 | Bureau + Convocation faction profiles, side mission scripts |
| 3 | 5 synergies, synergy hint text, Field Notes entries |
| 4 | Travel encounter table, route data |
| 5 | Bloom Site room segments (10-15), bloom creature definitions |
| 6 | Integration pass: all content loads from pack, validation passes |

---

## Testing Strategy

| Level | Target | Tool | Count |
|---|---|---|---|
| Unit | Reputation delta clamping, threshold checks, turn counter increments | xUnit | 10 tests |
| Snapshot | Combat with 6 characters, row-dependent abilities, synergy triggers | xUnit | 10 tests |
| Integration | Content pipeline: all Phase 1.5 JSON → binary → loaded state | xUnit | 1 test per content type |
| UI Smoke | Formation drag-and-drop, branch choice modal, faction vendor unlock, travel + encounter | Playwright | 6 tests |
| Manual | Full 15-turn campaign: create party, do missions, travel, enter Bloom Site, discover synergy | Playtesting | 3 runs |

---

## Dependency Graph

```
Phase 1 (✅ complete)
  ├─► Group 5.5 (Retrofits, run FIRST)
  │     ├─► 32a (Action log) → unblocks 32d
  │     ├─► 32b (Envelope v2) → unblocks all new message types
  │     ├─► 32c (Strafe + cancel) → unblocks formation/journal UI
  │     ├─► 32d (Save v2, drop v1) → unblocks all save schema growth
  │     ├─► 32e (Tile-tagged encounters) → unblocks set-piece authoring
  │     ├─► 32f (Town → engine) → unblocks Group 7 (Factions)
  │     └─► 32g (JSON segments) → unblocks Bloom Site + all Phase 2 templates
  ├─► Group 6 (Formation)
  │     ├─► 33 (Party of 6)
  │     │     ├─► 34 (Row abilities)
  │     │     ├─► 35b (Branch engine)
  │     │     └─► 36 (Formation UI)
  │     ├─► 35 (Fieldwright + Inkblood content)
  │     │     └─► 35a (Branch choice UI)
  │     └─► 37 (Combat renderer for 6)
  │           └─► depends on 33, 36
  ├─► Group 7 (Factions)
  │     ├─► 38 (Reputation system)
  │     ├─► 39 (Bureau + Convocation content)
  │     ├─► 40 (Gated vendor)
  │     ├─► 41 (Faction contacts)
  │     └─► 42 (Rep consequences)
  │           └─► depends on 38, 39, 40
  └─► Group 9 (Overworld)
        ├─► 47 (Two-node overworld)
        ├─► 48 (Map UI)
        ├─► 49 (Travel encounters)
        ├─► 50 (Turn counter)
        └─► 51 (Bloom Site)
              └─► depends on Phase 1 dungeon assembler

Group 9.5 (QoL)
  ├─► 51a (Rebindable keys) depends on Phase 1 task 9a
  ├─► 51b (Breakable walls + Cartographer) depends on Phase 1 task 25a, Task 35
  ├─► 51c (Bloom decay) depends on Task 35
  ├─► 51d (Inkblood memory recovery) depends on Task 35
  └─► 51e (Action log expansion) depends on Phase 1 task 31a, Tasks 38, 49

Group 6 ──► Group 8 (Synergies) [needs 6 classes]
Group 7 ──► Group 9 (town contacts need rep system)
```

**Parallelization:**
- Group 6 (backend) and Group 7 (content-heavy) can start immediately after Phase 1.
- Group 9 (overworld) can start in parallel with Groups 6 and 7.
- Group 8 (synergies) must wait for Group 6 (new classes) but can have content drafted in parallel.
- Frontend work (36, 37, 40, 41, 45, 46, 48) can be batched and worked on by one engineer while backend handles engine work.

---

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Retrofit work blocks Phase 1.5 feature progress | High | High | Group 5.5 has 7 tasks; assign two engineers; prefer 32a/32b/32d in week 1, 32c/32e/32g in week 2. 32f waits on Group 7 design alignment. |
| Schema-break churn during dev | Low | Low | CC1 policy: break saves/protocol freely between phases. Dev-only saves are disposable; production migration is a Phase 3 concern when schema stabilizes. |
| Encounter rate formula (`0.05 + 0.08 * steps`) is now load-bearing for pacing | Low | Medium | Pin formula in design doc 06 appendix with named constants before content authors tune Phase 1.5 encounters. Two-mode (tagged + wandering) must keep this knob isolated to wandering. |
| 6-character combat feels chaotic | Medium | High | Early prototype task 37 in week 1; if unreadable, reduce enemy group size or add UI grouping |
| Branch choice UI is confusing | Low | Medium | User test with 3 non-gamers in week 2; iterate modal copy |
| Faction reputation math feels arbitrary | Medium | High | Expose rep deltas in UI ("Bureau +5, Convocation -2") so players understand cause/effect |
| Bloom Site visual theme too similar to Broken Engine | Low | Medium | Art pass in week 5; if insufficient, add post-processing color grading |
| Travel encounters feel like interruptions | Medium | Medium | Keep encounter resolution under 2 minutes; allow "auto-resolve" for trivial encounters |

---

## Milestones

| Week | Milestone | Definition of Done |
|---|---|---|
| 1 | Retrofits land | Group 5.5 tasks 32a/32b/32d shipped; envelope v2 in production; v1 saves removed (no migration) |
| 2 | Retrofits complete | 32c/32e/32f/32g shipped; JSON segments authoritative; town server-side |
| 3 | Formation works | 3+3 combat is playable, row abilities function, branch choices are made |
| 5 | Factions feel real | Bureau/Convocation vendors unlock, side missions complete, rep consequences visible |
| 6 | Synergies discovered | All 5 synergies trigger in playtesting, Field Notes captures them |
| 7 | Phase 1.5 complete | Full 15-turn campaign playable from start to finish, all validation tests pass |

---

## Appendix A — WebSocket Protocol Delta (Phase 1.5)

Adds to Phase 1 Appendix A. Protocol version bumps to `v: 2`. Phase 1 messages still valid.

**Client → server (new):**
| Type | Payload | When |
|---|---|---|
| `formation.set` | `{ front: [charId×3], back: [charId×3] }` | Drag-drop in formation UI |
| `branch.choose` | `{ charId, branchId }` | Branch modal accept |
| `mission.accept` | `{ missionId, factionId }` | Faction contact dialogue |
| `vendor.purchase` | `{ vendorId, itemId, qty }` | Market buy |
| `overworld.travel` | `{ routeId }` | Map click confirm |
| `encounter.resolve` | `{ encounterId, choice: "fight\|stat_test\|dialogue\|class_alt", payload? }` | Travel encounter UI choice |
| `journal.replay` | `{ synergyId }` | Field Notes replay button |
| `keybinding.set` | `{ action, key, modifiers? }` | Settings rebind |

**Server → client (new):**
| Type | Payload | When |
|---|---|---|
| `state.formation` | `{ front, back }` | After `formation.set` |
| `state.reputation` | `{ factionId, oldValue, newValue, source }` | Any rep delta |
| `state.evidence` | `{ factionId, count }` | Evidence increment (Phase 1.5 stub, full system Phase 2) |
| `state.faction.vendor_unlocked` | `{ factionId }` | Threshold cross |
| `journal.synergy_discovered` | `{ synergyId, abilityIds, hintText }` | First synergy trigger |
| `overworld.encounter` | `{ encounterId, options }` | Travel encounter raised |
| `overworld.arrived` | `{ nodeId, kind }` | Travel complete |
| `campaign.turn_changed` | `{ turn, max, urgency: "stable\|developing\|urgent\|critical" }` | Turn increment |

**Sequencing additions:**
- `formation.set` is rejected mid-combat with `error.formation_locked`.
- `branch.choose` blocks town-exit `overworld.travel` until resolved.

---

## Appendix B — Synergy Schema

```json
{
  "id": "syn-bonewarden-cauterist-bone-link-pyre",
  "abilities": ["bonewarden.bone-link", "cauterist.pyre"],
  "anti": false,
  "effect": {
    "type": "damage_propagation",
    "modifier": { "damageMultiplier": 1.0, "targets": "linked" },
    "appliesAfter": "second_ability"
  },
  "hint": "Fire follows the bone tether.",
  "fieldNotes": {
    "title": "Pyre Through Bone",
    "body": "When the Cauterist's pyre touches a bone-linked target, every linked ally takes scaled fire damage. Useful against tight enemy clusters.",
    "discoveredBy": null
  },
  "tags": ["combo", "fire", "necromantic"]
}
```

- `abilities` order-independent (per C5). Duplicate ability IDs forbidden.
- `effect.appliesAfter`: `first_ability | second_ability | round_end`.
- `anti: true` marks negative synergies (e.g., Bonewarden + Stillblade — content team writes hint as warning).
- `fieldNotes.discoveredBy` is null until first trigger; persisted in save.

**Registry build:** Content pack compiler emits a flat `Map<sortedPair, Synergy>` for O(1) lookup at combat resolution (per C5).

---

## Appendix C — Reputation Delta Table (resolves C6)

| Source | Primary Faction | Opposed Faction(s) | Notes |
|---|---|---|---|
| Main mission complete | +8 | -4 | Headline campaign mission |
| Side mission complete | +5 | -2 | Faction contact offered mission |
| Mission failed / abandoned | -3 | +1 | Mid-mission retreat counts |
| Faction-aligned dialogue choice | +2 | -1 | Per dialogue node tag |
| Faction-opposed dialogue choice | varies | -3 to -8 | Insults, threats |
| Ashmouth Broker network (downtime, Phase 2) | +5 | 0 | No opposed penalty |
| Help refugees (travel encounter) | +2 | 0 | Applied to nearby factions |
| Kill faction soldier (combat) | varies | -1 | Per soldier killed in faction territory |

**Clamps:** −100..+100. Deltas log with `source` for debugging and player-facing UI (toast: "Bureau +5 (side mission), Convocation −2").

**Vendor thresholds:** unlock at +25. Hostile lockout at −25. Exclusive recruit at +50. Patron benefit at +75. Allied at +90.

---

## Appendix D — Action Log Categories (Phase 1.5)

Adds to Phase 1 log. Schema unchanged.

| Category | New types |
|---|---|
| `faction` | `rep_changed`, `vendor_unlocked`, `mission_completed`, `mission_failed` |
| `overworld` | `travel_started`, `travel_encounter_resolved`, `route_blocked`, `town_reached` |
| `dungeon` | `secret_discovered` (extended for breakable walls) |
| `combat` | `synergy_triggered` (was placeholder in Phase 1, now active) |

**Examples:**
```json
{ "turn": 4, "act": 1, "category": "faction", "type": "rep_changed",
  "payload": { "factionId": "bureau", "delta": 5, "newValue": 25, "source": "mission:engine-inspection" } }
{ "turn": 4, "act": 1, "category": "faction", "type": "vendor_unlocked",
  "payload": { "factionId": "bureau", "threshold": 25 } }
{ "turn": 7, "act": 1, "category": "combat", "type": "synergy_triggered",
  "payload": { "synergyId": "syn-bonewarden-cauterist-bone-link-pyre", "encounterId": "bloom-pocket-2" } }
```

---

## Appendix Y — Phase 1 As-Built Reference

Snapshot of what shipped in Phase 1. Use this as the source of truth for what Group 5.5 retrofits against. Diverges from the Phase 1 design-intent appendices originally drafted.

### Y.1 WebSocket Protocol (as-built)

Flat envelope, no `v` or `seq`:
```json
{ "type": "<action>", "...action-specific fields..." }
```

**Client → server (shipped):**
| Type | Extra fields | Notes |
|---|---|---|
| `move_forward` | — | No `move_back`, no strafe |
| `turn_left` | — | |
| `turn_right` | — | |
| `combat_action` | `action: { ... }` | Free-form action payload |
| `flee_combat` | — | |
| `enter_combat` | — | Manual trigger (test/debug path) |
| `enter_dungeon` | `dungeonType: string` | "broken_engine" / "crypt" / "sewers" |
| `rest` | — | Inn rest (full HP heal) |
| `return_to_town` | — | |
| `save_game` | — | Manual save |
| `reset_game` | — | New game / wipe |
| `swap_row` | `slot: int` | Built-in Phase 1, originally planned Phase 1.5 |

**Server → client (shipped):** broadcasts full game state JSON; no message-type discrimination beyond presence of fields.

**Gaps vs design:** no `hello`/`ready`, no heartbeat, no `seq` ack, no `error` envelope (errors `Console.WriteLine`d server-side). Task 32b closes these.

### Y.2 Save Schema (as-built, version `"1"`)

```json
{
  "version": "1",
  "party": [
    {
      "id": "guid",
      "name": "string",
      "classId": "string",
      "level": "int",
      "xp": "int",
      "baseStats": { ... },
      "currentHp": "int",
      "equipment": { ... },
      "knownAbilities": ["string"],
      "row": "int (0=front, 1=back)"
    }
  ],
  "player": { "x": "int", "y": "int", "facing": "string" },
  "currentDungeonType": "string | null",
  "exploredTiles": ["string (x:y)"],
  "mode": "string (GameMode enum name)"
}
```

- `Version` is a string field (not int). Task 32d replaces with int `schemaVersion = 2` and drops v1 reader.
- Save path: `%LocalAppData%/TheReach/save.json`.
- No action log, no formation array, no reputation map, no settings ref.
- Hard reject on version mismatch (`Console.Error` log, no migration).

### Y.3 Dungeon + Encounter (as-built)

- Dungeons assembled from C#-hardcoded segments (`CreateEntranceRoom`, `CreateCorridor`, `CreateChamber`, `CreateDeadEnd`).
- One encounter table per dungeon type (`Dungeon.EncounterTableId`).
- Encounter trigger: probabilistic per step using `encounterChance = 0.05 + 0.08 * stepsSinceEncounter`, evaluated after every successful move.
- `_stepsSinceEncounter` resets on encounter, dungeon enter, dungeon exit.
- No tile-tagged authored encounters.

### Y.4 Game State (as-built)

`GameState` is the single root holding `Player`, `Party`, `CurrentDungeon`, `Mode (GameMode enum)`, `Combat`, `LastCombatResult`, `ExploredTiles (BoundedTileSet, cap 4096)`, `CurrentDungeonType`, `_stepsSinceEncounter`, `_encounterRng`.

- `Player` is a separate entity from `Party` — Player owns `Position + Facing`, Party owns members. Plan implied Party owns position; refactor deferred to Phase 2 if it hurts.
- `Mode` enum: `Menu`, `Exploration`, `Combat` (others may exist).
- RNG is seedable via `GameState(seed: int)`; default seeds from `DateTime.UtcNow.GetHashCode()`. Combat snapshot tests use explicit seed.

### Y.5 Content Pipeline (as-built)

- `EncounterTableRegistry`, `ClassRegistry`, `ItemRegistry` load JSON from disk at server boot via `FindContentDir(...)` (walks up 0–8 directory levels to find `content/`).
- No `.rpk` binary pack in runtime path (compiler exists per T16 — `feat(tools): T16 content pack compiler + RPK reader` — but server reads raw JSON). Binary pack used for distribution prep only.
- Hot reload not implemented; restart required for content changes.

---

## Appendix Z — Build Learnings (Phase 1 → improve Phase 1.5+ planning)

Extracted from the 35 build commits on `build/kimi`. Each row is a real surprise — concrete enough that Phase 1.5+ planning should account for it.

### Z.1 Security / robustness fixes already shipped

| Commit | Lesson |
|---|---|
| `fix(security): WS frame accumulation, save clamping, version check, gitignore` | Multi-fragment WebSocket frames must be accumulated until `EndOfMessage`; one-shot reads drop data. Save inputs need bounds (`clamp`) at deserialization, not just at use site. Version mismatch must reject; absent check = silent corruption. |
| `fix(perf): cap ExploredTiles, fix texture disposal, exponential reconnect backoff` | Explored-tile set grew unbounded → memory leak; cap 4096 with FIFO eviction (`BoundedTileSet`). Three.js geometry/material/texture must be `.dispose()`d on dungeon teardown; GC alone leaks GPU. WebSocket reconnect spam → exponential backoff 250ms → 4s cap. |
| `fix(quality): remove debug logs, drop generate_dungeon, delete Class1, NaN guard in TownMenu` | Empty party division → NaN in price/avg calculations. Dead `generate_dungeon` action shipped before being removed. `Class1.cs` dotnet boilerplate snuck in. |
| `chore(nits): remove debug logs, rename sendRadius, drop dead fetchContent` | `console.log` in hot path. `sendRadius` (legacy name) confused for network function. Dead `fetchContent` left after pivot. |
| `build: downgrade from net10.0 to net9.0 for SDK compatibility` | net10.0 not available on most CI/dev SDKs at start of build (May 2026). Pin .NET version explicitly in `global.json` to avoid CI/local drift. |

**Phase 1.5+ planning rules:**
- Every new WebSocket message handler MUST accumulate frames before `JsonSerializer.Deserialize`.
- Every new save schema field MUST have a clamp at deserialization (sane bounds, not just nullable).
- Every new Three.js managed asset MUST be tracked for `.dispose()` on scene change. Add lint rule if possible.
- Every new collection that grows with gameplay MUST have a cap + eviction policy.
- Every new derived value (price avg, hit %, etc.) MUST guard divide-by-zero before computing.

### Z.2 Architectural divergences worth keeping

| Divergence | Why it's fine | Phase 1.5+ implication |
|---|---|---|
| `Player` separate from `Party` (position + facing isolated from members) | Allows party-of-N changes without touching navigation code | Phase 1.5 expansion to 6 members touches `Party` only |
| Probabilistic encounter triggers (vs tile-tagged spec) | Wandering encounters work for Broken Engine corridors | Tile-tagged path (task 32e) layered on top; both modes coexist |
| Hardcoded segment C# methods (vs JSON loader) | Shipped Phase 1 faster | Task 32g converts to JSON; Bloom Site (task 51) requires JSON path |
| Content loaded as raw JSON at boot (vs .rpk binary) | Faster dev iteration, smaller dev loop | Binary pack stays a release-build optimization; never required at runtime |
| Town as `GameMode.Menu` (vs server-authoritative state) | Phase 1 town is minimal — recruit/buy/save | Task 32f moves town to server before faction contacts land (taken: NaN bug already showed up) |

### Z.3 Architectural divergences worth fixing (already enumerated as Group 5.5)

- Flat WebSocket envelope → structured envelope with seq/error/heartbeat (task 32b).
- 3-direction input → 6-direction + cancel (task 32c).
- Save Version as string "1" with hard reject → int `schemaVersion = 2`, v1 reader deleted (task 32d).
- No action log → required by design doc 11 (task 32a).

### Z.4 Hidden gameplay knobs surfaced by build

Phase 1.5+ balance work needs to know these exist:

| Knob | Location | Default value | Tuning impact |
|---|---|---|---|
| Wandering encounter rate | `GameState.cs:127` | `0.05 + 0.08 * stepsSinceEncounter` | Drives dungeon pacing; too high = grindy, too low = empty corridors |
| Explored tile cap | `GameState.cs:71` | 4096 | Caps automap memory; ~64×64 dungeon limit before eviction |
| WebSocket reconnect backoff | client `net/GameClient.ts` | 250ms → 4s exponential | Affects perceived crash recovery time |
| Class color palette | `GameServer.cs:128` | hex per class | UI cohesion only; safe to override per skin |
| Encounter chance reset events | `GameState.cs` | on encounter, dungeon enter, dungeon exit | Plus task 32e: also on tile-tagged encounter resolved |

Surface these in design doc 06 as named balance constants before Phase 1.5 content authoring starts.

### Z.5 Testing surface to grow

Phase 1 e2e suite covers 18 tests across G1–G5 (Playwright). Coverage gaps Kimi should fill in Phase 1.5:

- No protocol-version negotiation test (introduced by task 32b).
- No "v1 save deleted and new game starts" test (task 32d behavior).
- No content hot-reload test (planned by task 32g; ship together).
- No multi-client reconnect test (single-client guard mentioned in plan but not verified end-to-end).
- No "kill -9 mid-save" durability test (covers task 32d atomic write).

Add these as Playwright + xUnit pairs alongside the Phase 1.5 feature work, not as an afterthought milestone.

### Z.6 Dartboard sync

`docs/dartboard/plan.md` already tracks G1–G5 ✅. Phase 1.5 must add Group 5.5 retrofits to the dartboard before standard Phase 1.5 tasks, with the same priority tier as Phase 1.5 Group 6 (Formation). Suggested ordering for Ralph Wiggum loop:

1. Group 5.5 (retrofits) — 32a → 32d → 32b → 32c → 32e → 32g → 32f (32a/d unblock log + schema; 32b/c unblock new UI flows; 32e/g unblock content scaling; 32f gates Group 7 town work).
2. Group 6 (Formation) in parallel with 32a/d once they stabilize.
3. Group 7 (Factions) blocked on 32f.
4. Group 8 (Synergies) blocked on Group 6.
5. Group 9 (Overworld) parallel with everything after 32b.

---

## Appendix AA — Dart Import Manifest

Drop-in metadata for `dart-query.create_task`. Each row maps to one Dart task. Dartboard `Personal/rpg` assumed; rename if user picks a different board. Size scale: XS (≤2h), S (½d), M (1d), L (2–3d), XL (1wk).

Priority: `critical` (blocks others / required for phase exit), `high` (in critical path), `medium` (parallelizable), `low` (polish).

### Group 5.5 — Phase 1 Retrofits

| Title | Dartboard | Priority | Size | Depends on | Notes |
|---|---|---|---|---|---|
| `[1.5] 32a Action log infrastructure` | Personal/rpg | critical | M | 32d | Engine — log emit points wired into shipped systems |
| `[1.5] 32b WS protocol envelope v2` | Personal/rpg | critical | L | — | Engine + Client — replace flat, no shim |
| `[1.5] 32c Strafe + cancel input` | Personal/rpg | high | S | 32b | Engine + Client — 6-direction + Esc, buffer cap 2 |
| `[1.5] 32d Save schema v2 (replace v1)` | Personal/rpg | critical | M | 32a | Engine — detect-and-delete v1, atomic write |
| `[1.5] 32e Tile-tagged encounter system` | Personal/rpg | high | M | — | Engine — layered over probabilistic wandering |
| `[1.5] 32f Hub town server-authoritative` | Personal/rpg | critical | L | 32b | Engine + Client — empty stubs for Group 7 |
| `[1.5] 32g JSON segment loader` | Personal/rpg | high | M | — | Engine + Content — replaces hardcoded `Create*Room` |

### Group 6 — Formation

| Title | Dartboard | Priority | Size | Depends on | Notes |
|---|---|---|---|---|---|
| `[1.5] T33 Expand party to 6` | Personal/rpg | high | M | 32d | Save schema 6 slots |
| `[1.5] T34 Row-dependent abilities` | Personal/rpg | high | S | T33 | Engine — `requiredRow` field |
| `[1.5] T35 Fieldwright + Inkblood content` | Personal/rpg | high | L | 32g | Content — 12 abilities, 2 classes |
| `[1.5] T35a Branch choice UI` | Personal/rpg | medium | S | T35b | Client — town-level modal |
| `[1.5] T35b Branch system engine` | Personal/rpg | high | M | T33 | Engine — level-3 choice gate |
| `[1.5] T36 Formation UI` | Personal/rpg | high | M | T33, T34 | Client — drag-drop 3+3 |
| `[1.5] T37 Combat renderer for 6` | Personal/rpg | high | M | T33, T36 | Client — 3 per band, initiative bar 12 slots |

### Group 7 — Factions

| Title | Dartboard | Priority | Size | Depends on | Notes |
|---|---|---|---|---|---|
| `[1.5] T38 Reputation system` | Personal/rpg | critical | S | 32f | Engine — `ReputationState`, opposed propagation |
| `[1.5] T39 Bureau + Convocation content` | Personal/rpg | high | L | T38 | Content — vendors, thresholds, 2 missions each |
| `[1.5] T40 Reputation-gated vendor` | Personal/rpg | high | M | T38, T39 | Engine + Client — threshold filter |
| `[1.5] T41 Faction contacts in town` | Personal/rpg | high | M | T38, T39 | Client — NPC dialogue panel |
| `[1.5] T42 Reputation consequences` | Personal/rpg | high | M | T38, T39, T40 | Engine — opposed deltas, contact lockout |

### Group 8 — Synergy Spark

| Title | Dartboard | Priority | Size | Depends on | Notes |
|---|---|---|---|---|---|
| `[1.5] T43 Synergy detection engine` | Personal/rpg | high | M | — | Engine — `SynergyRegistry`, set lookup |
| `[1.5] T44 5 synergies content` | Personal/rpg | high | S | T35, T43 | Content — JSON registry, hint text |
| `[1.5] T45 Synergy feedback (3-tier)` | Personal/rpg | high | M | T43, T44 | Client — flash, journal entry, replay |
| `[1.5] T46 Field Notes journal` | Personal/rpg | medium | M | T45 | Client — Svelte panel + replay |

### Group 9 — Minimal Overworld

| Title | Dartboard | Priority | Size | Depends on | Notes |
|---|---|---|---|---|---|
| `[1.5] T47 Two-node overworld` | Personal/rpg | high | M | 32d | Engine — `OverworldState`, travel |
| `[1.5] T48 Overworld map UI` | Personal/rpg | high | S | T47 | Client — Svelte node graph |
| `[1.5] T49 Travel encounters` | Personal/rpg | high | M | T47, T38 | Engine + Content — 6 encounters |
| `[1.5] T50 Turn counter` | Personal/rpg | high | S | T47 | Engine + Client — 15-turn cap |
| `[1.5] T51 Second dungeon: Bloom Site` | Personal/rpg | high | XL | 32g | Content + Client — 10–15 segments, theme |

### Group 9.5 — Quality of Life

| Title | Dartboard | Priority | Size | Depends on | Notes |
|---|---|---|---|---|---|
| `[1.5] T51a Rebindable keys` | Personal/rpg | medium | S | 32c | Client — KDL persistence, conflict detect |
| `[1.5] T51b Breakable walls + Cartographer detection` | Personal/rpg | medium | M | 32g, T35 | Engine + Client + Content |
| `[1.5] T51c Bloom sample decay` | Personal/rpg | medium | S | T35 | Engine + Client — 10 dungeon turns |
| `[1.5] T51d Inkblood memory recovery` | Personal/rpg | medium | S | T35 | Engine + Client |
| `[1.5] T51e Action log expansion (Phase 1.5 cats)` | Personal/rpg | medium | S | 32a, T38, T49 | Engine — faction + overworld categories |

### Dart payload pattern

For each row above, call:
```
mcp__plugin_slop-mcp_slop-mcp__execute_tool(
  mcp_name="dart-query",
  tool_name="create_task",
  arguments={
    "title": "<from table>",
    "dartboard": "Personal/rpg",
    "priority": "<from table>",
    "size": "<from table>",
    "description": "<copy task body from plan; ~10-30 lines>",
    "tags": ["phase-1.5", "<group-id>"]
  }
)
```

Group-id tags: `g5.5-retrofit`, `g6-formation`, `g7-factions`, `g8-synergies`, `g9-overworld`, `g9.5-qol`. These let the Ralph Wiggum loop filter by group.

### Suggested batch ordering for the loop

Week 1 (start the loop here):
1. 32b, 32a, 32d (foundational; assign in parallel where possible).
2. 32c, 32e, 32g (start once 32b lands).
3. 32f (after 32b).

Week 2 (overlaps end of retrofit week):
- T33, T35b (Group 6 can begin once 32d ships).
- T38 (Group 7 starts once 32f ships; T39 content authoring can begin in parallel).
- T43, T47 (Groups 8 and 9 independent of 32f).

Weeks 3–5:
- T35, T34, T36, T37 (Formation).
- T39–T42 (Factions).
- T44–T46 (Synergies).
- T48–T51 (Overworld).

Weeks 6–7:
- Group 9.5 QoL passes.
- Integration playthrough.
- Playwright suite refresh.
- Phase 1.5 exit-criteria check.

---

## Ready-to-execute checklist

- [x] Phase 1 build verified ✅ via dartboard.
- [x] Phase 1.5 retrofit tasks specified (32a–32g) with acceptance + test deliverables.
- [x] Group 6–9 + 9.5 task specs in place.
- [x] No back-compat shims or migration paths (CC1 policy).
- [x] Build learnings captured in Appendix Z.
- [x] As-built reference snapshot captured in Appendix Y.
- [x] Dart import manifest ready (Appendix AA).
- [x] Entry/exit criteria stated.
- [x] Suggested loop ordering documented.

**Status: ready to import to Dart and start the Ralph Wiggum loop.** Begin with `[1.5] 32b WS protocol envelope v2` and `[1.5] 32a Action log infrastructure` in parallel.
