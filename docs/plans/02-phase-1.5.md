# Phase 1.5: Minimum Viable Strategy

**Goal:** Does the strategic layer change how players approach the core loop? Validates formation, faction tension, and composition consequences before the full content investment.

**Prerequisite:** Phase 1 complete. Dungeon navigation and combat feel good.

## Group 6: Full Formation

| # | Task | Layer | Output |
|---|---|---|---|
| 33 | Expand party to 6 | Engine | 3 front row + 3 back row. Update party system, formation assignment UI. |
| 34 | Row-dependent abilities | Engine | Abilities that require front row (melee) or back row (ranged). Ability availability changes with formation. |
| 35 | Add Fieldwright + Inkblood | Content | Two new classes with one branch each. Fieldwright: Artificer (deployable gadgets). Inkblood: Cartographer (exploration bonuses). |
| 36 | Formation UI | Client | Drag-and-drop row assignment. Visual distinction between front/back in combat renderer. |
| 37 | Update combat renderer for 6 | Client | Three enemy slots per range band (matching 3+3 party). Updated targeting UI for larger parties. |

**Validation:** Does 3+3 formation create positioning decisions that 2+2 didn't? Do Fieldwright gadgets and Inkblood exploration bonuses feel distinct from the Phase 1 classes?

## Group 7: Faction Foundation

| # | Task | Layer | Output |
|---|---|---|---|
| 38 | Reputation system | Engine | Signed integer per faction (-100 to +100). Actions shift reputation. Threshold checks. |
| 39 | Two factions: Bureau + Convocation | Content | Identity, vendor inventory, reputation thresholds (25 for vendor access), 2 side missions each. |
| 40 | Reputation-gated vendor | Client + Engine | Faction vendor appears in town market at 25+ rep. Sells faction-exclusive gear. |
| 41 | Faction contacts in town | Client | NPC dialogue panels. Reputation-gated dialogue options. Side mission acceptance. |
| 42 | Reputation consequences | Engine | Completing Bureau missions costs Convocation rep and vice versa. Player feels the dilemma. |

**Validation:** Do players hesitate before taking faction missions? Does unlocking a faction vendor feel earned? Does the Bureau/Convocation tension create a real choice?

## Group 8: Synergy Spark

| # | Task | Layer | Output |
|---|---|---|---|
| 43 | Synergy detection engine | Engine | Checks ability pairs within same combat round. Triggers bonus effect on second ability resolution. |
| 44 | 5 synergies | Content | One per plausible class pair from the 6 available classes. Authored effects, hint text. |
| 45 | Synergy feedback (3-tier) | Client | Flash + sound on trigger. Field Notes auto-entry. Replayable animation from Field Notes. |
| 46 | Field Notes journal | Client | Svelte panel. Lists discovered synergies with hint text. Replay button per entry. |

**Validation:** Does discovering a synergy change player behavior — do they start planning around it? Does the three-tier feedback ensure synergies aren't missed? Is 5 enough to prove the system without overwhelming?

## Group 9: Minimal Overworld

| # | Task | Layer | Output |
|---|---|---|---|
| 47 | Two-node overworld | Engine | Town A ↔ Town B connected by one route. Travel costs 1 campaign turn. |
| 48 | Overworld map UI | Client | Svelte node graph. Click destination to travel. Route shows danger rating. |
| 49 | Travel encounters | Engine | One encounter table for the route. 1-2 encounters per trip drawn from pool. Resolution per design doc 08 table. |
| 50 | Turn counter | Engine | 15-turn campaign. Displayed in UI. World state doesn't change yet (no faction AI), but the counter creates urgency. |
| 51 | Second dungeon template: Bloom Site | Content + Client | New room segments (10-15), bloom visual theme in renderer, bloom creature encounters. |

**Validation:** Does travel feel like a decision (danger vs. time cost)? Does the Bloom Site feel tactically different from the Broken Engine? Does the turn counter create urgency even without faction AI?

## Content Needed

- 2 class definitions (Fieldwright Artificer, Inkblood Cartographer) with abilities and level-up tables
- 2 faction profiles with vendor inventory, reputation thresholds, 2 side missions each
- 5 synergy definitions (ability pairs, effects, hint text, Field Notes entries)
- 1 travel encounter table (6-8 encounter definitions with resolution mechanics)
- 10-15 Bloom Site room segments
- Bloom creature encounter set (2-3 new enemy types)

## Dependency Graph

```
Phase 1 (complete)
  ├─► Group 6 (Formation) ──► Group 8 (Synergies) [needs 6 classes]
  ├─► Group 7 (Factions)   [independent of formation]
  └─► Group 9 (Overworld)  [independent of formation/factions]
        └─► needs Group 7 for town faction contacts
```

Groups 6, 7, and 9 can start in parallel. Group 8 needs Group 6 complete (6 classes available). Group 9's town faction contacts need Group 7's reputation system.
