# Complications

Spec-to-implementation conflicts, ambiguities that block task estimation, and decisions that should be made before writing code.

## Must Resolve Before Phase 1

### C1: WebSocket Protocol Design

**Problem:** The spec describes game state flowing from server to client, but doesn't define the protocol. Every task in Group 2+ depends on what messages look like.

**Decision needed:** What serialization format for WebSocket messages?

Options:
1. **JSON messages** — human-readable, easy to debug, larger payloads. Standard for Photino projects.
2. **MessagePack** — compact binary, fast serialization. Good .NET library. Overkill for Phase 1 volumes.
3. **JSON in dev, MessagePack in release** — best of both, but doubles the serialization surface.

**Recommendation:** JSON for all phases. The message frequency is low (player actions are turn-based, not real-time). The payload size is small (game state deltas, not full frames). Debugging visibility matters more than bandwidth. Revisit only if profiling shows serialization as a bottleneck.

**Also needed:** A message type enumeration. At minimum for Phase 1:
- `move` (client → server: direction)
- `state` (server → client: full or delta game state)
- `combat.action` (client → server: action + target)
- `combat.state` (server → client: combat state update)
- `error` (server → client: invalid action explanation)

---

### C2: Game State Ownership Boundary

**Problem:** The architecture says "no game logic runs client-side." But some things are awkward to round-trip: camera interpolation between grid positions, UI animations, hover tooltips. Where's the exact boundary?

**Decision needed:** What can the client own?

**Recommendation:** The client owns:
- **Rendering state** — camera position/interpolation, animation timers, particle effects
- **UI state** — which panel is open, hover/focus, scroll position
- **Input buffering** — keypress queue before sending to server

The client does NOT own:
- **Player position** — server authoritative, client interpolates between confirmed positions
- **Combat state** — all resolution server-side, client animates the results
- **Inventory** — server authoritative, client displays cached state
- **Any randomness** — all RNG on server

This means movement has a round-trip: keypress → server validates → server sends new position → client animates. At localhost latency (< 1ms) this is imperceptible. But the client should interpolate the camera smoothly between grid positions rather than snapping, which means the client needs to know the movement duration independent of the server response.

**Implementation note:** Define a fixed movement animation duration (e.g., 200ms). Client starts the animation on keypress (optimistic), server confirms or rejects. On rejection, client snaps back. At localhost latency, rejections arrive before the animation completes, so the snap-back is invisible.

---

### C3: Dungeon Assembly Output Format

**Problem:** The dungeon assembler (task 7) produces a dungeon. What does that look like as data? The renderer needs geometry. The game engine needs walkability, encounter triggers, interactable positions. The automap needs explored/unexplored state.

**Decision needed:** What's the dungeon state data structure?

**Recommendation:** A flat 2D grid (array of arrays) where each cell contains:
```
cell {
  floor: bool
  walls: { north: WallType, south: WallType, east: WallType, west: WallType }
  segment_id: string          // which authored segment this cell belongs to
  encounter_id: string | null // trigger combat on first entry
  interactable_id: string | null
  explored: bool              // automap state
  visible: bool               // currently visible (line of sight)
}

WallType = solid | open | door | locked_door | hidden | destructible
```

The renderer converts this to Three.js geometry. The engine operates on the grid. The automap reads `explored` flags. Clean separation — the grid is the single source of truth.

**Size concern:** A large dungeon might be 40x40 = 1,600 cells. Each cell is ~100 bytes. Total: ~160KB per dungeon. Trivial for memory. Trivial for WebSocket (send once on dungeon entry, then deltas for explored/visible changes).

---

### C4: Phase 1 Party Size vs. Formation

**Problem:** The spec says Phase 1 uses a party of 4 "reduced from 6 for simplicity." But the combat system is designed for 3+3 formation. A party of 4 means 2+2, which has different tactical properties: thinner front line, less protected back row, different action economy.

**Impact:** Phase 1 combat tuning (balance targets in doc 06) assumes 3+3. If Phase 1 uses 2+2, those numbers don't apply. Retuning for 2+2 is wasted work because Phase 1.5 switches to 3+3.

**Recommendation:** Keep Phase 1 at 4 characters but use 2+2 formation with the explicit understanding that balance tuning in Phase 1 is throwaway. Phase 1 validates feel (does moving and fighting feel good), not balance (are the numbers right). Balance tuning starts in Phase 1.5 with the real formation.

Alternative: Start with 6 characters and 3+3 in Phase 1. This is more work up front (6 character slots in UI, more complex encounter balancing) but means combat tuning carries forward. The spec's Phase 1.5 was added specifically to bridge this gap.

**Decision:** 4 characters in Phase 1, 2+2 formation, with explicit throwaway balance. Phase 1 validates feel and UI flow. Numerical tuning starts in Phase 1.5 with 3+3. This closes C4.

---

## Must Resolve Before Phase 1.5

### C5: Synergy Data Model

**Problem:** Synergies trigger when two abilities are used in the same round. The engine needs to detect this. But abilities are resolved one at a time in initiative order. The second ability needs to check "was my pair ability already used this round?"

**Implementation question:** Where does the "used this round" state live?

**Recommendation:** Each combat round maintains a `Set<AbilityId>` of abilities used so far. When an ability resolves, the synergy engine checks: does any registered synergy pair include this ability ID and an ID already in the set? If yes, trigger the synergy effect.

This is simple and O(1) per ability resolution (hash set lookup). The synergy registry is a static lookup table: `Map<[AbilityId, AbilityId], SynergyEffect>`. Order-independent (if A+B is a synergy, B+A also triggers it).

**Edge case:** What if the same ability is used twice in one round (two Cauterists both using Flashfire)? The spec doesn't address duplicate-ability synergies. Recommendation: synergies require two *different* abilities. Same-ability-twice is not a synergy trigger.

---

### C6: Reputation Math

**Problem:** Reputation is -100 to +100 (design doc 04). Faction vendors unlock at 25+. Bureau and Convocation are opposed in Phase 1.5. But the spec doesn't define reputation gain/loss rates.

**Impact:** If completing a Bureau mission gives +10 Bureau / -5 Convocation, the math is very different from +3 / -2. The rate determines how many missions it takes to unlock a vendor, how quickly you can recover from a bad faction choice, and whether reaching both faction vendors in one campaign is possible.

**Recommendation:** Define reputation deltas:

| Action | Primary Faction | Opposed Faction |
|---|---|---|
| Complete main mission | +8 | -4 |
| Complete side mission | +5 | -2 |
| Fail/abandon mission | -3 | +1 |
| Ashmouth (Broker) network | +5 | 0 |
| Faction-opposed dialogue choice | varies | -3 to -8 |
| Help refugees (neutral) | +2 to all nearby factions | — |

With these rates: vendor unlock (25) takes ~3-4 missions. Reaching both faction vendors requires deliberate balancing (~6 missions, mixed). Recovering from hostile (-25) takes ~5-8 missions of repair work.

**Decision:** No. A player should not be able to max both opposed factions in a single campaign. The reputation system is designed to create tension and force choices. Deliberate balancing (mixed missions, Broker networking) can maintain positive standing with both, but maxing both is impossible without extraordinary investment that costs progress elsewhere. This closes C6.

---

## Must Resolve Before Phase 2

### C7: Content Authoring Workflow

**Problem:** Phase 2 requires 60-80 room segments, 30-40 encounters, 50-60 items, 20-30 rumors, 15-20 evidence documents. That's a lot of JSON files to write by hand.

**Impact:** If authoring is slow, Phase 2 timelines blow out. Content volume is the biggest risk in the entire project.

**Options:**
1. **Raw JSON** — write each file by hand. Maximum control, maximum tedium.
2. **JSON + validation** — write by hand, but a build-time validator checks every file against the schema. Catches errors early.
3. **Spreadsheet → JSON pipeline** — author bulk content (items, encounters, stat blocks) in a spreadsheet, export to JSON. Hand-author narrative content (room segments, evidence).
4. **LLM-assisted authoring** — use an LLM to draft content from templates, human reviews and edits. Risky for quality but dramatically faster.

**Recommendation:** Option 2 for Phase 1 and 1.5 (small volumes). Option 3 for Phase 2 bulk content (items, encounters, stat blocks). Hand-author all room segments, evidence documents, and NPC dialogue regardless. A JSON schema validator should exist from Phase 1 task 16 (content pack compiler) — extend it to validate all content types.

---

### C8: Type Synchronization (C# ↔ TypeScript)

**Problem:** The .NET engine defines game state types (CharacterState, CombatState, DungeonGrid, etc). The TypeScript client needs matching types for deserialization. Manual maintenance works for Phase 1's small surface but becomes error-prone at Phase 2 scale.

**Options:**
1. **Manual** — keep both in sync by hand. Use snapshot tests to catch drift (serialize .NET → deserialize TS → assert match).
2. **Codegen from C#** — use NSwag, TypedocConverter, or a custom Roslyn-based tool to generate TypeScript types from C# models.
3. **Shared schema** — define types in JSON Schema, generate both C# and TypeScript from it.

**Recommendation:** Manual for Phase 1 with snapshot tests catching drift. At Phase 2 start, evaluate codegen from C# (the .NET models are the source of truth since the engine owns all state). Don't build codegen until manual sync actually causes a bug.

---

### C9: Stillblade Anti-Synergy in Combat Resolution

**Problem:** Stillblades can't benefit from allied necromantic buffs (design doc 05). The combat engine needs to know which abilities are "necromantic" and which targets are "Stillblade." This isn't a simple damage calc — it's a targeting filter that runs before buff application.

**Impact:** If implemented as a special case, it creates precedent for other class-specific targeting exceptions. If implemented as a tag system (abilities have tags, characters have immunities), it's more general but more complex.

**Recommendation:** Tag system from the start. Abilities have tags: `[necromantic, buff, fire, physical, bloom]`. Characters have immunities/resistances: `Stillblade: immune to [necromantic, buff]` (the intersection — immune to buffs that are necromantic, not immune to all buffs or all necromancy). This generalizes cleanly to:
- Bloom creatures: resistant to `[necromantic]`, weak to `[fire, purification]`
- Engine constructs: immune to `[necromantic, poison]`, weak to `[lightning, fieldwright]`
- The Unaccounted: resistant to everything except their specific counter tags

Build the tag system in Phase 1 even though only the Stillblade uses it. The cost is low (a set intersection check on ability application) and it prevents a rewrite in Phase 2 when bloom resistances and construct immunities arrive.

---

## Must Resolve Before Phase 3

### C10: LLM Provider and Fallback

**Problem:** Phase 3 depends on an LLM generating campaign configs. The spec says "under 30 seconds." Which LLM? What if the provider is down? What if output quality degrades across model versions?

**Options:**
1. **Anthropic Claude** — strong at structured output, good at following constraints. You're already in the ecosystem.
2. **Local model** — runs on the player's machine. No network dependency. Quality may be lower for complex constraint satisfaction.
3. **Cloud with local fallback** — try cloud LLM first, fall back to hand-authored configs if unavailable.

**Recommendation:** Design the generation pipeline as a pluggable interface: input (six rolls + content index) → output (campaign config JSON). The interface doesn't care which LLM sits behind it. Ship Phase 3 with Claude as the default, hand-authored configs as fallback, and the interface open for local models later.

**Also:** Pin a specific model version in the generation pipeline. LLM output quality can shift between versions. The validation layer catches structural failures, but subtle quality regressions (less coherent evidence chains, repetitive NPC casting) need human review after model updates.

---

### C11: Underway Segment Pool Isolation

**Problem:** The Underway "changes each traversal" by re-assembling from the same segment pool. But if the segment pool is shared with the Broken Engine or other templates (reusable corridor segments), changes to one template's pool affect Underway variation.

**Recommendation:** The Underway has its own segment pool, separate from other templates. Reusable corridor geometry can be duplicated (it's just JSON data, not 3D models — the renderer uses the same meshes regardless of segment ID). This keeps template variation independent.

---

### C12: Epilogue as the Only Runtime LLM Call

**Problem:** The spec says the LLM "does not write prose on the fly." But the campaign epilogue (Phase 3, task 108) generates summary text from the player's action log. This is runtime prose generation — the one exception to the rule.

**Impact:** If the LLM is unavailable at campaign end, the player gets no epilogue. This is the emotional payoff of a 10-15 hour campaign.

**Recommendation:** Two-tier epilogue:
1. **Template epilogue** (always available): pre-authored templates with variable slots. "The [Mastermind faction] [succeeded/failed] in their attempt to [Scheme]. [Town] was [saved/lost]. Your party [survived/suffered N losses]." Functional but flat.
2. **LLM epilogue** (if available): structured prompt with the action log, generates 2-3 paragraphs reflecting the specific campaign. Cached locally after generation.

The template epilogue exists from Phase 2 (no LLM dependency). The LLM epilogue is a Phase 3 enhancement. The player always gets a conclusion.

---

## Cross-Cutting Concerns

### CC1: Save File Versioning

Every phase changes the game state schema. Saves from Phase 1 won't deserialize in Phase 2. Options:
1. **Break saves between phases** — simple. Players expect this during development.
2. **Migration layer** — save files include a schema version. Migration functions upgrade old saves. More work, required for release.

**Recommendation:** Option 1 during development. Build the migration layer in Phase 3 when the schema stabilizes. Include a schema version field in saves from Phase 1 so the migration layer has something to key on.

### CC2: Content Hot-Reload for Authoring

Editing a JSON content file, rebuilding the binary pack, and restarting the game to see changes is painful for content iteration. A content hot-reload path (file watcher → rebuild pack → push to running game) would dramatically speed up Phase 2+ content authoring.

**Recommendation:** In development mode, skip the binary pack entirely. Load content from raw JSON. The binary pack is a release optimization. Add a file watcher that triggers a content reload over WebSocket when JSON files change. Phase 1 task 16 (content pack compiler) should be build-time only, not in the dev server's critical path.

### CC3: Deterministic RNG for Snapshot Tests

Combat snapshot tests need deterministic outcomes. If RNG is seeded per-combat, tests are reproducible. If RNG uses system randomness, tests are flaky.

**Recommendation:** The combat engine accepts an RNG seed as input. Production: seed from system entropy. Tests: seed from the test fixture. This makes every snapshot test deterministic without conditional logic in the engine.
