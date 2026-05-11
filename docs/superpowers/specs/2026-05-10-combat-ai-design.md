# Combat AI — Design Spec
Date: 2026-05-10
Status: design — replaces shallow behavior in `src/engine/RPC.Engine/Combat/CombatEngine.cs`
Depends on: `docs/design/06-combat-system.md` §Enemy Design; combat-state-extension spec
Scope: enemy decision model, target-selection scoring, retreat thresholds, group coordination, action budgeting. Data-driven via per-enemy AI profiles. Phase 1 covers 3 enemy archetypes; spec scales to Phase 2/3.

## 1. Goals

- Decisions reproducible from `GameRandom` seed (snapshot tests).
- All enemies share a single decision pipeline; differences are data (profile) not code.
- Tactical without being telepathic — AI works from "visible" information only (Phase 2 stealth mechanic relies on this).
- Cheap: per-turn AI <1ms for 8-combatant encounter.

## 2. Decision pipeline

For each enemy turn:

```
1. Gather situation:    SnapshotCombat()      → SituationView
2. Pick stance:         profile.StanceFor(view)→ Stance
3. Score actions:       for each ability:
                          score = sum(profile.Heuristics, view, ability)
4. Pick target:         for top-3 actions, score targets, pick highest
5. Apply jitter:        random within ±jitter to break ties
6. Emit CombatAction
```

Top-1 selection is deterministic per `GameRandom` thread; jitter uses RNG.

## 3. Data: enemy AI profile

`content/enemies/<id>.json` adds an `ai` section:

```json
{
  "id": "bone_archer",
  "name": "Bone Archer",
  "stats": { ... },
  "abilities": [
    { "id":"longbow", "kind":"attack", "range":"long",
      "damage":"1d8+2", "cost":{} },
    { "id":"hold_ground", "kind":"defend", "range":"self",
      "effects":["+2 DEF this round"] },
    { "id":"retreat", "kind":"reposition", "range":"self",
      "effects":["band -1"] }
  ],
  "ai": {
    "archetype": "ranged_kiter",
    "stances": [
      { "name":"engage",  "when":{"selfHpPct":">50","alliesAlive":">=1"} },
      { "name":"flee",    "when":{"selfHpPct":"<=25"} },
      { "name":"defensive","when":{"meleeAdjacent":"true"} }
    ],
    "heuristics": {
      "engage": {
        "preferTargets": ["lowest_hp_back_row","most_threatening_caster"],
        "preferActions": ["longbow"],
        "avoid": ["melee_range_target"]
      },
      "defensive": {
        "preferActions": ["retreat","hold_ground"]
      },
      "flee": {
        "preferActions": ["retreat"],
        "willFleeCombat": true
      }
    },
    "groupTags": ["bone","archer"],
    "moraleThreshold": 25,
    "panicChance": 0.3
  }
}
```

### 3.1 Stance

A stance is a named decision context. The first matching `when` clause wins. Each stance picks from its heuristics block.

`when` clauses are simple predicates over `SituationView`:

```
selfHpPct   <|<=|>|>=|==  number
allyDownCount  op n
enemyMeleeCount op n
meleeAdjacent  bool
roundNumber  op n
hasStatus     statusId
```

### 3.2 Heuristic primitives

Target scoring primitives (composable, ordered):

| Key | Scoring |
|---|---|
| `lowest_hp_back_row` | weight = (1 - hpPct) × 1.4 if target in back row |
| `lowest_hp_any` | weight = (1 - hpPct) |
| `highest_threat` | weight = target.threatScore (computed §4) |
| `most_threatening_caster` | weight = (1 - hpPct) × 1.2 if target.class in ["bonewarden","inkblood","cauterist"] |
| `nearest_band` | weight inversely proportional to band distance |
| `marked` | weight ×= 1.5 if target has `marked` status |
| `wounded` | weight ×= 1.3 if hpPct < 0.5 |
| `prefer_player` | enemies prefer party members over ally constructs |

Action scoring primitives:

| Key | Score |
|---|---|
| `damage_potential` | expected damage × hitChance |
| `cost_efficient` | damage / resource cost (1 if free) |
| `panic_attack` | flat 0.5 for any attack when stance=flee |
| `support_ally` | targets ally; weight = (1 - allyHpPct) × 1.5 |
| `range_match` | weight 0 if ability range cannot reach any viable target |

`preferActions` and `preferTargets` arrays seed which primitives apply per stance. Engine resolves the list → weighted score → top action+target pair.

### 3.3 Archetypes (preset bundles)

`archetype` is a shortcut — engine loads a default profile and the json `ai` block overrides keys. Archetypes:

| Archetype | Default behavior |
|---|---|
| `aggressive_melee` | rush front row, lowest_hp_any, no flee |
| `tactical_melee` | focus back row when reachable, defend at low HP, retreat at 25% |
| `ranged_kiter` | back-row attacks, retreat on melee adjacency |
| `caster_support` | buff allies, debuff strongest party member, never engage melee |
| `bloom_chaotic` | random target weighted by HP, mutates abilities mid-fight, no morale |
| `construct_guard` | targets nearest, prioritizes attacker of "ward" tile, slow turns |
| `unaccounted` | (Phase 3) rule-breaking, ignores normal stances |

## 4. SituationView

Per-turn snapshot passed to decision functions:

```csharp
public record SituationView(
    Combatant Self,
    Combatant[] Allies,           // friendly side, alive only
    Combatant[] Enemies,          // party, includes Downed/Stabilized
    int Round,
    Dictionary<Guid,double> ThreatScores,
    Dictionary<string,int> StatusByTarget,
    int Band                       // self band: 0=melee, 1=short, 2=long
);
```

### 4.1 Threat score

`threatScore` per party member updates each turn:

```
baseThreat   = recent_damage_dealt_to_self_team * 1.0
              + healing_dealt * 0.8
              + buffs_applied * 0.5
              + class_weight (caster 1.5, healer 1.4, front 1.1, rogue 1.2)
decay each round by 0.8
```

Recent damage tracked in 3-round window. Encourages enemies to focus damage dealers / healers without being obvious "all attack the same target" deathball.

## 5. Group coordination

Enemies in same encounter share a small **shared blackboard**:

```csharp
public record EncounterBlackboard {
  Dictionary<Guid,int> AttackerCountsByTarget;  // how many allies already chose to attack each target this round
  HashSet<Guid> MarkedTargets;                  // marked by an ally's ability
  bool AnyFleeing;
}
```

Heuristic modifiers:
- `overkill_penalty`: -0.3 per ally already targeting this party member this round (avoids deathball).
- `marked_bonus`: +0.5 if blackboard marks the target (Stalker mark coordination).
- `morale_drop`: if `AnyFleeing && self.moraleThreshold > 0`, increase chance to also flee.

Blackboard rebuilt per round; carries no info across rounds.

## 6. Morale & flee

Morale = HP pct. When self HP ≤ `moraleThreshold`:
- `panicChance` (0.0–1.0) rolled once. If passes, enemy transitions to a hidden "panic" stance for the rest of combat — always picks `retreat` or `flee_combat` action.
- Otherwise normal `flee` stance from profile applies (may still defend or take desperate shots).

Flee resolution:
- `retreat` ability: enemy shifts one band away (e.g., melee → short → long).
- `flee_combat` ability: removes enemy from combat. They count as defeated for mission progression but yield 50% XP and 25% loot. Mission triggers like `kill_enemy_type` do NOT fire for fleers; `defeat_encounter` still fires if encounter fully resolved (all fled or dead).

Faction soldiers per doc 06 retreat when outmatched — `tactical_melee` profile sets `moraleThreshold: 35, panicChance: 0.4`.

Bloom creatures per doc 06 are unpredictable — `bloom_chaotic` profile sets `moraleThreshold: 0` (no flee).

Engine constructs per doc 06 are mechanical — `construct_guard` sets `moraleThreshold: 0`.

## 7. Action budget

Phase 1: one action per turn (matches doc 06 base rule). Quick action slot reserved but unused Phase 1.

Phase 1.5: quick action slot enables (use item, swap stance, drink potion). AI uses quick action when:
- self has a consumable that would meaningfully heal/buff (heuristic: HP < 50% + has potion → use potion as quick action then attack).
- self has a stance ability that improves following action's expected score.

Reactions (Phase 2): counter-attack on adjacent strike. Defined per ability, off in Phase 1.

## 8. Engine integration

New folder `src/engine/RPC.Engine/Combat/Ai/`:

```
Ai/
  AiProfile.cs              // record from content
  AiArchetypes.cs           // built-in defaults
  SituationView.cs
  ThreatTracker.cs          // 3-round window, decay
  EncounterBlackboard.cs
  Heuristics.cs             // target + action scoring primitives
  AiDecider.cs              // main pipeline
```

`CombatEngine.cs` modifications:
- Replace current `if (!combatant.IsPlayer)` branch in `Tick` that picks ad-hoc actions with `AiDecider.Decide(view, rng)` returning `CombatAction`.
- Maintain `ThreatTracker` in `CombatState`. Reset on combat enter.
- Maintain `EncounterBlackboard` in `CombatState`. Rebuilt at each round boundary.

`CombatState` additions:

```csharp
public ThreatTracker Threats { get; init; } = new();
public EncounterBlackboard Blackboard { get; init; } = new();
```

## 9. Phase 1 enemy profiles

Three archetypes wired:

- `rat` (`content/enemies/rat.json`): `aggressive_melee`, moraleThreshold 0, no flee — basic test.
- `goblin_scavenger`: `tactical_melee`, moraleThreshold 30, panicChance 0.5, prefers wounded targets.
- `bone_archer`: `ranged_kiter`, moraleThreshold 25, retreats from melee adjacency.

Each gets an `ai` block in its JSON. Engine validates: if `archetype` unknown OR referenced `abilities` missing → content-pack compile error.

## 10. Tests

- xUnit deterministic per seed: same `SituationView` + same `GameRandom` seed → identical action. 50 cases.
- xUnit per archetype: 10 scripted combat snapshots verifying behavior (kiter retreats from melee, support targets lowest-HP ally, etc.).
- xUnit invariant: AI never picks an ability the enemy lacks; never targets out-of-range; never targets dead/stabilized allies.
- xUnit blackboard: focus-fire suppressed across 4-enemy attack on same target without over-coordination.
- Snapshot regression: existing combat snapshot tests (T25) updated to new AI; goldens regenerated once.

## 11. Tooling

- `tools/ai-sim/` — new CLI: load enemy profile + party + N rounds → print action stream + final HP. Used for tuning.
- `tools/ai-sim/` outputs CSV of `round, actor, ability, target, damage, hpAfter` for spreadsheet analysis.

## 12. Phase 2/3 extensions

- **Faction tactics** — encounter-level orders (e.g., "protect the leader", "set traps in retreat"). Adds a top-level `encounter.tactics` field that biases blackboard.
- **Synergy awareness** — enemies use class synergies too. Their combinations are authored alongside party synergies.
- **The Unaccounted** — special archetype that bypasses initiative ordering, range bands, and death rules per doc 06. Implemented as decorator over normal AiDecider that overrides timing rules.

## 13. Out of scope

- Reinforcement learning / neural AI.
- Cross-combat persistent memory of player.
- Bluffing / feigning retreat (Phase 3 if it earns its keep).
- Player-controlled summons act under same AI by default — Phase 1.5 summons get a smaller "minion" profile.
