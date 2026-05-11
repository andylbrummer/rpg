# Phase 3: Full Vision

**Goal:** Does every run feel distinct? Does the LLM arrangement produce coherent narratives? Is there enough content depth for 5+ runs?

**Prerequisite:** Phase 2 complete. Strategic bind validated. Campaign configs feel different with hand-authored data.

**Duration estimate:** 8–10 weeks (2 engineers + 2 content authors + 1 QA + 1 LLM prompt engineer)

**Phase 3 is the differentiation layer.** It does not add new core systems; it adds content volume, procedural arrangement via LLM, faction AI, and the Unaccounted as a capstone enemy type. If Phase 2 is solid, Phase 3 is primarily content + integration work.

---

## Group 15: Full Content Library

### 85. Remaining dungeon templates
**Layer:** Content + Client  
**Owner:** Content authors (both) + Frontend lead

**Subtasks:**
1. **Boneyard** (new): 15–20 room segments.
   - Environment: bone-sorting halls, animation chambers, tithe archives.
   - Encounters: rogue tithe-constructs, Compact guardians, bureaucratic undead.
   - Setpiece: The Archive — searchable records revealing faction secrets.
   - Visual theme: off-white bone textures, sorting machinery, filing cabinets.
2. **Sealed Vault** (new): 15–20 room segments.
   - Environment: imperial wards, dead-language inscriptions, trap corridors.
   - Encounters: guardian constructs, ward traps.
   - Setpiece: The Vault — contents depend on campaign Scheme.
   - Visual theme: gold/bronze imperial architecture, glowing ward lines.
3. **Settlement Gone Wrong** (new): 15–20 room segments.
   - Environment: ruined town structures, bloom pockets, hostile survivors.
   - Encounters: occupying forces, bloom creatures in domestic settings.
   - Setpiece: Settlement fate choice — evacuate, reclaim, or sacrifice.
   - Visual theme: collapsed homes, makeshift barricades, civilian debris.
4. **Ossuary** (new): 15–20 room segments.
   - Environment: family vaults, memorial halls, private chambers.
   - Encounters: memory-ghosts, animated ancestors, Compact guardians.
   - Setpiece: Family secret — relevant to current campaign.
   - Visual theme: intimate scale, memorial plaques, bloodline markers.
5. Renderer updates: 4 new visual themes, lighting variations per template.

**Content volume:** 60–80 new room segments. Total library: 120–160 segments.

**Acceptance criteria:**
- All 8 templates load without errors.
- Each template has a distinct visual identity at a glance.
- Each template has at least 1 unique encounter type not found in other templates.

**Depends on:** Phase 2 task 81 (dungeon template pipeline)

---

### 86. All 6 Schemes
**Layer:** Content  
**Owner:** Content author (lead)

**Deliverables:**
1. Complete event chains for all 6 Schemes:
   - **Bloom Harvest:** 7 events, finale in bloom-consumed settlement.
   - **Engine Seizure:** 7 events, finale in hijacked Engine facility.
   - **Cascade Failure:** 7 events, finale in multi-Engine control room.
   - **The Resurrection:** 7 events, finale in Sealed Vault with awakened entity.
   - **Manufactured Crisis:** 7 events, finale in contested city-state.
   - **The Vault:** 7 events, finale in imperial vault with sealed horror.
2. Each scheme: faction involvement map, evidence placement strategy, finale dungeon configuration.
3. Authored setpieces for each finale: unique room segments, boss encounters, narrative resolutions.

**Content format:** JSON event chains in `content/campaigns/schemes/`. Each event has: id, trigger conditions, faction effects, route status changes, dialogue snippets, evidence grants.

**Acceptance criteria:**
- Each scheme's finale dungeon is visually and mechanically distinct.
- Evidence chain for each scheme leads to the Mastermind with ≥ 10 placed documents.
- Two campaigns with different schemes feel narratively distinct by turn 20.

**Depends on:** Phase 2 task 78 (scheme pipeline)

---

### 87. All 6 Complications
**Layer:** Content  
**Owner:** Content author

**Deliverables:**
1. Complete world-state modifiers for all 6 Complications:
   - **Bloom Siege:** Starting city has bloom pocket; Engine facilities degrade over time.
   - **Tithe Collapse:** No bone tithe in region; Bonewarden components scarce; Compact rep harder to gain.
   - **Open War:** Two factions start at war; multiple routes contested; faction soldiers everywhere.
   - **Erratic Engine:** One dungeon template has shifting geometry; random room rearrangement mid-dungeon.
   - **Missing Team:** Bureau team trail as first quest; evidence points to early Mastermind suspect.
   - **Closing Passes:** Routes close permanently at turn milestones; overworld shrinks.
2. Each complication: route status changes, faction behavior adjustments, dungeon environment modifiers.

**Acceptance criteria:**
- Each complication visibly changes campaign start state.
- Closing Passes removes at least 2 routes by turn 25.
- Erratic Engine causes at least 1 room shift per dungeon traversal.

**Depends on:** Phase 2 task 78 (complication pipeline)

---

### 88. Full synergy library
**Layer:** Content  
**Owner:** Content author

**Deliverables:**
1. 40–50 total synergies (expanding from 15–20 in Phase 2).
2. Secret synergies: 5–8 item-required or environmental synergies.
   - Example: "Animator's Bone Servant + Fieldwright's Overcharge + Engine Charge item" = skeleton detonates in massive AoE.
3. Faction-specific synergies: 3–5 synergies that only work against specific faction threats.
   - Example: "Stillblade Breaker + Fieldwright Artificer" = extra damage vs Engine constructs.
4. Balance pass: ensure no single synergy is mandatory; all are discoverable through experimentation.

**Acceptance criteria:**
- All synergies trigger in snapshot tests.
- A typical campaign with 6 random characters has 8–12 relevant synergies.
- Secret synergies require the stated item/environment; cannot trigger without it.

**Depends on:** Phase 2 task 63 (synergy engine)

---

### 89. Full NPC library
**Layer:** Content  
**Owner:** Content authors (both)

**Deliverables:**
1. Named characters: 50+ NPCs with distinct names, portraits, faction affiliations.
2. Dialogue sets per NPC: 3–5 dialogue variations per context.
   - Contexts: faction, campaign role (Patron/Threat/Mastermind/Wild Card), act (1/2/3).
3. NPC casting data: which NPCs can fill which roles per campaign.
4. Exclusive recruits: 8–10 named characters with unique abilities.

**Content format:** JSON in `content/npcs/`. Dialogue referenced by ID and tagged by context.

**Acceptance criteria:**
- No NPC is double-cast in the same campaign.
- NPC dialogue changes based on campaign act and player reputation.
- All named NPCs have unique portraits.

**Depends on:** Phase 2 task 58 (faction system)

---

### 90. Document/evidence library
**Layer:** Content  
**Owner:** Content author

**Deliverables:**
1. 60+ evidence documents total.
2. Each Mastermind/Scheme combo has a complete evidence chain (12–15 documents).
3. Documents tagged by: implicated faction, scheme relevance, act placement, dungeon template.
4. Document content: title, body text (2–4 paragraphs), implicated faction, evidence value (1–3 points toward threshold).

**Acceptance criteria:**
- Every campaign config places ≥ 10 evidence documents.
- Evidence chains are coherent: document N references events/places from documents 1..N-1.
- No document is placed in a dungeon template that contradicts its content.

**Depends on:** Phase 2 task 79 (evidence system)

---

### 91. Environmental lore
**Layer:** Content  
**Owner:** Content author

**Deliverables:**
1. Item descriptions: 100+ items have lore text reflecting campaign state.
2. Dungeon inscriptions: 30+ readable inscriptions per template, tagged by faction/scheme.
3. Faction markings: visual environmental storytelling (graffiti, banners, corpse placement).
4. All lore tagged for LLM selection: `valid_if: { faction: X, scheme: Y, act: Z }`.

**Acceptance criteria:**
- Item descriptions change based on campaign Mastermind (e.g., Bureau item has different description if Bureau is Mastermind).
- Inscriptions reference current campaign events, not generic placeholder text.

**Depends on:** Phase 2 task 81 (dungeon pipeline)

---

## Group 16: LLM Campaign Generation

### 92. Content library index
**Layer:** Engine (build-time)  
**Owner:** Backend lead

**Subtasks:**
1. Build-time indexer: scans all content JSON and produces `content-index.json`.
2. Index fields per content type:
   - Room segments: id, template, tags, size, connections.
   - Encounters: id, template, enemy types, difficulty.
   - NPCs: id, faction, roles, act availability.
   - Documents: id, faction, scheme, act, evidence value.
   - Items: id, faction, type, rarity.
   - Rumors: id, faction, truth status.
   - Events: id, faction, scheme, trigger conditions.
3. Index is loaded at campaign generation time; LLM prompt includes index summary.

**Acceptance criteria:**
- Index contains every content ID in the library.
- Index build completes in < 5 seconds.
- Index size < 5MB (loaded into memory at generation time).

**Depends on:** Group 15 (content library complete)

---

### 93. LLM generation prompt
**Layer:** Engine  
**Owner:** LLM prompt engineer + Backend lead

**Subtasks:**
1. Prompt structure:
   - **System prompt:** "You are a campaign arranger for a dungeon crawler. You select and connect pre-authored content. You do not write original prose."
   - **Input:** Six rolls + content index summary (available NPCs, segments, documents per template).
   - **Output format:** JSON matching Campaign Config Schema (design doc 09).
   - **Constraints:**
     - Dungeon sequence: 5–8 templates, no repeats except Underway.
     - Evidence: ≥ 10 documents placed across dungeons.
     - NPC casting: no double-casting, roles match faction affiliations.
     - Faction timelines: consistent with act structure.
2. Prompt versioning: prompts are versioned and stored in `content/llm-prompts/`.
3. Temperature: 0.2–0.4 (deterministic enough for structure, creative enough for variety).

**Acceptance criteria:**
- Prompt produces valid JSON 100% of the time (no markdown wrapping, no extra text).
- Output schema matches campaign config exactly.
- 10 test generations with same six rolls produce 10 distinct but coherent configs.

**Depends on:** Task 92

---

### 94. Campaign config schema
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. JSON Schema for campaign configuration (per design doc 09 data model).
2. Schema fields:
   - `campaignId`, `rolls` (six rolls).
   - `dungeonSequence`: ordered list of template IDs.
   - `dungeonAssignments`: per-dungeon faction presence, evidence slots, NPC casting, encounter escalation.
   - `townConfigurations`: per-town faction presence, engine type, special vendor, rumors.
   - `factionTimelines`: per-faction state transition turns and event lists.
   - `wildcardTrigger`: dungeon, turn window, rep threshold.
   - `evidenceChain`: ordered list of document IDs.
3. Schema validation: strict type checking, enum validation for faction/template IDs.

**Acceptance criteria:**
- Schema validates all hand-authored Phase 2 configs without modification.
- Schema rejects invalid configs with specific error messages.
- Schema is versioned; future additions are backward-compatible.

**Depends on:** Phase 2 Group 14 (campaign config system)

---

### 95. Validation layer
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. **Completeness check:** Every dungeon has faction presence, evidence slots, and NPC casting.
2. **Coherence check:**
   - All evidence IDs exist in content library.
   - All NPC IDs exist and are not double-cast.
   - All dungeon template IDs exist.
3. **Completability check:**
   - Evidence count ≥ 10.
   - Critical path through dungeon sequence exists (every dungeon reachable).
   - Faction timeline transitions are within turn limits.
4. **Faction consistency check:**
   - No faction assigned conflicting roles.
   - Mastermind's Scheme matches faction identity.
   - Wild Card is not Threat.
5. Retry logic: on validation failure, append specific constraint violation to prompt and re-generate. Max 3 retries.
6. Fallback: after 3 failures, load hand-authored config for the same six rolls.

**Acceptance criteria:**
- Validation catches 100% of invalid configs in test suite.
- Retry with constraint hint improves success rate to > 95%.
- Fallback config loads in < 1 second.

**Depends on:** Tasks 92, 94

---

### 96. Generation pipeline
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Pipeline flow:
   ```
   Six rolls → Content index query → LLM prompt build → LLM call → JSON parse → Schema validation → Full validation → Campaign config file
   ```
2. Performance target: < 30 seconds end-to-end.
3. Caching: cache generated configs by six-roll hash. Same rolls → cached result, no LLM call.
4. Progress UI: "Generating campaign..." with step indicators.
5. Offline mode: if LLM unavailable, use cached configs or fallback hand-authored configs.

**Acceptance criteria:**
- Generation completes in < 30 seconds on standard broadband.
- Cached configs load in < 1 second.
- Offline mode works without LLM connectivity.

**Depends on:** Tasks 93, 94, 95

---

### 97. Content addressing
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Campaign config references content by ID only.
2. Engine resolves IDs to binary-packed content at load time.
3. Missing ID handling: log error, skip content, continue loading (graceful degradation).
4. Addressing validation: build-time check that all IDs referenced in test configs exist.

**Acceptance criteria:**
- Campaign config file size < 50KB (only IDs, no embedded content).
- ID resolution is O(1) via dictionary lookup.
- Missing ID does not crash the game.

**Depends on:** Tasks 92, 96

---

### 98. Generation snapshot tests
**Layer:** Tests  
**Owner:** QA

**Subtasks:**
1. 5 known six-roll combinations as test inputs.
2. Assertions:
   - Validation passes on first attempt.
   - Critical path exists through dungeon sequence.
   - Evidence count ≥ 10.
   - Faction timeline transitions at valid turns.
3. Non-determinism handling: assert that 3 generations of same rolls all pass validation (even if configs differ).

**Acceptance criteria:**
- All 5 test combos pass validation.
- 10 random roll combinations also pass validation.

**Depends on:** Tasks 95, 96

---

## Group 17: Faction AI

### 99. Faction state machines
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Per-faction state machine: `Investigating → Preparing → Executing`.
2. Default transitions:
   - Investigating → Preparing: turn 12.
   - Preparing → Executing: turn 22.
3. Player-modified triggers:
   - Accelerated: completing faction-opposed missions, ignoring faction activity.
   - Delayed: disrupting faction operations, completing faction-aligned missions.
   - Delay cap: ±3 turns from default.
4. State effects:
   - Investigating: patrols on routes, contacts in towns.
   - Preparing: fortified territory, faction soldiers in dungeons, market price shifts.
   - Executing: scheme events fire, hostility increases, alliance offers appear.
5. Multi-faction collision: when 2+ factions are Executing simultaneously, resolution events fire (see task 103).

**Acceptance criteria:**
- Faction transitions at correct turns with no player interference.
- Disrupting faction operations delays transition by 1–2 turns.
- State changes are visible on overworld map.

**Depends on:** Phase 2 Group 11 (faction system)

---

### 100. Authored event chains
**Layer:** Content  
**Owner:** Content author

**Deliverables:**
1. 2–3 events per faction per state transition.
2. Total: ~45 events (5 factions × 3 transitions × 3 events).
3. Each event tagged by: faction, role (Threat/Mastermind/Wild Card/etc), scheme compatibility.
4. Event format: id, trigger conditions, visible effects, route status changes, town changes, evidence grants.

**Acceptance criteria:**
- Every faction transition fires at least 1 event.
- Events are distinct per faction (Bureau events feel bureaucratic; Stillness events feel militant).
- Events tagged for Mastermind role advance the Scheme.

**Depends on:** Task 99

---

### 101. Event scheduler
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Scheduler runs at campaign start and after each turn increment.
2. Checks faction state machines for transition triggers.
3. On transition: queries content library for events matching faction + role + scheme.
4. LLM-selected events: campaign config specifies which events fire; scheduler executes config.
5. Event execution: applies world state changes, sends UI notifications, updates quest log.

**Acceptance criteria:**
- Events fire at exact turns specified in campaign config.
- Event effects are visible in world state (route status, town availability).
- Multiple events on same turn fire in deterministic order.

**Depends on:** Tasks 99, 100

---

### 102. Visible faction actions
**Layer:** Client  
**Owner:** Frontend lead

**Subtasks:**
1. Overworld map updates:
   - Territory shading changes as factions claim/lose regions.
   - Route status updates in real-time (open → contested → blocked).
   - Faction markers appear/disappear on nodes.
2. UI notifications:
   - "The Stillness has claimed the northern pass."
   - "The Bureau has fortified Ashmark."
3. Town visual changes:
   - Faction-controlled towns show faction banners.
   - Contested towns show damage, reduced facilities.

**Acceptance criteria:**
- Player can see faction expansion without opening any menus.
- Territory changes are reflected on map within 1 turn.
- Town visuals match current faction control.

**Depends on:** Tasks 99, 101

---

### 103. Faction interaction rules
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Collision detection: when N factions are in Executing state simultaneously.
2. Pairwise resolution: for each pair of Executing factions, check for authored resolution event.
3. Multi-faction resolution (3+ factions): apply pairwise resolutions in priority order (Mastermind > Threat > Wild Card > others).
4. Resolution effects: may include route battles, town destruction, scheme acceleration, or unexpected alliances.

**Acceptance criteria:**
- Two Executing factions trigger at least 1 resolution event.
- Three Executing factions resolve without infinite loops.
- Resolution events have meaningful gameplay effects.

**Depends on:** Task 99

---

### 104. Faction AI snapshot tests
**Layer:** Tests  
**Owner:** QA

**Subtasks:**
1. Test inputs: faction timelines + player action sequences.
2. Assertions:
   - State transitions happen at expected turns ± player modifier.
   - Correct events fire at transitions.
   - Multi-faction collisions resolve correctly.
3. Coverage: all 5 factions, all 3 transitions, multi-faction scenarios.

**Acceptance criteria:**
- All faction timelines produce deterministic results.
- Player action modifiers correctly accelerate/delay transitions.

**Depends on:** Tasks 99, 103

---

## Group 18: The Unaccounted + Endgame

### 105. Unaccounted enemy type
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Rule-breaking behaviors (per design doc 06):
   - **Interrupt:** Acts between other turns, ignoring initiative order.
     - Implementation: insert Unaccounted turn at random initiative positions each round.
   - **Phase:** Appears at any range band without moving.
     - Implementation: spawn Unaccounted at random band at combat start; may teleport between bands.
   - **Reassemble:** Fallen Unaccounted recombine after 2 rounds.
     - Implementation: track dead Unaccounted; after 2 rounds, merge 2+ corpses into new enemy.
   - **Reach through:** Attacks target back row directly.
     - Implementation: melee abilities can target back row if flagged `unaccounted_reach`.
   - **Dread:** Status effect that doesn't tick down; only clears when source is killed.
     - Implementation: `DreadStatus` class with no duration; cleared on source death event.
2. Counters per behavior (per design doc 06):
   - Interrupt: Warden Shield Wall blocks regardless of timing.
   - Phase: Marcher Stalker can target any band.
   - Reassemble: Cauterist fire permanently prevents reassembly (applies `burned` tag to corpse).
   - Reach through: Animator summons absorb back-row targeting.
   - Dread: Ashmouth Agitator war cry dispels dread.
3. Unaccounted stats: high HP, low damage per hit, but rule-breaking creates attrition.

**Acceptance criteria:**
- Each rule-break functions as specified in combat.
- Each counter correctly negates the rule-break.
- Unaccounted encounter is winnable without any counter classes (harder, but possible).

**Depends on:** Phase 2 Group 12 (combat system)

---

### 106. Unaccounted renderer
**Layer:** Client  
**Owner:** Frontend lead

**Subtasks:**
1. Visual design: glitchy, wrong-timing, unsettling.
   - Models: humanoid but wrong proportions (too long limbs, missing features).
   - Materials: shifting textures, color inversion flickers.
2. Animation breaks:
   - Attack animations play at wrong speed (sometimes 2×, sometimes 0.5×).
   - Idle animation: model twitches or floats slightly.
   - Death animation: model does not fall; it folds unnaturally and fades.
3. Post-processing: subtle chromatic aberration when Unaccounted are on screen.

**Acceptance criteria:**
- Unaccounted are visually distinct from all other enemy types.
- Animation glitches are intentional, not bugs.
- Chromatic aberration triggers only when Unaccounted are visible.

**Depends on:** Task 105

---

### 107. Unaccounted audio
**Layer:** Client  
**Owner:** Frontend lead

**Subtasks:**
1. Audio design breaks:
   - Absence-of-sound: Unaccounted movement is silent (no footstep audio).
   - Reversed audio: attack sounds play backward.
   - Wrong pitch: ambient drone shifts to uncomfortable frequencies when Unaccounted appear.
2. Subtitle integration: "[Unnatural silence]", "[Reversed scream]", "[Wrong pitch drone]".
3. Audio cues as warning: 2–3 seconds of wrong audio before Unaccounted encounter starts.

**Acceptance criteria:**
- Unaccounted audio is unmistakably wrong compared to established patterns.
- Subtitles correctly describe audio events for accessibility.
- Warning audio gives player time to prepare.

**Depends on:** Task 105

---

### 108. Campaign epilogue
**Layer:** Engine  
**Owner:** Backend lead + LLM prompt engineer

**Subtasks:**
1. **Template epilogue** (always available, from Phase 2):
   - Pre-authored template with variable slots.
   - Populated from action log: Mastermind faction, scheme outcome, settlement fates, party losses.
2. **LLM epilogue** (enhanced, if available):
   - Structured prompt with action log summary (up to 20 key events).
   - Constraint: "Do not invent events not in the log. Do not contradict template facts."
   - Output: 2–3 paragraphs.
   - Cache result locally after generation.
3. Fallback: if LLM unavailable, display template epilogue.

**Acceptance criteria:**
- Template epilogue displays in < 1 second.
- LLM epilogue (if generated) reflects actual campaign events.
- Player always sees a conclusion, even offline.

**Depends on:** Phase 2 Group 14 (action log), Group 16 (LLM pipeline)

---

### 109. Ironman mode
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Single save slot, auto-saved after every action.
2. Save deleted on total party kill (TPK).
3. Bench rescue expedition:
   - If active party TPKs in dungeon, bench characters can attempt rescue.
   - Rescue expedition: 3 bench characters enter dungeon at entrance, must reach TPK location.
   - Success: recover equipment. Dead characters remain dead.
   - Failure: save deleted.
4. Fragile-state warning:
   - Trigger: < 3 bench characters AND campaign turn > 25.
   - UI warning: "Your roster is thin. A total party kill may end your campaign."

**Acceptance criteria:**
- TPK immediately deletes save file.
- Rescue expedition is playable with bench characters.
- Fragile-state warning appears at correct conditions.

**Depends on:** Phase 2 task 65 (death system), Task 56 (roster)

---

### 110. Secret content
**Layer:** Content + Engine  
**Owner:** Content author + Backend lead

**Subtasks:**
1. Hidden synergies: 3–5 synergies not listed in Field Notes until discovered.
   - Require obscure conditions (e.g., specific item + specific dungeon + specific enemy type).
2. Optional dungeons:
   - Accessible through obscure faction rep combos (e.g., 50+ Bureau AND 50+ Convocation).
   - Contain unique loot and lore.
3. Betrayal paths:
   - Player can side with Mastermind instead of Patron.
   - Requires specific evidence and dialogue choices.
   - Changes finale dungeon and epilogue.

**Acceptance criteria:**
- Hidden synergies are discoverable but not datamined from normal play.
- Optional dungeons appear only when conditions are met.
- Betrayal path is a valid campaign ending.

**Depends on:** Phase 2 Group 14 (campaign system)

---

## Group 19: Polish + Analytics

### 111. Audio system
**Layer:** Client  
**Owner:** Frontend lead

**Subtasks:**
1. Per-template ambient loops:
   - Broken Engine: industrial hum, machinery clanks.
   - Bloom Site: organic squelching, wrong wind.
   - Boneyard: bone-on-bone sorting, distant filing.
   - Sealed Vault: echoing drips, ward hums.
   - Contested Ruin: distant combat, campfire crackle.
   - Underway: water dripping, wind howl.
   - Settlement Gone Wrong: silence, occasional screams.
   - Ossuary: whispering, memorial chimes.
2. Combat ability sounds: one per action type (not per ability).
3. Faction motifs on overworld: short musical stinger when entering faction territory.
4. UI sounds: clicks, hovers, confirmations, warnings.
5. Synergy sound cue: distinctive chime (placeholder from Phase 1.5 replaced with final asset).

**Acceptance criteria:**
- Audio tells player what environment they're in without looking.
- Faction territory has recognizable musical motif.
- Synergy trigger sound is unmistakable.

**Depends on:** Phase 1 task 3a (audio hooks)

---

### 112. Lighting/weather
**Layer:** Client  
**Owner:** Frontend lead

**Subtasks:**
1. Dungeon lighting variations per template:
   - Broken Engine: flickering electric lights, emergency red zones.
   - Bloom Site: bioluminescent glow, pulsating shadows.
   - etc.
2. Overworld weather: rain, fog, bloom haze affecting mood (not gameplay).
3. Bloom visual effects:
   - Color distortion: chromatic aberration increases near bloom sources.
   - Geometry warping: subtle vertex displacement on bloom-corrupted meshes.
4. Performance: all effects run at 60fps on mid-range hardware.

**Acceptance criteria:**
- Each dungeon template has distinct lighting recognizable in screenshots.
- Bloom effects are unsettling without causing motion sickness.
- Performance target met on GTX 1060 equivalent.

**Depends on:** Phase 1 Group 2 (renderer)

---

### 113. Analytics hooks
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Local analytics: track events in `analytics.json`.
   - Synergies discovered (count and which ones).
   - Faction combos (final rep states).
   - Branches picked (per class).
   - Campaign outcomes (Mastermind exposed, scheme stopped, etc.).
2. Opt-in telemetry:
   - Player prompted once on first campaign completion.
   - If opted in, anonymized summary sent to telemetry endpoint.
   - No free-text, no PII, no session IDs.
3. Analytics UI: "Your Stats" screen showing personal aggregates.

**Acceptance criteria:**
- Analytics track all specified events without performance impact.
- Telemetry is strictly opt-in; default is off.
- Stats screen displays meaningful aggregates.

**Depends on:** Phase 2 Group 14 (campaign system)

---

### 114. Full Playwright suite
**Layer:** Tests  
**Owner:** QA

**Subtasks:**
1. Expanded smoke tests:
   - Campaign generation → navigate dungeon → enter combat → trigger synergy → faction encounter → save/load → overworld travel → complete campaign.
2. Critical path tests: 3 tests covering main quest completion.
3. Edge case tests: TPK, ironman deletion, rescue expedition, LLM fallback.
4. Performance tests: dungeon load < 3s, combat turn < 500ms, campaign generation < 30s.

**Acceptance criteria:**
- Full suite runs in < 10 minutes in CI.
- All critical path tests pass.
- Performance tests stay within targets.

**Depends on:** All Groups 15–19

---

### 115. Performance profiling
**Layer:** Both  
**Owner:** Both leads

**Subtasks:**
1. **Dungeon navigation:** 60fps target.
   - Profile Three.js draw calls; batch geometry per template.
   - LOD system for distant segments.
2. **Combat turns:** < 500ms resolution target.
   - Profile combat engine; optimize ability resolution hot path.
   - Precompute synergy lookups.
3. **Dungeon load:** < 3s target.
   - Profile content pack loading; stream segments asynchronously.
   - Cache assembled dungeon grids.
4. **LLM generation:** < 30s target.
   - Measure prompt build + LLM call + validation.
   - Optimize content index size.

**Acceptance criteria:**
- All targets met on reference hardware (i5-8400, 16GB RAM, GTX 1060).
- Profiling reports document bottlenecks and optimizations.

**Depends on:** All Groups 15–19

---

### 116. Gamepad support
**Layer:** Client
**Owner:** Frontend lead

**Subtasks:**
1. Gamepad detection via Web Gamepad API; surface in settings when connected.
2. Default binding: analog left stick → grid movement (deadzone configurable), face buttons → confirm/cancel/inventory/map, right stick → camera turn, shoulders → cycle targets in combat.
3. Binding UI: same conflict detection as keyboard; per-controller profile saved to KDL.
4. Mixed input: keyboard + gamepad active simultaneously; no priority swap required.
5. Accessibility integration: motion reduction also dampens rumble (where applicable).

**Acceptance criteria:**
- Standard Xbox/PS controller connects and is auto-bound; player can rebind any face button.
- Analog deadzone slider lives in settings under Input → Gamepad.
- Disconnect mid-combat falls back to keyboard without state loss.

**Depends on:** Phase 2 task 84d (display + a11y), Phase 1.5 task 51a (rebindable keys)

---

### 117. Secret-revealing synergies + environmental secret cues
**Layer:** Engine + Content
**Owner:** Backend lead + Content author

**Subtasks:**
1. Synergy effect type extension: `revealSecretInRange: int` (tiles).
2. 3–5 synergies that reveal secrets as side effects (e.g., Bonewarden Tremorsense + Fieldwright Overcharge reveals all breakables within 2 tiles).
3. Environmental storytelling: bloodstains, scratched walls, displaced furniture in segment data act as "?" hints on automap when the party is adjacent.
4. Documents cross-reference secrets: 5+ documents in Phase 3 library reference specific secret IDs (extends task 90).
5. Tag system: secrets gain `narrative_cue` field for the cue type; renderer composes the cue mesh decoration.

**Acceptance criteria:**
- Triggering the Tremorsense + Overcharge synergy reveals all breakable walls within 2 tiles.
- Bloodstain near a hidden door appears as a subtle decal; passing within 1 tile flags "?" on automap.
- 5 Phase 3 documents resolve to live secret IDs in content validation.

**Depends on:** Phase 2 task 84c (full secret system), Task 88 (synergy library)

---

## Content Volume & Pipeline

### Content Types

| Content Type | Count | Authoring Method | Effort |
|---|---|---|---|
| Room segments (new templates) | 60–80 | Hand-authored JSON | 4 weeks |
| Schemes (remaining 3) | 3 | Hand-authored JSON | 2 weeks |
| Complications (remaining 3) | 3 | Hand-authored JSON | 1 week |
| Synergies (expansion) | 25–30 | Hand-authored JSON | 2 weeks |
| NPCs (expansion) | 30+ | Hand-authored JSON | 2 weeks |
| Documents (expansion) | 40+ | Hand-authored JSON | 2 weeks |
| Environmental lore | 100+ items, 30+ inscriptions | Hand-authored JSON | 2 weeks |
| Faction AI events | ~45 | Hand-authored JSON | 2 weeks |
| Audio assets | 50–60 | External audio production | 3 weeks |
| Visual effects | 8 templates + bloom | Shader/VFX work | 2 weeks |

**Total content effort:** ~8 weeks with 2 authors + 1 audio contractor + 1 VFX artist (parallelized).

---

## Testing Strategy

| Level | Target | Tool | Count |
|---|---|---|---|
| Unit | Unaccounted behaviors, faction state machines, LLM validation | xUnit | 40 tests |
| Snapshot | Unaccounted encounters with diverse party comps, faction AI timelines | xUnit | 15 tests |
| Integration | LLM pipeline: 10 random rolls → validation → load campaign | xUnit | 10 tests |
| UI Smoke | Full campaign critical path, ironman mode, epilogue generation | Playwright | 8 tests |
| Performance | 60fps dungeon, < 500ms combat, < 3s load, < 30s generation | Custom | 4 tests |
| Manual | 5+ full campaigns with different six rolls | Playtesting | 5 runs |

---

## Dependency Graph

```
Phase 2 (complete)
  ├─► Group 15 (Content Library)
  │     ├─► 85 (Dungeon templates)
  │     ├─► 86 (Schemes)
  │     ├─► 87 (Complications)
  │     ├─► 88 (Synergies)
  │     ├─► 89 (NPCs)
  │     ├─► 90 (Documents)
  │     └─► 91 (Lore)
  ├─► Group 16 (LLM Generation)
  │     ├─► 92 (Content index)
  │     │     └─► depends on Group 15
  │     ├─► 93 (LLM prompt)
  │     │     └─► depends on 92
  │     ├─► 94 (Config schema)
  │     ├─► 95 (Validation)
  │     │     └─► depends on 92, 94
  │     ├─► 96 (Pipeline)
  │     │     └─► depends on 93, 94, 95
  │     ├─► 97 (Content addressing)
  │     │     └─► depends on 92, 96
  │     └─► 98 (Snapshot tests)
  │           └─► depends on 95, 96
  ├─► Group 17 (Faction AI)
  │     ├─► 99 (State machines)
  │     ├─► 100 (Event chains)
  │     │     └─► depends on 99
  │     ├─► 101 (Scheduler)
  │     │     └─► depends on 99, 100
  │     ├─► 102 (Visible actions)
  │     │     └─► depends on 99, 101
  │     ├─► 103 (Interaction rules)
  │     │     └─► depends on 99
  │     └─► 104 (Snapshot tests)
  │           └─► depends on 99, 103
  ├─► Group 18 (Unaccounted + Endgame)
  │     ├─► 105 (Unaccounted enemy)
  │     ├─► 106 (Unaccounted renderer)
  │     │     └─► depends on 105
  │     ├─► 107 (Unaccounted audio)
  │     │     └─► depends on 105
  │     ├─► 108 (Epilogue)
  │     │     └─► depends on Group 16 (LLM)
  │     ├─► 109 (Ironman)
  │     └─► 110 (Secret content)
  └─► Group 19 (Polish)
        ├─► 111 (Audio)
        ├─► 112 (Lighting/weather)
        ├─► 113 (Analytics)
        ├─► 114 (Playwright)
        │     └─► depends on all groups
        ├─► 115 (Performance)
        │     └─► depends on all groups
        ├─► 116 (Gamepad) depends on P2 84d, P1.5 51a
        └─► 117 (Secret-revealing synergies + cues) depends on P2 84c, Task 88
```

**Parallelization:**
- Group 15 (content) starts immediately; most work is parallelizable across authors.
- Group 16 (LLM) needs Group 15 complete for content index.
- Group 17 (Faction AI) can start in parallel with Group 15; needs Phase 2 faction system.
- Group 18 (Unaccounted) can start in parallel with Groups 15–17.
- Group 19 (Polish) is final-pass work; starts when all other groups are feature-complete.

---

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| LLM validation fails > 30% of time | Medium | Critical | Budget 2 weeks for prompt engineering. Fallback to hand-authored configs is robust. Pin model version. |
| Content volume (160+ segments) is unachievable | Medium | Critical | Procedural variation within segments (furniture/loot randomization). Re-skin connector segments across templates. |
| Unaccounted are frustrating, not scary | Medium | High | Extensive playtesting with diverse parties. Tune rule-break frequency so counters matter but aren't required. |
| Epilogue LLM call fails at worst moment | Low | High | Template epilogue always available. Cache LLM result. No online-only requirement. |
| Performance degrades with full content | Medium | Medium | Profile early and often. LOD, async loading, and geometry batching are must-haves, not optimizations. |
| Audio production timeline slips | Medium | Medium | Placeholder audio from Phase 1.5 is functional. Final audio is polish, not blocking. |

---

## Milestones

| Week | Milestone | Definition of Done |
|---|---|---|
| 3 | Content library complete | All 8 templates, all schemes, all complications authored and validated |
| 5 | LLM pipeline working | 10 random campaigns generate, validate, and load successfully |
| 6 | Faction AI alive | All 5 factions progress through states; world visibly changes |
| 7 | Unaccounted implemented | Rule-breaking behaviors function; counters work; playtesters report unease |
| 8 | Secret content discoverable | Hidden synergies, optional dungeons, betrayal paths found by playtesters |
| 9 | Polish pass | Audio, lighting, performance targets met |
| 10 | Phase 3 lock | Full Playwright suite passes, 5 manual campaigns complete, no P0 bugs |

---

## Appendix A — LLM Campaign Generation Prompt (Task 93)

Pinned model: Claude Sonnet 4.6 at launch; upgrade gated on validation pass rate ≥ 95% on test suite. Temperature 0.3.

**System prompt (verbatim):**
```
You are a campaign arranger for The Reach, a grim-fantasy dungeon crawler.

You SELECT and CONNECT pre-authored content. You DO NOT write prose, names, or descriptions.

Output a single JSON object matching the Campaign Config Schema. No prose. No markdown. No commentary.

Constraints you MUST satisfy:
1. dungeonSequence length 5..8, no repeats except "underway".
2. evidenceChain length >= 10. All IDs from input content index.
3. No NPC appears in more than one role across npcCasting values.
4. factionTimelines: investigating < preparing < executing for every faction.
5. wildcardTrigger.factionId must differ from rolls.threat and rolls.mastermind.
6. mastermind faction must have at least 4 evidence documents in evidenceChain.
7. Mastermind's faction identity must be plausibly compatible with the scheme (use provided faction-scheme affinity table).
8. Every dungeon in dungeonSequence must have a dungeonAssignments entry with factionPresence, evidenceSlots, npcCasting, encounterEscalation.
```

**User prompt template:**
```
ROLLS:
{rolls_json}

CONTENT INDEX SUMMARY:
{content_index_summary}      // up-to-5MB JSON; pruned to relevant subset per rolls

FACTION-SCHEME AFFINITY:
{affinity_table}

PRIOR CAMPAIGNS (for variety, last 3):
{prior_campaign_summaries}   // ID + dungeon sequence + key NPC roles only

OUTPUT: A single JSON object matching the Campaign Config Schema v3. No other text.
```

**Retry prompt addendum (on validation failure):**
```
Your previous output failed validation:
{validation_errors}          // pinpoint list of constraint violations
Regenerate the full Campaign Config JSON, fixing only these issues. Preserve all other choices.
```

**Caching:** Hash `(rolls, contentIndexHash, promptVersion)` → cached JSON. Cache survives upgrades only if `promptVersion` unchanged.

---

## Appendix B — Validation Rule Catalog (Task 95)

Three validation tiers. Run in order; fail fast.

### Tier 1 — Schema (JSON Schema strict)
- All required fields present.
- All enum values valid (`factionId`, `templateId`, `scheme`, `complication`).
- All ID-typed strings match `[a-z0-9-]+` pattern.

### Tier 2 — Coherence
| Rule | Error code |
|---|---|
| Every `evidenceChain` ID exists in content index | `evidence.unknown_id` |
| Every NPC ID in `npcCasting` exists in content index | `npc.unknown_id` |
| Every dungeon `templateId` exists in template registry | `template.unknown_id` |
| No NPC ID appears in two `npcCasting` values | `npc.double_cast` |
| `dungeonSequence` length in [5,8] | `dungeon.sequence_length` |
| No duplicate `templateId` except `underway` | `dungeon.duplicate_template` |
| Every dungeon has assignment entry | `dungeon.missing_assignment` |

### Tier 3 — Completability
| Rule | Error code |
|---|---|
| `evidenceChain` length ≥ 10 | `evidence.too_few` |
| Mastermind faction has ≥ 4 docs in chain | `evidence.mastermind_underrepresented` |
| Critical path: BFS from start node reaches finale | `dungeon.unreachable_finale` |
| Faction timeline ordering investigating < preparing < executing | `timeline.bad_order` |
| Faction timeline transitions within [1, 35] | `timeline.out_of_bounds` |
| `wildcardTrigger.factionId` ∉ {threat, mastermind} | `wildcard.invalid_faction` |
| `wildcardTrigger.turnWindow` overlaps any faction `executing` window | `wildcard.window_conflict` |

**Retry budget:** 3 LLM regenerations. After 3 failures, fallback to hand-authored config matching the same rolls (any acceptable, even if rolls don't match exactly — degraded experience but no crash).

---

## Appendix C — Unaccounted Behavior Contract (Task 105)

Engine implementation per design doc 06. Each rule-break is a flag on the enemy + a hook into combat state machine.

**Enemy schema extension:**
```json
{
  "id": "unaccounted-archetype-1",
  "ruleBreaks": ["interrupt", "phase", "reassemble", "reach_through", "dread"],
  "interrupt": { "extraTurnsPerRound": 1, "initiativeBucket": "random" },
  "phase":     { "startBand": "random", "teleportProbability": 0.3 },
  "reassemble":{ "delayRounds": 2, "requiredCorpses": 2, "blockedBy": "burned" },
  "reachThrough": { "abilityTagFilter": ["unaccounted_reach"] },
  "dread":     { "sourceLinked": true, "clearsOnSourceDeath": true }
}
```

**Hook points in combat state machine (task 17 from Phase 1):**
| Behavior | Hook | Counter check |
|---|---|---|
| Interrupt | After each combatant's turn, if Unaccounted has unspent extra turn this round, insert turn at random index of remaining order | Warden Shield Wall checks `defender.tags.has("shield_wall_active")` and blocks |
| Phase | At combat start: assign band from `phase.startBand`. On Unaccounted's own turn: roll vs `teleportProbability` to swap to random band | Marcher Stalker's `unaccounted_target_any_band: true` cancels positional requirement |
| Reassemble | At round end: scan dead Unaccounted older than `delayRounds`. If ≥ `requiredCorpses` not flagged `burned`, merge into new Unaccounted with summed HP/2 | Cauterist fire damage applies `burned` tag to corpse |
| Reach through | When melee ability flagged `unaccounted_reach` resolves vs party, target filter ignores row restriction | Animator summon intercepts: if any active summon on field, redirect target to summon |
| Dread | Status effect with `duration: null`, `clearsOn: { event: "death", entityId: <source> }` | Ashmouth war cry emits `dispel.status({ tag: "dread" })` |

**Counter discoverability:**
- Field Notes auto-populates Unaccounted entry on first encounter with cryptic warning.
- Counter hints appear in Field Notes after each rule-break is observed at least once.

---

## Appendix D — Action Log Categories (Phase 3)

Adds to Phase 2 Appendix D.

| Category | New types |
|---|---|
| `combat` | `unaccounted_encountered`, `unaccounted_counter_applied`, `dread_applied`, `dread_cleared`, `reassembly_blocked` |
| `narrative` | `betrayal_path_taken`, `optional_dungeon_unlocked`, `hidden_synergy_discovered` |
| `meta` | `analytics_optin_set`, `epilogue_template_shown`, `epilogue_llm_shown` |

**Epilogue prompt feeds (Task 108):**
- Key events selected by weight: `character_died (weight 3)`, `mastermind_accused (5)`, `wild_card_alliance_* (3)`, `settlement_fate_chosen (4)`, `betrayal_path_taken (5)`, `synergy_triggered (1, cap 5)`.
- Total capped at 20 events. Sorted by turn ascending.

---

## Appendix E — LLM Epilogue Prompt (Task 108)

**System:**
```
You are writing a campaign epilogue for The Reach. You summarize events the player actually experienced. You do NOT invent characters, places, or events. You write 2-3 short paragraphs in a grim, restrained tone.
```

**User template:**
```
CAMPAIGN FACTS:
{template_epilogue_text}     // the deterministic Phase 2 template output

KEY EVENTS (chronological):
{action_log_summary}         // up to 20 events with character/faction/town names

CHARACTER ARCS:
{character_arcs}             // surviving + dead, with branch and notable moments

CONSTRAINT:
- 2-3 paragraphs, 180-280 words total.
- Do not contradict CAMPAIGN FACTS.
- Do not name any character, faction, town, or event NOT in the inputs.
- Plain prose. No headers, no bullets, no markdown.
```

**Output validation:**
- Word count in [150, 320]: pass; outside range trigger one retry.
- Contains any noun not present in inputs (proper-noun extraction → set diff): fail, fall back to template.
- Cached per save file after first successful generation.
