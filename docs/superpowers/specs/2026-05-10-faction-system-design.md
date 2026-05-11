# Faction System — Design Spec
Date: 2026-05-10
Status: design — Phase 2 deliverable; Phase 1.5 ships 2 factions
Depends on: `docs/design/04-factions.md`, `docs/design/09-mvp-phases.md` §Faction AI Architecture, quest-mission spec, world-clock spec
Scope: faction definitions, reputation tracking, state machines, scheme execution, threat behaviors. Phase 1 has zero factions; Phase 1.5 introduces 2; Phase 2 full 5.

## 1. Concept

Per `docs/design/04-factions.md`: five factions, each with goals + tactics + leverage points. Each campaign assigns roles via six rolls (Patron, Threat, Mastermind, Scheme, Wild Card, Complication).

Faction system = data-driven state machines layered on:
- Reputation (per-faction integer).
- Per-campaign role (assigned at campaign start).
- Three-state progression (Investigating → Preparing → Executing).
- Authored event chains fired at state transitions.

No autonomous simulation. All "intelligence" is scheduled authored content.

## 2. Data model

`content/factions/<id>.json`:

```json
{
  "id": "stillness",
  "name": "The Stillness",
  "shortName": "Stillness",
  "colorPrimary": "#5fa8c9",
  "colorAccent": "#0c2530",
  "iconId": "faction-stillness",
  "anchorCity": "old-calder",

  "goals": "Shut down the necromantic infrastructure. End the bone tithe.",
  "tactics": "Anti-magic, sabotage, scholar-soldiers.",
  "leverage": "They know how the Engines actually work.",

  "starterRep": 0,
  "repThresholds": {
    "feared": -100,
    "hostile": -50,
    "neutral": 0,
    "trusted": 25,
    "exalted": 75
  },

  "soldiers": ["enemy-stillness-warden", "enemy-stillness-scholar"],
  "vendorAt": {
    "trusted": "vendor-stillness-supply",
    "exalted": "vendor-stillness-archive"
  },
  "exclusiveRecruits": {
    "exalted": ["recruit-stillness-breaker-elite"]
  },

  "roleProfiles": {
    "patron": { "rumorBoostsPerTurn": 1, "missionTier1Pool": ["mission-stillness-scout","mission-stillness-recover"] },
    "threat": { "encounterEscalation": [3,5,7], "soldierAggression": "high" },
    "mastermind": { "schemeOptions": ["cascade-failure","engine-strike"] },
    "wildCard": { "alliancePrice": { "rep": 30, "gold": 500 } },
    "complicationActive": false
  }
}
```

Defines static parameters. Runtime state separate (§3).

## 3. Runtime state

Per save:

```csharp
public class FactionState {
    public Dictionary<string,int> Reputation { get; init; }       // by factionId
    public Dictionary<string,FactionRole> Roles { get; init; }    // assigned at campaign gen
    public Dictionary<string,FactionPhase> Phases { get; init; }  // Investigating|Preparing|Executing
    public Dictionary<string,List<string>> FiredEvents { get; init; }
    public Dictionary<string,FactionRelation> RelationsTo { get; init; }   // factionA → factionB
}

public enum FactionRole { Inactive, Patron, Threat, Mastermind, WildCard, Complication }
public enum FactionPhase { Investigating, Preparing, Executing }
public enum FactionRelation { Allied, Neutral, Opposed }
```

Roles assigned once per campaign (six rolls). Phases tick per world clock (per design 09 turn 12, 22 default triggers).

## 4. Reputation operations

```csharp
public void AdjustRep(string factionId, int delta, string cause);
public int GetRep(string factionId);
public string GetStanding(string factionId);   // "neutral", "trusted", etc.
public IEnumerable<(string,int,string)> History(string factionId);  // last N changes
```

Each adjustment logs to `RepHistory` (capped 200 entries). Surface in faction screen.

Trigger sources:
- Mission outcomes (`factionRepDelta` per quest spec §2).
- Dialogue actions (`faction_rep_delta` per dialogue spec §3).
- Combat: killing faction soldiers = -2 to that faction's rep.
- Vendor: buying faction-specific items at faction vendor = +1 per X gold spent.
- Public action visibility: some actions affect multiple factions (siding with one against another).

### 4.1 Cross-faction reputation effects

Some factions are opposed in lore — gaining rep with one may cost rep with another. Configurable per pair:

```json
"opposed": [
  { "faction": "compact", "factor": 0.4 },
  { "faction": "bureau",  "factor": 0.2 }
]
```

When Stillness rep gains +10, Compact rep adjusts -4 (0.4×) and Bureau -2 (0.2×). Cause logged: "Opposed faction effect: Stillness +10".

## 5. State machine

Per faction:

```
Investigating  ─turn_12─►  Preparing  ─turn_22─►  Executing
       │                                              │
       └──────────── player_interference ─────────────┘
                       (delay or accelerate)
```

Per design 09:
- Default transitions on world turns 12, 22.
- Modifiers:
  - Player completes faction-opposed mission → accelerate by 1-2 turns.
  - Player completes faction-supporting mission → delay by 1-2 turns.
  - Player directly interferes (sabotages a faction operation) → delay up to 5 turns.

Each phase transition fires 1-2 authored events from content library:

```json
{
  "id": "event-stillness-engine-flicker",
  "factionId": "stillness",
  "phaseTrigger": "investigating_to_preparing",
  "narrative": "A minor Engine in {town} flickers. Locals report Stillness scouts.",
  "effects": [
    { "type": "set_flag", "flag": "stillness_scouts_seen" },
    { "type": "spawn_encounter_pool", "pool": "stillness-scout-low" },
    { "type": "rumor_unlock", "rumorId": "rumor-stillness-scouts" }
  ],
  "townScope": ["old-calder"]
}
```

Engine selects which specific event fires based on campaign context (Phase 3 LLM; Phase 2 hand-mapped).

## 6. Schemes (Mastermind)

When a faction has role `Mastermind`, their `roleProfiles.mastermind.schemeOptions` lists possible Schemes. Campaign generation picks one. Scheme drives the `Executing` phase — the climactic event chain.

Sample scheme:

```json
{
  "id": "scheme-cascade-failure",
  "factionId": "stillness",
  "displayName": "Cascade Failure",
  "stages": [
    { "narrative": "Whispers of engine malfunctions across the Reach.", "atPhase": "investigating" },
    { "narrative": "The Old Calder Engine goes dark.", "atPhase": "preparing", "effect": { "type":"set_flag", "flag":"engine_dark_old_calder" } },
    { "narrative": "Three more cities fall silent. The Reach burns its dead.", "atPhase": "executing", "effect": { "type":"global_event", "id":"cascade-event" } }
  ]
}
```

## 7. Wild Card

Faction with role `WildCard` offers an alliance midway through the campaign:

- Visibility: rumored from turn 12 onward.
- Trigger: when player reaches `roleProfiles.wildCard.alliancePrice.rep` reputation with them.
- Effect: faction switches `RelationsTo[playerFactions]` to `Allied`. Opens an exclusive late-game mission. Costs reputation with currently-Patron faction (per `docs/design/04`).

Accepting / declining is an explicit player choice via dialogue.

## 8. Complication

A complication is a faction-driven crisis that overlaps with another faction's role. Authored as a scheme that runs on a different timeline:

```json
{
  "id": "complication-bloom-siege",
  "factionId": "convocation",
  "stages": [...]
}
```

`docs/design/04` complications: bloom-siege, betrayal-from-within, evidence-burned, etc. Each implemented as a scheme-like authored event chain.

## 9. UI

### 9.1 Faction screen (Town → Faction Office)

```
┌─Factions─────────────────────────────────────────────────────┐
│ Tabs: [All] [Patron] [Threat] [Mastermind] [Wild Card]       │
├───────────────────────────────────────────────────────────────┤
│ The Stillness                                  THREAT  Phase 2│
│ ─────                                                         │
│ "Shut down the necromantic infrastructure."                   │
│                                                               │
│ Standing: Hostile (-32)                                       │
│ ▓▓▓▓░░░░░░ -100 ──── -50 ── 0 ── +25 ── +75 ── +100           │
│                                                               │
│ Recent rep changes:                                           │
│   Day 4: Completed Foothold (-8)                              │
│   Day 3: Killed 2 Wardens (-4)                                │
│                                                               │
│ Threat phase: Preparing (executes at turn 22, ~10 turns)      │
│ Recent events:                                                │
│   "Old Calder Engine goes dark."                              │
│                                                               │
│ [View History]                                                │
└───────────────────────────────────────────────────────────────┘
```

Tablet portrait: stacked accordion.

### 9.2 Topbar faction tick badge

When any faction phase transition fires, banner appears above Topbar for 4 seconds with brass underline:

```
─── The Stillness moves to Preparing. ───
```

Followed by toast linking to faction screen.

### 9.3 Rep change indicators

Mission completion modal shows per-faction rep deltas:

```
Reputation:
  Bureau     +5
  Stillness  -3
  Compact    +1
```

Color-coded (positive green-ish, negative red-ish) but never sole signal — always text.

## 10. Engine

`src/engine/RPC.Engine/Factions/`:

```
Factions/
  FactionDef.cs              // immutable from content
  FactionState.cs            // runtime per save
  FactionRegistry.cs         // load all defs
  FactionEngine.cs           // phase ticking + event firing
  RepHistory.cs              // log
```

`GameState`:

```csharp
public FactionState Factions { get; }
```

`FactionEngine.OnWorldTurnAdvanced(int newTurn)`:

```csharp
foreach (var (id, def) in registry) {
    var phase = state.Phases[id];
    var nextThreshold = ComputeNextTransitionTurn(def, state, id);
    if (newTurn >= nextThreshold) {
        AdvancePhase(id);
        FireEvents(id, GetEventsForTransition(id));
    }
}
```

Cross-references to world-clock spec §3 — faction tick is one of the registered subscribers.

## 11. Campaign generation hook

Phase 2 hand-authored; Phase 3 LLM-assigned. At campaign start:

1. Roll six values: patron, threat, mastermind, scheme, wildCard, complication.
2. Apply roles: each faction gets `FactionRole.Inactive` except the four assigned.
3. Apply complication's faction.
4. Initialize phases to `Investigating` for all roled factions.
5. Set transition turns from `clock.json` (defaults 12, 22; modifiable per campaign).
6. Seed initial reputation per faction's `starterRep`.

## 12. Save / load

`SaveData` extension:

```csharp
public FactionStateData Factions { get; set; }
public List<RepHistoryEntry> RepHistory { get; set; }
public Dictionary<string, List<string>> FiredFactionEvents { get; set; }
public CampaignRollsData CampaignRolls { get; set; }
```

Save version → `"8"` (after release-pipeline + earlier specs).

## 13. Tests

- xUnit: phase advances on turn threshold; player interference delays correctly.
- xUnit: opposed-faction rep effect computes correctly.
- xUnit: alliance offer fires once threshold crossed and not before.
- Integration: full 35-turn campaign with hand-crafted rolls → expected events fire in order.
- Playwright: faction screen shows current standings; click history shows last N changes.

## 14. Phase rollout

- Phase 1: skip entirely. Faction system absent.
- Phase 1.5: Bureau + Convocation only (per `docs/design/09` Phase 1.5 scope). Reputation tracker, threshold-gated vendor. Minimal role assignment (Patron + Threat). No schemes.
- Phase 2: all 5 factions, full state machines, role assignment via campaign generation, schemes + complications, faction screen.
- Phase 3: LLM-generated campaign rolls + event selection from authored pool.

## 15. Out of scope

- Multi-player faction PvP.
- Real-time faction simulation (deliberately authored, not simulated).
- Dynamic faction creation (always the canonical 5).
- Faction win/lose meta-progression across campaigns.
