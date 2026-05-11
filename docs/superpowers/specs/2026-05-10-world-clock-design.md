# World Clock & Turn Cost — Design Spec
Date: 2026-05-10
Status: design — Phase 2 deliverable; Phase 1.5 ships counter only (no faction effects)
Depends on: faction system (separate Phase 2 spec), mission spec, settings spec
Scope: turn counter, action cost table, downtime allocation, faction tick triggers, save/load, UI surfacing. Replaces ambient "Day —" placeholder in Town screen.

## 1. Model

Single monotonically-increasing **world turn** counter `WorldTurn: int`. Advances on specific actions; nothing real-time.

Campaign length: 35 turns (per `docs/design/09` three-act structure). Phase 1.5 = 15 turns. Phase 1 = uncapped (no narrative time pressure yet).

### 1.1 Time abstraction

A "turn" represents ~half a day. Two turns = one in-fiction day. Used loosely — UI shows "Day {⌈turn/2⌉}" + half-day indicator.

## 2. Cost table

Actions advance turns by these amounts:

| Action | Cost |
|---|---|
| Travel between adjacent overworld nodes | 1 turn (Phase 2 baseline; Pathfinder branch −1, min 0) |
| Enter a dungeon | 0 turns (entry is free) |
| Complete a dungeon (return to town from dungeon) | 1 turn |
| Dungeon exploration step | 0 turns (intra-dungeon time is abstracted) |
| Combat | 0 turns (intra-combat is abstracted) |
| Rest at inn | 1 turn |
| Downtime action | 0 turns (happens within a town visit; capped to 1 per character per visit) |
| Save game | 0 turns |
| Town shopping / dialogue | 0 turns |
| Investigate a rumor (no Ashmouth) | 1 turn |
| Wait (manual) | 1 turn |

Phase 1: only Rest (1) and Complete-dungeon (1) advance.

### 2.1 Class modifiers

| Class/branch | Modifier |
|---|---|
| Marcher (Pathfinder) | Travel −1 (min 0) |
| Marcher (any) | Investigate −1 |
| Ashmouth (Broker) | Rumor investigation: 0 turns when in their network |
| Hollow (Fader) | Lay Low: 1 turn instead of 2 to reduce heat |

Modifiers apply to whichever party member is selected as the "leader" (Phase 2 selection UI; Phase 1 = always first party member alive).

## 3. Turn events

Turn boundary fires a set of subscribers in fixed order:

```
WorldTurn++
  → Mission system: evaluate `turn_count` triggers
  → Faction tick (Phase 2): advance state machines (see docs/design/09 Faction AI Architecture)
  → Bloom sample decay (per inventory spec)
  → NPC schedules (Phase 2): NPCs move locations, dialogue trees update
  → Rumor expiry (Phase 2): rumors past freshness window lose verification value
  → Heat decay (Phase 2): Hollow heat -1
  → Component decay markers updated
  → Event bus: emit `turn_advanced` for UI / analytics
```

Order matters — missions evaluated FIRST so faction reacts to mission completion that turn.

## 4. Engine

`src/engine/RPC.Engine/World/`:

```
World/
  WorldClock.cs           // current turn, advance + history
  TurnSubscriber.cs       // interface; engines register
  TurnHistory.cs          // log of events per turn for replay/epilogue
```

`GameState`:

```csharp
public int WorldTurn { get; private set; }
public TurnHistory TurnHistory { get; } = new();
public void AdvanceWorldTurn(int amount = 1, string cause = "");
```

`AdvanceWorldTurn` walks subscribers, then writes a `TurnEvent` record:

```csharp
public record TurnEvent(int Turn, string Cause, DateTime At, List<string> Effects);
```

## 5. Server actions

| Action | Cost | Notes |
|---|---|---|
| `rest` (existing) | 1 turn | already in code; wire to AdvanceWorldTurn(1, "rest") |
| `return_to_town` (existing) | 1 turn IF coming from dungeon completion | not for fleeing mid-dungeon (0) |
| `wait` | 1 turn | menu-only, used for forcing faction events |
| `travel` | varies | Phase 2 with overworld |
| `investigate_rumor` | varies | Phase 2 |
| `downtime` | 0 turn | new action; payload `{characterId, activity}`; capped per-visit |

`event:state_update` always includes `worldTurn`; UI binds reactively.

## 6. Downtime allocation

Per `docs/design/05` §Downtime: one activity per character per town visit. Activities:

| Activity | Effect | Available to |
|---|---|---|
| Rest | full HP, clear temp stat penalties | all |
| Train | +25% XP toward next level | all |
| Craft | convert materials → components/consumables | Fieldwright, Bonewarden |
| Network | +5 rep with one faction | Ashmouth (Broker) |
| Investigate | verify one rumor | Inkblood, Hollow (Rumor) |
| Lay Low | reduce Hollow heat by 30 | Hollow |
| Tend Blooms | stabilize 1 bloom sample | Heretic |

Server tracks `town.visitId` (incremented every entry). Per-character `lastDowntimeVisitId` prevents repeats. Activity does NOT advance world turn (it's within the visit), but Rest still does because Rest is a separate `rest` action that advances 1 turn AND auto-applies "rested" effect.

Server action `downtime`:

```json
{ "type":"downtime", "characterId":"guid", "activity":"train" }
```

Validates: character alive + in town + visitId != lastDowntimeVisitId + activity available to class.

## 7. UI

### 7.1 TopBar clock badge

Top-right of TopBar (next to save indicator):

```
┌─────────────┐
│ Day 4   ↑   │
│ turn 7 / 35 │
└─────────────┘
```

Hover/tap → tooltip with `TurnHistory` summary of last 3 turns:

```
Turn 7 — Rest at Inn
Turn 6 — Completed Foothold at Broken Engine
Turn 5 — Travel to Old Calder
```

### 7.2 Mission-board hint

Each mission card shows time pressure hints (Phase 2):
- "Expires turn 12" (5 turns from now)
- "Faction acts turn 22"
- "Mastermind reveal at turn 25"

### 7.3 Downtime UI

In Town screen, party panel shows per-character downtime status:

```
Kael — Bonewarden Lv 2
   ⏳ Downtime: [Rest] [Train] [Craft]
```

Used activities show greyed out with checkmark. Each character can do one action per visit. Action picks expand a sub-modal for activity-specific config (e.g., Network → pick faction).

### 7.4 Indicators

- Brass clock icon next to turn count.
- Pulsing amber dot when next faction tick is ≤2 turns away (Phase 2).
- Red border on turn badge when within 3 turns of campaign end.

### 7.5 Flash alerts

- Turn advance → small toast "Turn {n} — {cause}".
- Mission expiring next turn → red-bordered modal "{mission} expires after this turn — complete or abandon?".
- Faction tick → centered narrative card (Phase 2) describing what changed in the world.
- Campaign end approaching (turn ≥ 30) → persistent banner "The reckoning approaches".

## 8. Save / load

`SaveData`:

```csharp
public int WorldTurn { get; set; }
public List<TurnEventData> TurnHistory { get; set; }   // capped at 100 most recent
public int TownVisitId { get; set; }
public Dictionary<Guid,int> LastDowntimeVisitId { get; set; }
```

Save version → `"6"` (after dialogue).

## 9. Configuration

`content/world/clock.json`:

```json
{
  "campaignLength": 35,
  "phaseLengths": { "phase1": null, "phase1_5": 15, "phase2": 35 },
  "costs": {
    "rest": 1,
    "complete_dungeon": 1,
    "travel_default": 1,
    "investigate_default": 1,
    "wait": 1
  },
  "classModifiers": {
    "marcher.pathfinder.travel": -1,
    "marcher.investigate": -1,
    "ashmouth.broker.rumor_network": -1,
    "hollow.fader.lay_low": -1
  }
}
```

Hot-loadable for Phase 2 tuning without code change.

## 10. Determinism

Turn order across subscribers is fixed (mission → faction → decay → npc → rumor → heat). Within each subscriber, internal ordering is deterministic by id sort. Replay safe.

## 11. Tests

- xUnit: turn advancement applies all subscribers in declared order.
- xUnit: downtime per-visit cap.
- xUnit: class modifier resolution.
- xUnit: mission `turn_count` triggers fire at correct turn.
- Playwright: rest at inn → turn badge increments → save → reload → turn preserved.

## 12. Out of scope

- Real-time clock (the entire system is turn-based; no wall-clock advancement).
- Multi-leader travel modifiers (Phase 2 leader-selection spec).
- Time travel / rewind (replay is informational, not gameplay rewind).
- Player-set turn pacing (no fast-forward; would undermine pressure).
