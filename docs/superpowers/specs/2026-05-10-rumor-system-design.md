# Rumor System — Design Spec
Date: 2026-05-10
Status: design — Phase 2 deliverable; Phase 1.5 ships placeholder rumor display only
Depends on: faction-system, dialogue-system, quest-mission, world-clock, field-notes-journal
Scope: rumor data model, truth state, sources, verification, expiry, planting (faction tactic), UI surfacing.

## 1. Model

Rumor = a piece of in-fiction information player hears. Has truth state hidden until verified.

```csharp
public record Rumor(
    string Id,
    string Text,
    RumorTruth Truth,            // hidden state
    string SourceNpcId,
    string? PlantedByFactionId,
    int HeardOnTurn,
    int? ExpiresAtTurn,
    bool Verified,
    string? VerifiedBy,
    List<string> Tags,           // "scheme","faction-stillness","mastermind-hint"
    string? ConnectedTo          // mission/synergy/lore/npc id
);

public enum RumorTruth { True, False, Planted, Mixed }
```

## 2. Truth states

| State | Meaning |
|---|---|
| `True` | accurate; verifying confirms |
| `False` | inaccurate; verifying disproves |
| `Planted` | deliberately false, sourced from a faction's misinformation campaign |
| `Mixed` | partial truth + partial distortion |

Player does not see state until verified. Field Notes shows status:
- Unverified: italicized + "?"
- True: brass check
- False: red strike-through
- Planted: violet "?" with faction badge
- Mixed: amber "~"

## 3. Sources

### 3.1 Authored rumors

`content/rumors/<id>.json`:

```json
{
  "id": "rumor-stillness-scouts",
  "text": "The Stillness sent scouts to Whitepeak last week.",
  "truth": "True",
  "tags": ["faction-stillness","scheme-hint"],
  "connectedTo": "scheme-cascade-failure",
  "freshness": 8,
  "sourceContexts": [
    { "npcId":"tavern-keeper-old-calder", "weight":10 },
    { "npcId":"market-stall-asher", "weight":3 }
  ]
}
```

`freshness`: how many turns the rumor remains in active pool before retiring (Phase 2 schemes can extend freshness).

### 3.2 Planted rumors

Faction state (per faction-system spec) can fire actions that plant rumors:

```json
{ "type":"plant_rumor", "rumorId":"rumor-stillness-frame-bureau", "byFactionId":"stillness" }
```

`rumorId` references a content def with `truth:"Planted"`. Tagged with planter faction. Distinguishable through verification.

### 3.3 Encounter rumors

Some dungeons have NPCs who give rumors as conversation. Dialogue action:

```json
{ "type":"hear_rumor", "rumorId":"..." }
```

Adds to journal as unverified.

## 4. Verification

How players confirm/reject a rumor:

| Method | Time/Cost | Restricted to |
|---|---|---|
| `investigate` action | 1 world turn | anyone; Inkblood/Hollow get 0 turns |
| Ashmouth `Broker` downtime activity | 0 turns | Ashmouth Broker only |
| Direct evidence in dungeon | 0 turns | found via segment-tagged loot |
| NPC dialogue | 0 turns | specific dialogue nodes |

`investigate` server action:

```json
{ "type":"investigate_rumor", "rumorId":"...", "characterId":"..." }
```

Effect:
- Mark rumor `Verified = true`.
- Update truth in Field Notes (revealed).
- If `Planted`: small reputation penalty to planter faction's opposing factions (player learns who lies).
- Emit `event:fx rumor_verified`.

## 5. Expiry

When `worldTurn > rumor.heardOnTurn + rumor.freshness`:
- Rumor expires: stays in journal as "stale".
- Unverified stale rumors cannot be acted on (won't fire missions linked to them).
- Verified stale rumors retain their truth — historical record.

Faction Investigating-phase ticks can refresh rumors connected to them, extending freshness.

## 6. Mission interactions

Quest spec `flag_set` triggers can include rumor verification flags:

```json
"triggers": [
  { "type":"flag_set", "flag":"rumor_verified:rumor-stillness-scouts" }
]
```

This lets missions key off verified rumors. Acting on an unverified rumor (e.g., investigating a faction based on hearsay) leads to authored consequences if the rumor was planted.

## 7. UI

### 7.1 Field Notes — Rumors tab

Per journal spec §2.5 + 6.1:

```
─── Rumors ───
🟡 "The Stillness sent scouts to Whitepeak."  (heard Day 3, unverified)
✓  "The Old Calder Engine is failing."          (heard Day 2, true)
✗  "The Bureau hides ledgers from us all."     (heard Day 1, false — planted by Stillness)
⌖  "A Bonewarden was last seen near the bloom." (stale)
```

Click → detail:

```
"The Stillness sent scouts to Whitepeak."

Heard from: Innkeeper, Old Calder, Day 3
Tags: faction-stillness, scheme-hint

Status: Unverified

[Investigate (1 turn)] [Send Broker (free, exalted only)]
```

### 7.2 Rumor toasts

On hearing a new rumor:

```
─ You overhear a rumor. (Field Notes updated) ─
```

Brief toast; non-blocking.

### 7.3 Tavern rumors (Phase 1.5+)

Tavern vendor (per vendor-economy spec §6) `rumorTopics` exposes a "Listen" service:

```
─ Listen at the Hollow Tavern ─    Cost: 5 gold
Pick a topic:
  • Faction movements
  • Engine status
  • Bloom growth
```

Pays 5 gold, receives 1 rumor from corresponding pool. Limit: 1 listen per visit.

## 8. Engine

`src/engine/RPC.Engine/Rumors/`:

```
Rumors/
  RumorDef.cs             // from content
  RumorState.cs           // runtime
  RumorRegistry.cs
  RumorService.cs         // hear, verify, expire, plant
```

`GameState`:

```csharp
public RumorState Rumors { get; }
```

`RumorService.HearRumor(string rumorId, string sourceNpcId, int worldTurn)`:

```csharp
if (state.Heard.ContainsKey(rumorId)) return RumorResult.AlreadyHeard;
state.Heard[rumorId] = new RumorRuntime(
    Id: rumorId, HeardOnTurn: worldTurn, SourceNpcId: sourceNpcId,
    ExpiresAt: worldTurn + def.Freshness, Verified: false, Stale: false);
emit fx.rumor_heard;
```

World-clock subscriber checks expiry each turn:

```csharp
foreach (var r in state.Heard.Values where !r.Stale)
    if (worldTurn > r.ExpiresAt) { r.Stale = true; emit fx.rumor_stale; }
```

## 9. Save / load

`SaveData`:

```csharp
public Dictionary<string,RumorRuntimeData> Rumors { get; set; }
```

Save version → `"11"` (after crafting v10).

## 10. Phase rollout

Phase 1: rumor system absent. Field Notes shows empty Rumors tab.

Phase 1.5: hearable from authored NPC dialogue. Tavern listening. Manual investigate. No planted variants yet.

Phase 2: factions plant rumors; verification reveals planter. Mission triggers tied to verification flags.

Phase 3: LLM-arranged campaign seeds initial rumors per campaign rolls.

## 11. Tests

- xUnit: hearing a rumor adds to state once; second hearing is idempotent.
- xUnit: expiry on turn advance.
- xUnit: verification reveals truth + applies side effects.
- xUnit: planted rumor verification damages planter faction's opposed factions' rep.
- Playwright: NPC dialogue node fires hear_rumor; journal updates; investigate flow completes.

## 12. Out of scope

- Player spreading rumors (Hollow Rumor branch may add Phase 2 — separate spec).
- Cross-NPC rumor propagation network simulation (deliberately authored, not simulated).
- Real-time gossip ticker.
