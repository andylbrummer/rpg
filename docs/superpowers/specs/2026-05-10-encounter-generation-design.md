# Encounter Generation — Design Spec
Date: 2026-05-10
Status: design — current code at `src/engine/RPC.Engine/Combat/EncounterTable.cs` rolls flat-weighted; this spec replaces with tier + composition + faction-aware selection
Depends on: combat-ai, faction-system, world-clock, quest-mission
Scope: encounter table schema, weighting, escalation, pacing rules. Phase 1 hand-authored; Phase 2 faction-driven; Phase 3 LLM-arranged.

## 1. Encounter definition

`content/encounters/<table-id>.json`:

```json
{
  "id": "broken-engine-low",
  "dungeonType": "broken_engine",
  "tier": 1,
  "encounters": [
    {
      "id": "rat-pair",
      "weight": 30,
      "enemies": [ { "enemyId":"rat", "min":1, "max":3 } ],
      "tags": ["bloom-light","trash"],
      "requires": { "partyAvgLevel": { "max": 3 } }
    },
    {
      "id": "goblin-scouts",
      "weight": 20,
      "enemies": [
        { "enemyId":"goblin_scavenger", "min":2, "max":3 },
        { "enemyId":"rat", "min":0, "max":2 }
      ],
      "tags": ["faction-light","trash"]
    },
    {
      "id": "bone-archer-pair",
      "weight": 10,
      "enemies": [
        { "enemyId":"bone_archer", "min":1, "max":2 },
        { "enemyId":"goblin_scavenger", "min":1, "max":1 }
      ],
      "tags": ["faction-medium","ranged-pressure"],
      "requires": { "partyAvgLevel": { "min": 2 } }
    },
    {
      "id": "engine-construct-guard",
      "weight": 5,
      "enemies": [ { "enemyId":"engine_construct_alpha", "min":1, "max":1 } ],
      "tags": ["setpiece","construct"],
      "requires": { "flag": "setpiece-encounter-slot" }
    }
  ],
  "fallback": "trash-fallback",
  "pacingRules": {
    "minTurnsBetween": 3,
    "maxConsecutiveTag": { "tag":"trash", "n":3 }
  }
}
```

### 1.1 Fields

- `tier` — dungeon difficulty band. Used to gate the table by party level.
- `encounters[].weight` — base selection weight.
- `min`/`max` per enemy spawn — count rolled uniformly.
- `requires` — conditional filter (predicate, same dialect as dialogue/missions).
- `tags` — for pacing + faction filtering.
- `fallback` — id of safe trash encounter if all primary picks filtered out.
- `pacingRules` — anti-monotony constraints.

## 2. Selection algorithm

`EncounterTableRegistry.Roll(tableId, ctx)`:

```
1. Load table T.
2. Filter `T.encounters` by `requires` predicates against ctx (party level, world flags, faction phase, etc.).
3. Apply pacing rules:
   - Remove encounters whose last-spawned turn is within `minTurnsBetween`.
   - Remove encounters whose dominant tag would exceed `maxConsecutiveTag.n` streak.
4. If filtered list empty: load `fallback`.
5. Apply weight adjustments per §3.
6. Weighted-random pick using `EncounterRng`.
7. Roll per-enemy counts uniform within [min,max].
8. Record `lastSpawnedTurn` for the pick.
9. Return concrete `EncounterDef`.
```

`EncounterRng` derived from RngTree (determinism-replay spec §2).

## 3. Weight adjustments

Modifiers applied to base weight:

| Modifier | Effect |
|---|---|
| Party-level mismatch | if party avgLvl outside `[tier-1, tier+2]`, weight × 0.3 |
| Faction-phase match | if encounter tag matches an Executing faction, weight × 2 |
| Faction-phase opposite | if encounter tag matches an Investigating faction with low rep, weight × 0.5 |
| Recently used | per-encounter cooldown linear back to base over 5 turns |
| Mission-injected | quest-spawned encounter overrides table entirely; bypasses random pick |
| Bloom-zone bias | in bloom-tagged segment, bloom-tag encounters × 2 |

Modifiers are multiplicative; order independent. Multipliers logged for replay.

## 4. Step-and-trigger

Per `GameState.TryMoveForward`:

```
_stepsSinceEncounter++
chance = baseChance + perStepBonus * _stepsSinceEncounter
chance = clamp(chance, 0, maxChance)
if Roll() < chance:
    Encounter = RollEncounter(currentTableId)
    _stepsSinceEncounter = 0
```

Tunables (`content/world/clock.json` neighbor):

```json
"encounterPacing": {
  "baseChance": 0.05,
  "perStepBonus": 0.06,
  "maxChance": 0.55,
  "minStepsAfterEncounter": 2
}
```

`minStepsAfterEncounter` enforces post-combat grace period (encounter chance = 0 for N steps).

Current code (GameState.cs:127) already has this pattern at `0.05 + steps*0.08`. Replace constants with config.

## 5. Setpiece encounters

Tiles tagged with `setpiece-encounter` in segment data spawn a specific encounter (not table-rolled). Set via `setpieceEncounter: "engine-construct-guard"` on the segment. Mission triggers can require a setpiece for stage advancement.

Setpiece tiles bypass step-and-trigger; they fire on player **entering** the tile, not on step count.

## 6. Tagging discipline

Tags drive pacing and faction logic. Discipline:

| Tag namespace | Use |
|---|---|
| `trash` / `standard` / `elite` / `setpiece` | difficulty role |
| `bloom-light` / `bloom-heavy` | bloom category |
| `faction-X-light` / `faction-X-medium` / `faction-X-heavy` | faction presence + intensity |
| `ranged-pressure` / `melee-press` / `swarm` | tactical type |
| `unaccounted` | Phase 3 Unaccounted enemies |

Pacing rule: avoid `>=3 trash in a row`, force `standard or elite` next.

## 7. Phase rollout

Phase 1: 3 dungeons × ~5 encounter table entries each. Hand-authored. Step trigger uses pacing. No faction adjustment yet (faction system absent).

Phase 1.5: faction adjustment (Bureau, Convocation). Setpiece slots wired.

Phase 2: full faction phase reactivity. Mission injection overrides. Bloom escalation in bloom dungeons.

Phase 3: LLM selects encounter table assignment per dungeon at campaign generation; encounter table content unchanged.

## 8. Engine

`src/engine/RPC.Engine/Combat/EncounterTable.cs` (existing) extend:

```csharp
public class EncounterTableRegistry {
    public EncounterDef RollEncounter(string tableId, EncounterContext ctx, GameRandom rng);
}

public record EncounterContext(
    int PartyAvgLevel,
    Dictionary<string,FactionPhase> FactionPhases,
    Dictionary<string,int> FactionRep,
    int WorldTurn,
    Dictionary<string,int> RecentEncounters,    // encounterId → lastTurn
    Dictionary<string,int> RecentTags,          // tag → consecutiveCount
    HashSet<string> SegmentTags
);
```

Pacing state (`RecentEncounters`, `RecentTags`) persisted in `GameState.PacingHistory` to survive save/load.

## 9. Mission injection

Quest spec triggers `defeat_encounter` and `enter_segment_id` can pre-place encounters:

Mission injects via action effects:

```json
{ "type":"force_next_encounter", "encounterId":"bandit_ambush" }
```

Engine queues this; next step-and-trigger fires the injected encounter instead of rolling. Cleared after firing.

## 10. Tests

- xUnit: weighting deterministic under seed.
- xUnit: filtering by `requires` predicates correct.
- xUnit: pacing rule prevents 4 consecutive trash.
- xUnit: cooldown decay restores weight over 5 turns.
- xUnit: faction-phase weight multiplier applies correctly.
- xUnit: setpiece bypasses step trigger.
- Manual playtest: dungeon traversal feels paced, no monotonous chains.

## 11. Out of scope

- Difficulty selection by player (single canonical balance).
- Random table reroll on player flee (treats flee as encounter resolution).
- Multi-table layering (single table per dungeon segment).
- Procedural enemy stat scaling (enemies are authored at fixed stats; tables select among them).
