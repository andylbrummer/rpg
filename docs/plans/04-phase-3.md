# Phase 3: Full Vision

**Goal:** Does every run feel distinct? Does the LLM arrangement produce coherent narratives? Is there enough content depth for 5+ runs?

**Prerequisite:** Phase 2 complete. Strategic bind validated. Campaign configs feel different with hand-authored data.

## Group 15: Full Content Library

| # | Task | Layer | Output |
|---|---|---|---|
| 85 | Remaining dungeon templates | Content + Client | Boneyard, Sealed Vault, Settlement Gone Wrong, Ossuary. Visual themes, room segments (15-20 each). |
| 86 | All 6 Schemes | Content | Complete event chains for each. Authored setpieces for each Scheme's finale dungeon. |
| 87 | All 6 Complications | Content | World-state modifiers. Each affects overworld routes, dungeon environments, faction behavior. |
| 88 | Full synergy library | Content | 40-50 synergies including secret/environmental. Item-required synergies. Faction-specific synergies. |
| 89 | Full NPC library | Content | Named characters for every faction role. Dialogue sets tagged by context (faction, campaign role, act). |
| 90 | Document/evidence library | Content | Full evidence chain per Mastermind/Scheme combo. Enough documents to exceed the threshold (10) per campaign. |
| 91 | Environmental lore | Content | Item descriptions, dungeon inscriptions, faction markings — all reflecting campaign state and tagged for LLM selection. |

**Content volume:** ~160-240 room segments, 40-50 synergies, 50+ NPCs with dialogue, 60+ evidence documents, 100+ items.

## Group 16: LLM Campaign Generation

| # | Task | Layer | Output |
|---|---|---|---|
| 92 | Content library index | Engine | Build-time index of all content by ID and tag. Queryable at generation time. |
| 93 | LLM generation prompt | Engine | Structured prompt: six rolls + content index summary → campaign config JSON. |
| 94 | Campaign config schema | Engine | JSON Schema for the campaign configuration (design doc 09 format). Validates LLM output. |
| 95 | Validation layer | Engine | 4 checks: completeness, coherence, completability, faction consistency. Retry on failure (max 3). Fallback to hand-authored config. |
| 96 | Generation pipeline | Engine | Six rolls → LLM call → validation → campaign config file. Under 30s target. |
| 97 | Content addressing | Engine | LLM output references content by ID. Engine resolves IDs to binary-packed content at load time. |
| 98 | Generation snapshot tests | Tests | 5 known six-roll combos → assert validation passes, critical path exists, evidence count meets threshold. |

**Note:** The LLM generates a campaign configuration — a JSON file. It does not generate dialogue, descriptions, or any runtime text. All prose is pre-authored in the content library and selected by the LLM.

## Group 17: Faction AI

| # | Task | Layer | Output |
|---|---|---|---|
| 99 | Faction state machines | Engine | Per-faction: Investigating → Preparing → Executing. Transition triggers on turn counter + player action modifiers. |
| 100 | Authored event chains | Content | 2-3 events per faction per state transition. Tagged by faction and role (Threat/Mastermind/etc). |
| 101 | Event scheduler | Engine | Fires events at state transitions. LLM selects which specific events from tagged pool during campaign generation. |
| 102 | Visible faction actions | Client | Overworld map shows faction territory changes, route status updates, faction markers appearing/disappearing. |
| 103 | Faction interaction rules | Engine | When two faction state machines collide (both Executing at the same time), authored resolution events fire. |
| 104 | Faction AI snapshot tests | Tests | Given faction timelines + player actions, assert state transitions happen at correct turns and fire correct events. |

## Group 18: The Unaccounted + Endgame

| # | Task | Layer | Output |
|---|---|---|---|
| 105 | Unaccounted enemy type | Engine | Rule-breaking combat behaviors: interrupt, phase, reassemble, reach through, dread. Per design doc 06. |
| 106 | Unaccounted renderer | Client | Visually distinct. Animation style breaks established patterns (glitchy, wrong timing, unsettling). |
| 107 | Unaccounted audio | Client | Audio design: absence-of-sound, reversed audio, wrong pitch. Breaks established audio patterns. |
| 108 | Campaign epilogue | Engine | Player action log → summary of consequences. LLM generates epilogue text from structured event history. |
| 109 | Ironman mode | Engine | Single save, deleted on death. Bench rescue expedition mechanic. Fragile-state warning (< 3 bench, past turn 25). |
| 110 | Secret content | Content | Hidden synergies, optional dungeons accessible through obscure faction rep combos, betrayal paths. |

## Group 19: Polish + Analytics

| # | Task | Layer | Output |
|---|---|---|---|
| 111 | Audio system | Client | Per-template ambient loops, combat ability sounds, faction motifs on overworld, UI sounds. |
| 112 | Lighting/weather | Client | Dungeon lighting variations per template. Overworld weather affecting mood. Bloom visual effects (color distortion, geometry warping). |
| 113 | Analytics hooks | Engine | Track: synergies discovered, faction combos, branches picked, campaign outcomes. Local storage, opt-in telemetry. |
| 114 | Full Playwright suite | Tests | Expanded smoke tests: campaign generation → dungeon run → combat → faction encounter → save/load → overworld travel. |
| 115 | Performance profiling | Both | 60fps dungeon navigation, < 500ms combat turns, < 3s dungeon load, < 30s LLM generation. |

## Key Risks

### LLM Output Quality
The LLM must produce valid campaign configs that pass 4 validation checks. If validation fails frequently (> 30% of attempts), the prompt needs iteration or the content library needs better tagging. Budget time for prompt engineering.

### Content Volume
160-240 room segments is a massive authoring effort. Consider:
- Procedural variation within segments (furniture/loot placement randomized, geometry fixed)
- Segment reuse across templates with re-skinning (a corridor is a corridor regardless of theme)
- Prioritize setpiece segments (hand-crafted) over connector segments (can be more formulaic)

### The Unaccounted Balance
Rule-breaking enemies are hard to balance. Too weak: they're just weird enemies. Too strong: they're frustrating. The per-class counters help, but a party without the right counter classes could hit a wall. Snapshot tests for Unaccounted encounters should cover diverse party compositions.

### Epilogue Quality
The epilogue is the one place the LLM generates prose at runtime. Keep it constrained: structured templates with variable slots, not free-form generation. "The Stillness achieved [X] because you [Y]" rather than "Write a paragraph about what happened."

## Dependency Graph

```
Phase 2 (complete)
  ├─► Group 15 (Content) ──► Group 16 (LLM Gen) [needs content library to address]
  ├─► Group 17 (Faction AI) [needs Phase 2 faction system]
  ├─► Group 18 (Unaccounted) [needs Phase 2 combat system]
  └─► Group 19 (Polish) [can start with audio alongside any group]

Group 16 ──► Group 17 [LLM selects faction events during generation]
Group 17 + 18 ──► Group 19 [analytics needs all systems running]
```
