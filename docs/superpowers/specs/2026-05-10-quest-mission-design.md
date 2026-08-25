# Quest / Mission System — Design Spec
Date: 2026-05-10
Status: design — not yet implemented
Depends on: `docs/design/03-narrative-framework.md`, `docs/design/04-factions.md`, `docs/design/07-dungeon-design.md`
Scope: data model for missions, stages, prerequisites, completion triggers, branching outcomes, reward tables. Phase 1 = linear 3-dungeon questline; Phase 2 = full faction-aware quests with branches; Phase 3 = LLM-generated arrangement.

## 1. Concepts

- **Mission**: a unit of player-facing work with a start, stages, and end. Visible on Mission Board. Has reward.
- **Stage**: ordered or DAG'd milestone within a mission. Each stage has a **completion trigger**.
- **Trigger**: declarative condition (`when X happens, advance/fail/branch`).
- **Outcome**: branch terminal — defines rewards, world-state effects, follow-up missions unlocked.
- **Objective**: per-stage user-facing hint shown in objective ticker.

Quests are **data-driven** from Phase 1 — no hardcoded mission logic. Engine evaluates triggers against state changes.

## 2. Data model

`content/missions/<id>.json`:

```json
{
  "id": "broken-engine-foothold",
  "title": "Foothold at the Broken Engine",
  "tier": 1,
  "factionGiver": null,
  "patronOnly": false,
  "summary": "The Engine in Old Calder has gone dark. Scout the facility.",
  "lore": "Locals say the hum stopped three nights ago. Then the lights.",

  "prerequisites": {
    "minPartyLevel": 1,
    "completedMissions": [],
    "factionRep": [],
    "campaignTurn": { "min": 0, "max": null },
    "flags": []
  },

  "stages": [
    {
      "id": "enter-engine",
      "objective": "Enter the Broken Engine",
      "triggers": [
        { "type": "enter_dungeon", "dungeonType": "broken_engine" }
      ],
      "next": "find-control"
    },
    {
      "id": "find-control",
      "objective": "Locate the control room",
      "triggers": [
        { "type": "enter_segment_tag", "tag": "setpiece-control" }
      ],
      "next": "decide-engine"
    },
    {
      "id": "decide-engine",
      "objective": "Decide the Engine's fate",
      "triggers": [
        { "type": "interactable", "id": "engine_console", "choice": "repair",
          "next": "outcome-repaired" },
        { "type": "interactable", "id": "engine_console", "choice": "sabotage",
          "next": "outcome-sabotaged" },
        { "type": "interactable", "id": "engine_console", "choice": "leave",
          "next": "outcome-untouched" }
      ]
    }
  ],

  "outcomes": {
    "outcome-repaired": {
      "title": "Engine Reignited",
      "rewards": { "xp": 800, "gold": 200, "items": ["engine_tech_manual"] },
      "worldEffects": [ { "type":"set_flag", "flag":"engine_repaired_old_calder" } ],
      "factionRepDelta": [ { "faction":"fieldwright_guild", "delta": 10 } ],
      "unlocks": [ "sewers-investigation" ]
    },
    "outcome-sabotaged": {
      "title": "Engine Silenced",
      "rewards": { "xp": 1000, "gold": 100, "items": ["control_rod"] },
      "worldEffects": [ { "type":"set_flag", "flag":"engine_sabotaged_old_calder" } ],
      "factionRepDelta": [
        { "faction":"convocation", "delta": 8 },
        { "faction":"fieldwright_guild", "delta": -15 }
      ],
      "unlocks": [ "crypt-of-whispers" ]
    },
    "outcome-untouched": {
      "title": "Foothold Held",
      "rewards": { "xp": 500, "gold": 150 },
      "unlocks": [ "sewers-investigation" ]
    }
  },

  "failures": [
    { "trigger": { "type": "party_wipe" }, "outcome": "outcome-wiped" }
  ],

  "tags": ["mainline","act1","broken-engine"]
}
```

### 2.1 Trigger types

Each trigger is `{type, ...params}`. Engine matches against state events. Add types incrementally.

Phase 1 set:

| Type | Params | Fires when |
|---|---|---|
| `enter_dungeon` | `dungeonType` | dungeon enter event matches |
| `exit_dungeon` | `dungeonType?` | dungeon exit (return to town) |
| `enter_segment_tag` | `tag` | player enters segment whose tags include this |
| `enter_segment_id` | `id` | specific segment entered |
| `kill_enemy_type` | `enemyId, count?` | enemy with id killed N times this mission |
| `defeat_encounter` | `encounterId` | encounter table id fully resolved (no fleeing) |
| `interactable` | `id, choice?` | interactable activated; choice optional sub-filter |
| `pickup_item` | `itemId, qty?` | item entered any party inventory |
| `deliver_item` | `itemId, npcId, qty?` | NPC dialogue option spent the item |
| `party_wipe` | — | all party Dead |
| `turn_count` | `op, n` | `worldTurn op n` (op = `eq|gte|lte`) |
| `flag_set` | `flag` | world flag set anywhere else |
| `mission_completed` | `missionId, outcome?` | another mission ended |
| `talk_to` | `npcId, dialogueNode?` | NPC dialogue reached node |
| `level_reached` | `partyAvgLevel, op, n` | party level threshold |

Phase 2 additions: `faction_rep`, `time_window`, `escort_alive`, `bloom_contained`, `pickup_evidence`.

### 2.2 Stage advancement

- Stage triggers evaluated in declared order. First match advances mission to `next` (or sets outcome if listed).
- If stage has multiple triggers, ANY matching advances (OR semantics).
- For AND semantics: split into stage chain (stage A → stage B), or use a single trigger with composite params (e.g., `defeat_encounter` requires full resolution).
- Stage can have `optional: true` — skippable; advances by either own trigger or any downstream stage's trigger firing.

### 2.3 Outcomes

Outcome is a terminal node. Reaching it:
- Applies `rewards` to party (gold, xp, items into Cache; if full, dropped on floor with toast).
- Applies `worldEffects` (flags, encounter table modifiers, NPC state changes).
- Applies `factionRepDelta` (Phase 2 — Phase 1 logs but no faction system yet).
- Adds `unlocks` to mission catalog.
- Records outcome id in `MissionJournal` for save/replay/epilogue.

### 2.4 Failures

Mission can specify failure triggers (e.g., party wipe, target NPC killed, time exceeded). Failure outcomes are valid outcomes; they still record + may have rewards (consolation gear).

## 3. Engine

New folder `src/engine/RPC.Engine/Missions/`:

```
Missions/
  MissionDef.cs              // immutable from content
  MissionState.cs            // per-save runtime: missionId → stageId, flags, outcomes
  MissionRegistry.cs         // loads all MissionDef from content pack
  MissionEngine.cs           // event-sink, evaluates triggers, advances stages
  WorldFlags.cs              // string-keyed bool/int map, Save-persisted
```

`GameState` additions:

```csharp
public MissionState Missions { get; } = new();
public WorldFlags Flags { get; } = new();
public int WorldTurn { get; private set; }
```

`MissionEngine` subscribes to `GameState` events:

```csharp
public void OnEnterDungeon(string dungeonType);
public void OnExitDungeon(string dungeonType);
public void OnSegmentEnter(RoomSegmentRef segment);
public void OnEnemyKilled(string enemyId, int count);
public void OnEncounterDefeated(string encounterId);
public void OnItemPickup(string itemId, int qty);
public void OnInteractable(string id, string? choice);
public void OnPartyWipe();
public void OnTurnTick();
public void OnFlagSet(string flag);
public void OnNpcDialogue(string npcId, string node);
public void OnLevelReached(int avgLevel);
```

Each event call walks active missions, evaluates current stage triggers, advances accordingly. State changes emit `event:state_update` + `event:fx` `mission_progress`.

### 3.1 Mission lifecycle

| State | Meaning |
|---|---|
| `locked` | prerequisites not met |
| `available` | prerequisites met, not accepted |
| `active` | accepted, in progress at some stage |
| `completed` | reached non-failure outcome |
| `failed` | reached failure outcome |
| `expired` | turn window passed (Phase 2) |

Transitions:
- `locked → available` whenever prerequisites recheck satisfies (after flag set, mission complete, level reached, etc.).
- `available → active` on `mission_accept` action.
- `active → completed | failed | expired` via triggers.

Multiple missions active simultaneously allowed (max 8 active to avoid clutter Phase 1 — config).

### 3.2 Trigger evaluation cost

All event subscribers scan only **active** missions. Index missions by event type they care about to avoid full scan. For 8 active missions × ~3 stage triggers each, this is negligible.

## 4. Server actions

| Action | Payload | Effect |
|---|---|---|
| `mission_accept` | `{missionId}` | available → active. Validates prereqs server-side. |
| `mission_abandon` | `{missionId}` | active → available (resets stage to first). Some missions flagged un-abandonable. |
| `mission_complete_force` | `{missionId,outcome}` | DEBUG only — disabled in release builds. |

Missions evaluated reactively to other actions/events; no per-tick mission action needed.

## 5. Phase 1 starter content

Three missions hand-authored to drive the linear questline (matches Phase 1 plan T29 "3-dungeon questline"):

1. **`foothold-broken-engine`** — enter Broken Engine, find control, decide fate. Outcomes branch to next mission unlock.
2. **`whispers-of-the-sewers`** — enter Sewers, defeat the Wretched Captain encounter, retrieve `bloom_ledger`. Single outcome path Phase 1.
3. **`crypt-of-the-still`** — enter Crypt, full clear, defeat boss encounter, return. Branches: kill the Bone Marshal vs spare him (talk_to + dialogue_node trigger). Different rewards.

`content/missions/index.json` lists all available missions. Missing prerequisite content (NPCs, segments) is a hard validation error at content-pack compile time (extend RPK compiler).

## 6. UI

### 6.1 Mission Board (Town)

Replaces the dungeon-tile pattern from `2026-05-10-screens-design.md §2`. Mission Board shows missions, not dungeons. (Dungeons are accessed *via* missions.)

Layout:

```
┌─Mission Board──────────────────────────────────────────────────┐
│ Filters: [All] [Mainline] [Faction] [Side] [Completed]         │
├────────────────────────────────────────────────────────────────┤
│ ▣ Foothold at the Broken Engine                  Tier 1   ★   │
│   "The Engine in Old Calder has gone dark..."                  │
│   Reward: 800 xp · 200g · Engine Tech Manual                   │
│   [Accept]                                                     │
├────────────────────────────────────────────────────────────────┤
│ ▢ Whispers of the Sewers                         Tier 3  Locked│
│   Requires: Foothold at the Broken Engine                      │
├────────────────────────────────────────────────────────────────┤
│ ✓ Engine Reignited (Completed)                                 │
│   Outcome: Engine Reignited                                    │
└────────────────────────────────────────────────────────────────┘
```

Each card is a `<Card>` primitive with state indicator (locked / available / active / completed / failed).

Tablet portrait: full-width stacked list.

### 6.2 Active mission ticker

Top of TopBar replaces objective ticker generic text with current active mission's stage objective. If multiple active, cycles every 6s with subtle crossfade.

User can pin one as "tracked" via mission detail modal → ticker stays on it.

### 6.3 Mission Journal modal

New modal (`J` key) listing all missions by status. Each entry expands to show:
- Lore + summary
- Stage history (completed stages crossed out + chronological dates)
- Current stage objective(s)
- Outcomes already realized (if completed)
- Reward preview
- Track / Untrack / Abandon buttons

### 6.4 Flash alerts

- Mission accepted → brass toast "Mission accepted: {title}"
- Stage advanced → small brass burst around tracked objective ticker + toast "{objective}"
- Mission completed → centered modal (toast layer) with outcome title, rewards, [Continue] button
- Mission failed → bad-tinted modal with failure outcome + Continue
- Mission unlocked → notification badge on Mission Board tile in town
- Reward overflow (inventory full) → toast "Cache full — {item} dropped at your feet"

## 7. Save / load

`SaveSystem.cs`: extend `SaveData`:

```csharp
public MissionStateData Missions { get; set; }
public Dictionary<string,object> WorldFlags { get; set; }
public int WorldTurn { get; set; }
```

`MissionStateData`:

```csharp
public Dictionary<string,MissionRuntime> Active { get; set; }
public List<CompletedMission> History { get; set; }

public record MissionRuntime(string MissionId, string CurrentStageId, Dictionary<string,object> Locals);
public record CompletedMission(string MissionId, string OutcomeId, DateTime At, int WorldTurn);
```

Save Version → `"3"` after combat-state + inventory bumps. Migration v2→v3 initializes empty mission state + no flags.

## 8. Validation

Content-pack compiler extension:
- All `missionId` references in `unlocks`, `mission_completed` triggers, and prereqs must resolve.
- All `dungeonType`, `enemyId`, `itemId`, `npcId`, `segmentTag`, `interactableId` referenced in triggers must resolve to content present in the pack.
- All outcomes referenced from stages exist.
- No circular `unlocks` chains (toposort).
- No mission has both `failures` and stage path reaching same outcome id (duplicate outcome key).
- Compiler emits `mission-graph.json` artifact showing DAG; CI fails if isolated/orphan missions exist.

## 9. Tests

- xUnit: trigger matrix — for each trigger type, fixture an event and assert advancement.
- xUnit: prereq evaluation — locked vs available vs active given various state.
- xUnit: full play-through three Phase 1 missions including branch outcomes.
- xUnit: rep deltas applied to mock faction-rep service (Phase 1 stub, real Phase 2).
- Playwright: Mission Board → Accept mission → enter dungeon → trigger fires → stage advances → reward modal shows.

## 10. Out of scope

- Procedural mission generation. Phase 3 LLM arrangement re-uses authored missions, doesn't author new ones.
- Time-limited missions with real-time clock. Mission timers are in world turns.
- Multi-party / co-op assignments.
- Dynamic objective text generation. All strings authored.
- Voice acting.
