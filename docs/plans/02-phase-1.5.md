# Phase 1.5: Minimum Viable Strategy

**Goal:** Does the strategic layer change how players approach the core loop? Validates formation, faction tension, and composition consequences before the full content investment.

**Prerequisite:** Phase 1 complete. Dungeon navigation and combat feel good.

**Duration estimate:** 4–6 weeks (2 engineers + 1 content author)

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
5. Update save schema: migration from Phase 1 `PartyState` v1 → v2. Old saves load with 2 front + 2 back + 2 empty bench slots.

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
Phase 1 (complete)
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
| 6-character combat feels chaotic | Medium | High | Early prototype task 37 in week 1; if unreadable, reduce enemy group size or add UI grouping |
| Branch choice UI is confusing | Low | Medium | User test with 3 non-gamers in week 2; iterate modal copy |
| Faction reputation math feels arbitrary | Medium | High | Expose rep deltas in UI ("Bureau +5, Convocation -2") so players understand cause/effect |
| Bloom Site visual theme too similar to Broken Engine | Low | Medium | Art pass in week 5; if insufficient, add post-processing color grading |
| Travel encounters feel like interruptions | Medium | Medium | Keep encounter resolution under 2 minutes; allow "auto-resolve" for trivial encounters |

---

## Milestones

| Week | Milestone | Definition of Done |
|---|---|---|
| 2 | Formation works | 3+3 combat is playable, row abilities function, branch choices are made |
| 4 | Factions feel real | Bureau/Convocation vendors unlock, side missions complete, rep consequences visible |
| 5 | Synergies discovered | All 5 synergies trigger in playtesting, Field Notes captures them |
| 6 | Phase 1.5 complete | Full 15-turn campaign playable from start to finish, all validation tests pass |

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
