# Phase 2: Strategic Depth

**Goal:** Does the strategic bind work? Do party composition choices feel consequential? Does the faction system create tension?

**Prerequisite:** Phase 1.5 complete. Formation, faction basics, and synergies validated.

## Group 10: Full Class System

| # | Task | Layer | Output |
|---|---|---|---|
| 52 | Add Marcher + Ashmouth | Content | Two remaining classes. Marcher: Pathfinder branch. Ashmouth: Agitator branch. |
| 53 | Branch system | Engine | Permanent specialization choices at level 3 and 6. Branch-specific abilities unlock. Choice UI at level-up. |
| 54 | All 8 classes, all branches | Content | Complete ability definitions for every class/branch combination (8 classes x 2-3 branches). |
| 55 | Level cap 10 | Engine | Extend XP curve to level 10. Tuned to 35-turn three-act structure per design doc 05. |
| 56 | Roster/bench system | Engine | 12-character roster. Bring 6 to dungeons. Bench characters don't gain XP. Field promotion mechanic (50% bonus XP for catch-up). |
| 57 | Roster management UI | Client | Bench view, swap characters in/out of active party, formation assignment for 6. |

**Validation:** Do players agonize over branch choices? Is the bench used strategically? Does the field promotion mechanic prevent bench characters from being permanently useless?

## Group 11: Full Faction System

| # | Task | Layer | Output |
|---|---|---|---|
| 58 | All 5 factions | Content | Complete profiles: Stillness, Ossuary Compact, Cartography added. Vendors, contacts, side missions, exclusive recruits. |
| 59 | Compact signature mechanics | Engine | Ancestral bargaining (Bonewarden + Compact rep → negotiate with tithe-constructs), bloodline locks, family archives. |
| 60 | Faction-gated recruits | Engine | Exclusive NPCs requiring reputation thresholds. Beastkeeper gate at Compact 25+. |
| 61 | Faction-gated branch fallback | Engine | Alternate (weaker) branch version if player doesn't meet rep threshold at level 6. |
| 62 | Reputation impact on encounters | Engine | Patrol encounters check rep: friendly/neutral/hostile. Town NPC attitude shifts. |

**Validation:** Do faction relationships create genuine dilemmas? Does the Compact's signature mechanics feel distinct? Do gated recruits drive reputation investment?

## Group 12: Expanded Combat

| # | Task | Layer | Output |
|---|---|---|---|
| 63 | Full synergy library | Content | 15-20 synergies. At least one per viable class pair. Secret/environmental synergies started. |
| 64 | Faction soldier AI | Engine | Soldiers retreat when outmatched. Use their own synergies. Equipment matches faction identity. Ashmouth can negotiate. |
| 65 | Death and resurrection | Engine | Downed → dead flow. Bone Clerk resurrection with escalating costs per design doc 05. Permanent death on 3rd attempt. |
| 66 | Component inventory | Engine | Full inventory system: 8 slots per character, 12-slot expedition cache, component stacking, fallback casting (blood at 2x HP). |
| 67 | Component inventory UI | Client | Inventory rework: component counts visible, expedition cache panel, warnings when running low. |
| 68 | Downtime system | Engine | One downtime action per character per town visit. Rest/train/craft/network/investigate/lay low/tend blooms. |
| 69 | Downtime UI | Client | Town screen shows downtime allocation per character. |

**Validation:** Does component scarcity create real decisions mid-dungeon? Does death feel consequential without being purely punitive? Is downtime allocation interesting?

## Group 13: Full Overworld

| # | Task | Layer | Output |
|---|---|---|---|
| 70 | Node-based overworld | Engine | Full graph: 2-4 towns, dungeon entrances, routes with distance/danger/terrain/status. |
| 71 | Overworld map renderer | Client | Visual overworld map. Nodes, routes, danger indicators, faction territory markers. |
| 72 | Travel encounter system | Engine | Full encounter tables per route. Resolution mechanics per design doc 08. Class-specific alternatives. |
| 73 | Route status changes | Engine | Routes change status during campaign: open → contested → blocked → bloom-affected. Complication roll affects initial state. |
| 74 | Town facilities (full) | Client + Engine | Tavern (with rumor system), market (price fluctuation), patron office, faction contacts, bone clerk. |
| 75 | Rumor system | Engine | Rumors tagged true/outdated/planted. Verification via Ashmouth (reliable) or Inkblood/Hollow (80% accurate). |
| 76 | Closing window signals | Engine + Client | Ambient signals (price shifts, NPC dialogue), direct warnings (rep-gated), turn counter markers (stable → critical). |

**Validation:** Does the overworld feel like a strategic layer, not a loading screen? Do rumors create interesting information decisions? Do closing windows feel like missed choices, not arbitrary punishment?

## Group 14: Campaign Configuration

| # | Task | Layer | Output |
|---|---|---|---|
| 77 | Six-roll system | Engine | Patron/Threat/Mastermind/Scheme/WildCard/Complication as data. Hand-authored config files (no LLM). |
| 78 | 3 Schemes + 3 Complications | Content | Authored event chains for each. Enough variety to test the combinatorial system. |
| 79 | Evidence system | Engine | Per-faction evidence counter. Threshold effects (3/5/7/10) from design doc 03. Evidence placed in dungeons. |
| 80 | Mastermind discovery flow | Engine | Five evidence channels. Inkblood/Ashmouth class checks at thresholds. Act 3 confrontation trigger at 7+. |
| 81 | 4 dungeon templates | Content + Client | Broken Engine, Bloom Site, Contested Ruin, Underway. Visual themes and room segments for each. |
| 82 | Turn counter + world state | Engine | 35-turn campaign. Three-act pacing. Faction states progress (manually scripted, not AI yet). |
| 83 | Wild Card trigger | Engine | Wild Card faction appears mid-campaign if rep threshold met. Alliance offer with mechanical benefits. |
| 84 | Campaign snapshot tests | Tests | 3 different six-roll configs → assert dungeon sequence, evidence placement, faction states at turn milestones. |

**Validation:** Do two hand-authored configs feel like different campaigns? Does the evidence trail toward the Mastermind work? Does the Wild Card appearance feel earned?

## Content Volume

Phase 2 is the largest content investment:

| Content Type | Count | Notes |
|---|---|---|
| Class/branch definitions | 8 classes, ~20 branches | Full ability trees, costs, targeting |
| Room segments | 60-80 | 4 templates x 15-20 each |
| Encounters | 30-40 | Per-template + overworld + faction soldiers |
| Items | 50-60 | Weapons, armor, components, consumables, faction exclusives |
| NPCs | 20-25 | Faction contacts, recruitable characters, quest givers |
| Synergies | 15-20 | Ability pairs + effects + hints |
| Rumors | 20-30 | True/outdated/planted, per-town distribution |
| Documents (evidence) | 15-20 | Dungeon-placed evidence for Mastermind discovery |
| Schemes/Complications | 3+3 | Authored event chains |

## Dependency Graph

```
Phase 1.5 (complete)
  ├─► Group 10 (Classes) ──► Group 12 (Combat) [needs branches for synergies/death]
  ├─► Group 11 (Factions) ──► Group 14 (Campaign) [needs all 5 factions]
  ├─► Group 13 (Overworld) [needs Group 11 for faction territory]
  └─► Group 14 (Campaign) [needs Groups 10-13 all complete]
```

Groups 10 and 11 can start in parallel. Group 12 needs Group 10 (branch system). Group 13 needs Group 11 (faction territory). Group 14 integrates everything.
