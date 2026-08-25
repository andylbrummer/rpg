# Progression System — Design Spec
Date: 2026-05-10
Status: design — formalizes XP, level-up, branching, stat allocation referenced across design docs
Depends on: ability-system, combat-state-extension, world-clock, quest-mission
Scope: XP sources, curve, level-up flow, branch choices at L3/L6, stat allocation, field promotion, cap, persistence.

## 1. XP sources

Per `docs/design/05` + plan:

| Source | XP |
|---|---|
| Combat victory | encounter.xpReward, split among living party participants |
| Combat with retreat (some kills) | per-enemy-killed XP × 0.5 |
| Exploration: new tile revealed (per dungeon) | 1 per tile, cap 50 per dungeon |
| Mission completion | mission.rewards.xp |
| Discovery (synergy first observation, lore document read first time) | 25 per discovery |
| Quest milestone | mission stage advance: 50 |

Inactive bench characters: 0 XP. Hard rule per `docs/design/05` (composition matters).

### 1.1 Field promotion

Per `docs/design/05`: characters below party-average level gain 50% bonus XP until within 1 level.

```
ifMember.level < (party.avgLevel - 1):
    appliedXp = baseXp * 1.5
else:
    appliedXp = baseXp
```

Recomputed per XP grant.

## 2. XP curve

Per `docs/design/09`:

| Level | XP to next | Cumulative |
|---|---|---|
| 1 → 2 | 100 | 0 |
| 2 → 3 | 250 | 100 |
| 3 → 4 | 500 | 350 |
| 4 → 5 | 800 | 850 |
| 5 → 6 | 1200 | 1650 |
| 6 → 7 | 1800 | 2850 |
| 7 → 8 | 2500 | 4650 |
| 8 → 9 | 3200 | 7150 |
| 9 → 10 | 4000 | 10350 |
| 10 | — | 14350 |

Curve flattens after L8 per design 09: "final act progression comes from gear, synergy, faction resources — not stat increases."

Phase 1 cap: 5. Phase 2 cap: 10.

`content/world/progression.json` holds curve so designers can tune:

```json
{
  "xpToNext": [100, 250, 500, 800, 1200, 1800, 2500, 3200, 4000],
  "levelCap": 10,
  "fieldPromotionMultiplier": 1.5,
  "fieldPromotionThresholdDelta": 1
}
```

## 3. Level-up flow

XP threshold crossed during combat resolution. Combat-state-extension spec already triggers per-character XP application post-combat. After XP applied:

```
1. While member.xp >= xpToNext(member.level):
   a. member.xp -= xpToNext(member.level)
   b. member.level++
   c. Apply level-up effects (see §4)
   d. Emit `fx:level_up` event
2. If branch choice pending (L3, L6) and not yet decided:
   a. Set member.pendingBranchChoice = true
   b. UI prompts player at next town visit
```

Multiple level-ups in one batch resolve sequentially. Each level emits its own fx.

### 3.1 Level-up effects (per level)

```json
{
  "level": 2,
  "hpDelta": 5,
  "abilities": ["reinforced_marrow"],   // class-dependent; from class def
  "statPoints": 1                        // +1 to one stat, player picks
}
```

Phase 1 auto-applies (no stat pick UI; default to class's primary stat). Phase 1.5 lets player pick at town.

## 4. Branch choices (L3 + L6)

Per `docs/design/05`: three branches per class, L3 unlocks first branch tier (2 options), L6 unlocks second branch tier (1 option).

`content/classes/<class>.json`:

```json
{
  "branches": {
    "L3": [
      { "id":"animator", "name":"Animator", "abilities":["bone_servant","commune_with_bones"] },
      { "id":"tithebinder", "name":"Tithebinder", "abilities":["bone_link","share_burden"] }
    ],
    "L6": [
      { "id":"remnant", "name":"Remnant", "abilities":["pyrrhic_offering","ossuary_storm"],
        "requiresL3":"any" }
    ]
  }
}
```

`requiresL3:"any"` means available regardless of L3 choice. Some L6 branches may gate on specific L3 branches (Phase 2 expansion).

### 4.1 Choice UI

Modal triggered at town entry when `member.pendingBranchChoice == true`:

```
┌─Branch Choice — Kael (Bonewarden, Lv 3)─────────────────────────┐
│                                                                  │
│ ┌─Animator─────────────┐  ┌─Tithebinder─────────────────────┐    │
│ │ Summon and command   │  │ Buff allies and debuff enemies  │    │
│ │ bone constructs.     │  │ through bone-link magic.        │    │
│ │                      │  │                                 │    │
│ │ • Bone Servant       │  │ • Bone Link                     │    │
│ │ • Commune w/ Bones   │  │ • Share Burden                  │    │
│ │                      │  │                                 │    │
│ │ [Choose Animator]    │  │ [Choose Tithebinder]            │    │
│ └──────────────────────┘  └─────────────────────────────────┘    │
│                                                                  │
│ ⚠ Permanent. Cannot be undone. Plan around expected threats.     │
└──────────────────────────────────────────────────────────────────┘
```

Confirmation double-modal required (destructive). After choice:
- Member gains branch's abilities (added to known list).
- `member.branches.L3 = "animator"`.
- Cleared pending flag.

Cancel: postpone to next town visit (still pending). Cannot enter dungeon while pending — design forces decision.

## 5. Stat allocation

Phase 1.5: each level grants 1 stat point allocatable to STR/DEX/CON/INT/WIL.

UI: small modal alongside branch choice (or each level if no branch this time).

```
[+] STR  4 → ?      [+] DEX  3 → ?
[+] CON  5 → ?      [+] INT  4 → ?
[+] WIL  4 → ?

Points remaining: 1
[Confirm]
```

Hard cap per stat: 10 (engine clamp). Min: 1 (cannot reduce).

Phase 1: auto-assign to class primary (Bonewarden→WIL, Stillblade→STR, Cauterist→INT, Hollow→DEX).

## 6. HP / resource progression

Per-level HP delta authored per class. Examples (Phase 1 ranges per `docs/design/06` balance targets):

| Class | L1 HP | L5 HP | L10 HP | Per-level pattern |
|---|---|---|---|---|
| Bonewarden | 17 | 28 | 45 | +2/level + CON bonus |
| Stillblade | 14 (back-row dmg) → 30 (after Warden) | depends on branch | | |

Resource caps grow with level — Bonewarden bone fragment slot capacity stays 1 slot but `stackCap` 20 (per inventory spec). Phase 2 may allow multi-slot.

## 7. Resurrection penalty interaction

Per combat-state spec §4: resurrection applies permanent stat penalty (`StatPenalties`). Stat allocation at level-up CANNOT offset existing penalty (penalty is permanent, applied via `EffectiveStats` computation).

Effective stat formula:

```
effective = baseStat + allocatedPoints + equipmentBonus + statusModifier - resurrectionPenalty
```

Phase 1: `baseStat` from class def; `allocatedPoints` always 0 (auto-assign Phase 1); `resurrectionPenalty` 0.

## 8. Engine

`src/engine/RPC.Engine/Character/LevelingSystem.cs` (existing) extend:

```csharp
public static class LevelingSystem {
    public static CharacterState ApplyXp(CharacterState m, int xp, ProgressionDef def);
    public static CharacterState CheckAndApplyLevelUps(CharacterState m, ClassDef cls);  // existing
    public static CharacterState ApplyBranchChoice(CharacterState m, ClassDef cls, string branchId, int tier);
    public static CharacterState ApplyStatAllocation(CharacterState m, Dictionary<string,int> points);
}
```

`CharacterState` (existing record) extend:

```csharp
public int Xp { get; init; }
public bool PendingBranchChoiceL3 { get; init; }
public bool PendingBranchChoiceL6 { get; init; }
public int PendingStatPoints { get; init; }
public Dictionary<string,string> Branches { get; init; } = new();  // "L3"→"animator"
```

## 9. Server actions

| Action | Payload | Effect |
|---|---|---|
| `branch_choose` | `{characterId, tier, branchId}` | apply branch choice |
| `stat_allocate` | `{characterId, allocations:{STR:1,...}}` | apply stat points |
| `level_up_acknowledge` | `{characterId}` | dismiss level-up modal |

## 10. Tests

- xUnit: XP curve crossing → level applied correctly + carryover XP preserved.
- xUnit: multi-level-up batches.
- xUnit: field promotion multiplier applied within threshold.
- xUnit: branch unlock at L3 + L6.
- xUnit: stat cap enforced.
- xUnit: resurrection penalty applied to effective stats.
- Playwright: level-up modal flow → branch choice → confirm → abilities added.

## 11. Out of scope

- Multi-class / prestige.
- Stat respec (permanent design choice).
- Level beyond cap (XP overflow caps at level 10).
- Prestige resets (Phase 3 if pursued; not in current vision).
