# MVP Phases

Three phases, each producing a playable build. Each phase validates specific design assumptions before investing in the next.

## Phase 1: Core Loop

**Goal:** Does the dungeon crawl feel good? Is combat satisfying? Is the 3D navigation readable?

### Scope
- Low-poly 3D first-person dungeon navigation — grid movement, step/turn
- One authored dungeon template: The Broken Engine
- Party of 4 (reduced from 6 for simplicity)
- 4 classes available: Bonewarden, Stillblade, Cauterist, Hollow
- One branch per class, no branching choices yet
- Combat system: initiative order, front row/back row, basic actions (attack/defend/cast/use item)
- One enemy type per category: one bloom creature, one faction soldier, one Engine construct
- No overworld — menu-based hub town with tavern (recruit), market (buy/sell), mission board
- No faction system — linear 3-dungeon questline to validate pacing
- No synergy system
- No LLM generation — hand-configured campaign
- Basic automap
- Inventory and equipment system

### Technical Deliverables
- WebGL renderer: low-poly 3D dungeon view with grid-based movement
- Combat engine: turn-based initiative system, damage calculation, status effects
- Character system: stats, leveling (1-5 cap), basic ability trees
- Dungeon loader: hand-authored room segments, tile-based assembly
- UI: party status, inventory, automap, combat interface
- Save/load system

### Validation Questions
- Does moving through the dungeon feel satisfying in 3D?
- Is the combat loop engaging for 3+ hours without synergies or branching?
- Is the information density right — can players read the environment?
- Does the resource attrition model (limited healing, physical spell costs) create interesting decisions?

---

## Phase 1.5: Minimum Viable Strategy

**Goal:** Does the strategic layer change how players approach the core loop? Validates formation, faction tension, and composition consequences before the full content investment.

### Scope (adds to Phase 1)
- Full 6-person party with front/back row formation
- 6 classes available (add Fieldwright and Inkblood to Phase 1's four)
- One branch per class at level 3 (no level 6 branches yet)
- 2 dungeon templates: Broken Engine + Bloom Site (different tactical problems)
- 2 factions with reputation: Bureau and Convocation (opposed, creates dilemma)
- Reputation-gated vendor at rep 25 (one per faction)
- 5 synergies — enough to discover the system, not enough to catalog exhaustively
- Minimal overworld: 2 nodes connected by 1 route with travel encounters
- Turn counter (15 turns) — enough to feel time pressure without full three-act structure

### Technical Deliverables
- Formation system: front/back row targeting, row-dependent abilities
- Reputation tracker: two factions, threshold-gated content
- Synergy engine (minimal): ability-pair detection, Field Notes entry
- Overworld (minimal): two-node map, travel timer, one encounter table
- Second dungeon theme in renderer

### Validation Questions
- Does front/back row formation create meaningful positioning decisions?
- Do players notice and react to faction reputation consequences?
- Does discovering a synergy change player behavior (start planning around it)?
- Does the Bloom Site feel tactically different from the Broken Engine?
- Does the 6-person party feel meaningfully different from the 4-person Phase 1 party?

---

## Phase 2: Strategic Depth

**Goal:** Does the strategic bind work? Do party composition choices feel consequential? Does the faction system create tension?

### Scope (adds to Phase 1)
- Full 6-person party with front/back row formation
- All 8 classes with branching specializations at levels 3 and 6
- Roster/bench system with 12-character cap
- 4 dungeon templates: Broken Engine, Bloom Site, Contested Ruin, Underway
- Synergy system: 15-20 combinations, Field Notes journal
- Node-based overworld with 2 towns, travel time, random encounters
- Faction reputation system: all 5 factions present, reputation-gated vendors and recruits
- The six campaign rolls: implemented as hand-authored configuration files (no LLM yet)
- 3 Schemes and 3 Complications available
- Turn counter and world-state progression
- Faction soldiers that retreat, negotiate, and use their own tactics
- Town facilities: tavern, market, patron office, faction contacts, bone clerk
- Rumor system with truth/false/planted categorization

### Technical Deliverables
- Expanded renderer: 4 dungeon visual themes, overworld map screen
- Formation system: front/back row mechanics, row-targeting
- Branch system: permanent specialization choices, branch-specific abilities
- Synergy engine: ability-pair detection, Field Notes UI, hint system
- Faction system: reputation tracking, gated content, NPC attitude system
- Overworld: node graph, travel timer, encounter tables
- Campaign configuration: six-roll system as data files, content addressing
- World state manager: turn counter, faction action queue, event triggers

### Validation Questions
- Do players agonize over branch choices? (Good sign)
- Does discovering a synergy feel rewarding?
- Do faction relationships create genuine dilemmas?
- Does the six-roll system produce noticeably different campaigns from hand-authored configs?
- Is the bench system used strategically or ignored?
- Does the turn counter create appropriate urgency without frustration?

---

## Phase 3: Full Vision

**Goal:** Does every run feel distinct? Does the LLM arrangement produce coherent narratives? Is there enough content depth for 5+ runs before repetition?

### Scope (adds to Phase 2)
- All 8 dungeon templates
- All 6 Schemes, all 6 Complications
- Full synergy library: 40-50 combinations including secret/environmental synergies
- LLM-powered campaign arrangement:
  - Input: the six rolls
  - Output: dungeon sequence, NPC casting, evidence placement, faction encounter order
  - Constraint: selects and arranges from authored content library, does not generate prose
- 3-4 towns per campaign with distinct identities
- Faction AI: scripted state machines, not autonomous simulation (see Faction AI Architecture below)
- Exclusive faction recruits and reputation-locked branches
- The Unaccounted as a wildcard enemy type that breaks established combat patterns
- Campaign summary/epilogue generated from player choices
- Environmental storytelling: item descriptions, lore documents, NPC dialogue all reflect campaign state
- Secret content: hidden synergies, optional dungeons, faction betrayal paths

### Technical Deliverables
- Full renderer: all 8 dungeon themes, weather/lighting variations, bloom visual effects
- LLM integration:
  - Campaign generation pipeline (six rolls → content arrangement)
  - Content library format and addressing system
  - Validation layer ensuring LLM output is coherent and completable
  - Epilogue generation from player action log
- Faction AI system: state machines with authored event chains (see below)
- Full content library: all room segments, encounters, NPCs, documents, items
- The Unaccounted: enemy type with rule-breaking abilities (ignores initiative, crosses rows, etc.)
- Procedural dungeon assembly: room segment stitching, connectivity validation, pacing algorithm
- Analytics: track which synergies are discovered, which faction combos are common, which branches are picked

### Validation Questions
- Do two runs with different six-rolls feel like genuinely different stories?
- Does the LLM arrangement produce campaigns that feel authored rather than random?
- Is the Mastermind reveal satisfying — did the evidence trail work?
- Are there synergies that remain undiscovered after 5+ runs? (Good sign)
- Does the faction AI create emergent moments the designers didn't script?
- Is the Unaccounted unsettling even to experienced players?

---

## Faction AI Architecture

The "faction AI" is a set of per-faction state machines, not an autonomous simulation. Each faction progresses through authored states on the turn counter, with transitions modified by player actions and world state.

### State Machine

Each faction has a three-state progression per campaign:

```
Investigating → Preparing → Executing
```

- **Investigating** (Acts 1-2): The faction is gathering information, positioning assets. Visible effects: faction patrols on routes, contacts in towns offering missions, occasional claims on territory.
- **Preparing** (Act 2): The faction is committing resources. Visible effects: territory claims become fortified, faction soldiers appear in dungeons, prices shift at faction-controlled markets.
- **Executing** (Act 3): The faction acts on its goals. Visible effects depend on faction role:
  - Mastermind executing a Scheme: the Scheme's authored event fires (e.g., Engine goes dark, bloom accelerates)
  - Threat escalating: hostility increases, routes become contested, faction soldiers are more aggressive
  - Wild Card emerging: alliance offer appears if rep threshold met

### Transition Triggers

| Transition | Default Trigger | Player-Modified Trigger |
|---|---|---|
| Investigating → Preparing | Turn 12 | Accelerated if player completes faction-opposed missions; delayed if player disrupts faction operations |
| Preparing → Executing | Turn 22 | Accelerated if player ignores faction; delayed (up to 5 turns) if player directly interferes |

### Authored Events

Each state transition fires 1-2 pre-authored events from the content library. Events are tagged by faction and state, and the LLM (Phase 3) selects which specific events fire based on campaign context.

Example events for Stillness as Threat:
- Investigating → Preparing: "A minor Engine in [town] flickers. Locals report Stillness scouts in the area."
- Preparing → Executing: "The Engine at [town] goes dark. The Stillness claims responsibility and demands surrender of all Engine facilities."

This is a scheduler firing authored content, not a simulation making decisions. The emergent feeling comes from the intersection of multiple faction state machines advancing simultaneously.

## LLM Campaign Generation

### Data Model

The LLM receives the six rolls and outputs a campaign configuration — a structured data file that the game engine interprets. The LLM does not generate prose, dialogue, or runtime content.

```json
{
  "campaignId": "uuid",
  "rolls": {
    "patron": "convocation",
    "threat": "stillness",
    "mastermind": "bureau",
    "scheme": "cascade-failure",
    "wildCard": "cartography",
    "complication": "bloom-siege"
  },
  "dungeonSequence": [
    "broken-engine",
    "bloom-site",
    "underway",
    "contested-ruin",
    "boneyard",
    "underway",
    "sealed-vault"
  ],
  "dungeonAssignments": {
    "broken-engine": {
      "factionPresence": ["bureau", "convocation"],
      "evidenceSlots": [
        { "segmentTag": "faction-evidence-slot", "evidenceId": "doc-cascade-memo-1" },
        { "segmentTag": "faction-evidence-slot", "evidenceId": "doc-bureau-orders-3" }
      ],
      "npcCasting": {
        "questGiver": "npc-bureau-inspector",
        "bossEncounter": "npc-engine-guardian-alpha"
      },
      "encounterEscalation": [3, 5, 7]
    }
  },
  "townConfigurations": {
    "town-ashmark": {
      "factions": ["bureau", "convocation", "compact"],
      "engine": "water-purification",
      "specialVendor": "convocation-bloom-gear",
      "rumors": ["rumor-stillness-scouts-true", "rumor-bloom-origin-planted-bureau"]
    }
  },
  "factionTimelines": {
    "stillness": {
      "investigatingEnd": 12,
      "preparingEnd": 22,
      "events": ["event-stillness-scout-report", "event-engine-flicker", "event-engine-dark"]
    }
  },
  "wildcardTrigger": {
    "dungeon": "contested-ruin",
    "turnWindow": [18, 24],
    "repThreshold": 20
  },
  "evidenceChain": [
    "doc-cascade-memo-1",
    "doc-bureau-orders-3",
    "doc-engine-sabotage-report",
    "doc-mastermind-confession"
  ]
}
```

### Validation Layer

Before the campaign loads, a schema validator checks:
1. **Completeness** — every dungeon has faction presence, evidence, and NPC casting assigned
2. **Coherence** — evidence chain references content that exists in the content library; NPCs aren't double-cast
3. **Completability** — evidence threshold for Mastermind reveal (10) is achievable with the placed evidence count; critical path through dungeon sequence is traversable
4. **Faction consistency** — no faction is assigned conflicting roles; the Mastermind's Scheme matches its faction identity

If validation fails, the LLM re-generates with the specific constraint violation as additional context. Maximum 3 retries before falling back to a hand-authored campaign configuration.

### Content Library Format

All authored content is stored in structured data files addressable by ID and discoverable by tag. The LLM references IDs; the game engine resolves them.

Content types: room segments (see doc 07), encounter tables, NPCs, documents (evidence/lore), items, rumors, events. Each has an ID, tags, and prerequisites (e.g., "only valid if Stillness is Threat").

## Audio Direction

Audio is not a Phase 1 priority but the design should account for it from the start so that sound hooks exist in the codebase.

### Phase 1 Audio (Minimal)
- Dungeon ambience: looping environmental audio per dungeon template (industrial hum for Broken Engine)
- Combat: hit/miss/ability-use sound effects (one per action type, not per ability)
- UI: menu clicks, inventory management, map interaction
- Synergy cue placeholder: a distinctive sound reserved for synergy triggers (Phase 1.5+)

### Phase 2+ Audio
- Per-template ambient soundscapes (Bloom Site: organic squelching, wrong-sounding wind; Boneyard: bone-on-bone sorting, distant filing)
- Combat ability sounds per branch
- Faction-specific musical motifs on the overworld when in faction-controlled territory
- The Unaccounted: audio design priority. Their sound should break the established audio patterns the way their mechanics break combat rules — wrong pitch, reversed audio, silence where there should be sound.

### Design Principle
Audio should be information, not decoration. Sound tells the player what's coming: faction patrol audio before visual contact, bloom ambient shift before entering a bloom zone, the Unaccounted's absence-of-sound as a warning.

---

## Cross-Phase Concerns

### Save System
- Phase 1: single save slot, save at town
- Phase 2: multiple save slots, save at town and dungeon checkpoints
- Phase 3: ironman mode option (single save, deleted on death), standard mode with manual saves

### Content Pipeline
- All authored content (room segments, encounters, NPCs, items, documents) should be data-driven from Phase 1
- Use the structured content format defined in doc 07 (Room Segment Data Format) — ID-addressable, tag-discoverable
- Room segments, encounter tables, loot tables, NPCs, documents, and rumors as structured data files
- Every content entry has an ID (for LLM addressing in Phase 3) and tags (for filtering and validation)
- Phase 1 and 2 content automatically feeds into Phase 3's LLM arrangement system with no format migration
- NPC dialogue is pre-authored and tagged by context (faction, campaign role, act). The LLM selects which dialogue set to assign to which NPC — it does not generate dialogue text

### Performance Targets
- 60fps dungeon navigation on mid-range hardware
- Combat turn resolution under 500ms
- Dungeon load time under 3 seconds
- LLM campaign generation (Phase 3) under 30 seconds
