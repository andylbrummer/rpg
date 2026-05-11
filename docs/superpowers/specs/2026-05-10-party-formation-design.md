# Party Formation — Design Spec
Date: 2026-05-10
Status: design — formalizes front/back row rules referenced across combat, screens, design docs
Depends on: combat-ai, ability-system, combat-state-extension
Scope: row mechanics, targeting rules, line-of-effect, row-shift actions, bench, formation swap. Phase 1 = 2+2; Phase 2 = 3+3.

## 1. Geometry

Party occupies a 2-row arrangement against enemy groups in range bands:

```
        [Party Front Row]   ←melee band
        [Party Back Row]    ←protected behind front
        
        [Enemy Group A]     ←melee
        [Enemy Group B]     ←short
        [Enemy Group C]     ←long
```

Phase 1: 4 slots (front: 2, back: 2). Phase 2: 6 slots (front: 3, back: 3).

`PartyState.Members` is an array of length 4 (Phase 1) / 6 (Phase 2). Index 0..frontCount-1 = front; rest = back.

## 2. Targeting rules

### 2.1 Melee attacks (range = melee)

- Front-row enemies target party front row only.
- If front row is entirely Downed/Dead/Stabilized: melee enemies advance and target back row directly. Emit `event:fx`backline_exposed`.
- Reverse for player melee: cannot target enemy back row unless front group eliminated or has `reach_through` flag.

### 2.2 Ranged attacks (range = short, long)

- Can target any row of opposing side regardless of front-row status.
- Range band determines availability: long-range abilities reach long band; short-range cannot.

### 2.3 Same-row abilities

Some abilities require same-row targeting (e.g., Cauterist heal: front-row character's special ability heals only fronts). Authored via `targeting.filter:["same_row"]` per ability-system spec.

## 3. Front-row exposure rule

When a melee attack would target front row but all front members are Downed/Dead/Stabilized:

```
1. Compute exposureCount = countOf(front row in {Downed,Dead,Stabilized})
2. If exposureCount >= frontCount: back row becomes melee-targetable.
3. Emit `event:fx exposed{newTargetRow:"back"}`.
4. Next round at round_start, attempt auto-row-shift (§4) before initiative.
```

Stabilized counts as front-occupant for protection purposes the round it stabilized (no instant exposure on stabilize). Phase 1 simplification: Stabilized counts as occupying the slot but cannot draw aggro.

## 4. Auto row-shift

Round start: if any front-row slot has a Dead character AND any back-row character is Healthy, surfacing prompt:

- Auto-shift: a Healthy back-row character moves to front (player picks if multiple eligible).
- Phase 1: auto-pick the leftmost healthy back-row character; emit toast "Sera moves to the front line."
- Phase 1.5: player picks via brief modal during round_start phase.

This mechanic prevents permanent loss of front-line presence after a death.

## 5. Swap row action

Existing server action `swap_row` (Phase 1) lets player rearrange party in town.

```json
{ "type":"swap_row", "characterId":"...", "row":0 }
```

Rules:
- Town: free, any time.
- Exploration: free, takes 0 turns but cannot be done in active combat round.
- Combat: takes one standard action; not free. The character moves to the other row; reorders both rows' indices.

Phase 1 implementation in code; expand to combat-action variant Phase 1.5.

## 6. Formation actions (Phase 1.5+)

| Action | Effect |
|---|---|
| `advance` (group) | enemy/ally group shifts one band closer (long → short, short → melee) |
| `retreat` (group) | shift away |
| `cover` | front-row character grants +DEF to back-row neighbor for one round |
| `taunt` | force enemies to target caster (Stillblade Warden branch) |
| `formup` | auto-shift on round start without player prompt for rest of combat |

Available as class-specific abilities.

## 7. Bench (Phase 1.5+)

Bench: characters not in active party. Cap 12 per `docs/design/05`. Stored in `PartyState.Bench` array.

Active party in dungeon: 4 (P1) / 6 (P2). Swap with bench: town only.

Bench characters:
- Earn no XP.
- Earn no items.
- Retain HP/status frozen from last departure.
- Persist permanent injuries / stat penalties.

UI: PartyManagement screen shows bench row below active party formation (per screens spec §6).

## 8. Enemy formation mirror

Enemies have analogous front/back per encounter. Per encounter table:

```json
{
  "enemies": [
    { "enemyId":"warden_grunt", "min":2, "max":3, "row":"front" },
    { "enemyId":"warden_archer", "min":1, "max":2, "row":"back" }
  ]
}
```

Engine respects row + range band placement when spawning combatants.

## 9. Selection UI in combat

Targeting an enemy: highlight whole row + selected combatant. Out-of-range enemies dimmed.

Ability-side: if ability requires melee + only enemy melee group is exposed:
- Range-valid enemies highlighted brass.
- Invalid greyed, tooltip explains.

Tab cycles legal targets in row order. Shift+Tab reverses. `1-9` keys jump to numbered target.

## 10. Engine

`src/engine/RPC.Engine/Party/PartyState.cs` (existing) extend:

```csharp
public int FrontRowCount { get; init; } = 2;   // Phase 1; 3 Phase 2
public CharacterState[] Bench { get; init; } = Array.Empty<CharacterState>();   // cap 12
public bool IsExposed { get; private set; }    // computed: all front Down+

public void SetMember(int slot, CharacterState m);
public void SwapToBench(Guid characterId);
public void SwapFromBench(Guid characterId, int targetSlot);
```

Helpers:

```csharp
public IEnumerable<CharacterState> FrontRow();
public IEnumerable<CharacterState> BackRow();
public bool AllFrontIncapacitated();
public int Exposure();   // 0 = fully covered, frontCount = fully exposed
```

`CombatEngine` queries these for targeting validation. `EnemyAi` checks `BackRow().Any(alive)` for back-row priority heuristics (combat-ai spec).

## 11. Save / load

Formation state already in save-format spec (party + bench arrays). No new fields beyond `FrontRowCount`.

## 12. Tests

- xUnit: melee from enemy targets front; back protected.
- xUnit: all-front-Downed → back becomes target.
- xUnit: auto-row-shift after death moves leftmost healthy back forward.
- xUnit: swap_row in combat consumes action; in town it's free.
- xUnit: bench cap enforced.
- Playwright: party management screen drag-drop front↔back updates server state.
- Snapshot: 3 scenarios — front wipe + protect → back exposure works.

## 13. Out of scope

- 3-row formations (Phase 3 if pursued — current design says 2 rows).
- Diagonal positioning / individual slot positioning.
- Cover system beyond row mechanic.
- Movement during combat beyond row+band shifts.
