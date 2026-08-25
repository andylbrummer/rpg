# Damage Types & Status Effects — Design Spec
Date: 2026-05-10
Status: design — canonicalizes references scattered across combat-ai, abilities, design docs
Depends on: ability-system spec
Scope: damage type catalog, resistance/weakness model, status effect catalog, stacking + duration rules, dispel categories.

## 1. Damage type catalog

Ten canonical types. Adding a type is a design decision (bumps content schemas).

| Id | Display | Source examples | Common resists | Common weak |
|---|---|---|---|---|
| `slashing` | Slashing | swords, claws | mail armor (-25%) | unarmored |
| `piercing` | Piercing | spears, arrows, bone | thick hide | leather |
| `bludgeoning` | Bludgeoning | hammers, fists | armor (-15%) | brittle constructs (+50%) |
| `fire` | Fire | Cauterist abilities, hazards | wet creatures (-50%) | bloom (+75%), dry wood |
| `cold` | Cold | Stillness scholars (Phase 2) | fur (-25%) | constructs of warm flesh |
| `lightning` | Lightning | Fieldwright shocks | rubber-equivalent | water-touched (+100%) |
| `necrotic` | Necrotic | Bonewarden, undead | undead (-100%), Stillblade (-50%) | living, healers |
| `radiant` | Radiant | Cauterist purifications | living neutral | undead (+75%), bloom (+50%) |
| `acid` | Acid | bloom secretions | acid-resist coating | armor degrades (durability tick) |
| `bloom` | Bloom-touch | Heretic, bloom creatures | Stillblade (-50%) | unprotected flesh (+25%) |

Resistances expressed per target as percentages stored on character / enemy:

```json
"resistances": {
  "fire": -50,        // 50% damage reduction
  "necrotic": -100,   // immune
  "radiant": 75       // 75% bonus damage taken
}
```

Formula: `final = base × (1 - resist/100)`. Clamped to ≥ 0.

### 1.1 Type categories

| Category | Members |
|---|---|
| `physical` | slashing, piercing, bludgeoning |
| `elemental` | fire, cold, lightning |
| `arcane` | necrotic, radiant, bloom |
| `chemical` | acid |

Used for abilities that affect categories (e.g., "wards against arcane").

## 2. Status effect catalog

Status effects are time-bound modifiers on a combatant. Authored in `content/statuses/<id>.json`:

```json
{
  "id": "burning",
  "name": "Burning",
  "category": "debuff",
  "tag": "elemental",
  "iconId": "status-burning",
  "stackPolicy": "extend",
  "maxStacks": 5,
  "maxDuration": 6,
  "tickPhase": "turn_end",
  "tickEffects": [
    { "type":"damage", "amount":"2*stacks", "damageType":"fire" }
  ],
  "modifiers": [
    { "stat":"DEF", "delta":"-stacks" }
  ],
  "dispelTags": ["water","heal","fire"],
  "interactsWith": [
    { "with":"wet", "result":"cancel" },
    { "with":"oil", "result":"escalate", "newStatusId":"infernal" }
  ]
}
```

### 2.1 Categories

| Category | Use | Visual |
|---|---|---|
| `buff` | positive, dispellable by enemy abilities | brass border |
| `debuff` | negative, dispellable by ally abilities | red border, pulse |
| `neutral` | mark, stance, identifier | violet border |
| `permanent` | persistent (e.g., curse) | dark gray |
| `injury` | combat-end persists into exploration | scarred |

### 2.2 Stack policy

| Policy | Behavior on re-application |
|---|---|
| `extend` | duration reset to max(existing, new) |
| `add_stack` | stack count += new stacks (capped at maxStacks) |
| `independent` | each application is its own instance |
| `refresh` | duration set to new (replaces existing) |
| `ignore` | first application wins; reapplications no-op |

### 2.3 Tick phases

| Phase | When |
|---|---|
| `turn_start` | bearer's turn start |
| `turn_end` | bearer's turn end |
| `round_start` | round start (initiative roll) |
| `round_end` | round end |
| `on_apply` | when applied (one-shot) |
| `on_remove` | when removed (one-shot) |

Multiple statuses may tick same phase; resolved in deterministic order (status id alphabetical for stability).

## 3. Phase 1 status set

Minimum viable for Phase 1 combat:

| Id | Brief |
|---|---|
| `bleed` | turn_end damage based on stacks (physical) |
| `burning` | turn_end damage based on stacks (fire) |
| `poisoned` | turn_end damage (necrotic-tint), reduces healing received |
| `stunned` | skip next turn |
| `slowed` | initiative −5 |
| `marked` | next damage taken +50% (combat-ai uses for focus) |
| `defending` | DEF +2 until next turn start |
| `regenerating` | turn_start heal |
| `wounded` | max HP −2 until rested at inn |
| `silenced` | cannot cast abilities |
| `shielded` | flat damage reduction X for next hit |

Phase 1.5 expands: hex, blessed, bloom_touched, frozen, charmed, fear, hidden, exposed.

## 4. Engine

`src/engine/RPC.Engine/Statuses/`:

```
Statuses/
  StatusDef.cs              // from content
  StatusRegistry.cs         // loaded
  StatusInstance.cs         // runtime per combatant
  StatusEngine.cs           // tick + stack + dispel
```

`Combatant` carries:

```csharp
public List<StatusInstance> Statuses { get; init; } = new();

public record StatusInstance(
    string StatusId,
    int Stacks,
    int RemainingDuration,
    Guid Source,
    Dictionary<string,int> CustomData
);
```

`StatusEngine.Tick(phase, combat)`:

```csharp
foreach (var combatant in combat.Combatants.OrderBy(c => c.Id)) {
    foreach (var status in combatant.Statuses
                 .OrderBy(s => s.StatusId)
                 .Where(s => def.TickPhase == phase)) {
        ApplyTickEffects(combatant, status, def);
        status.RemainingDuration--;
    }
    combatant.Statuses.RemoveAll(s => s.RemainingDuration <= 0);
}
```

Stat modifiers from statuses are computed each time a combatant's effective stats are queried — no in-place mutation. Keeps recalculation cheap; never stale.

## 5. Interaction matrix

`interactsWith` allows pair behaviors (per `docs/design/06` synergy spirit, but at status level):

| Result | Behavior |
|---|---|
| `cancel` | both statuses removed |
| `cancel_target` | named target status removed; this status remains |
| `escalate` | both removed, `newStatusId` applied with combined stacks |
| `block` | this status cannot be applied while named status present |

Engine resolves on apply: if `interactsWith` matches, apply result instead of normal stack policy.

## 6. Dispel

Abilities dispel by `dispelTags`. Example:

```json
{ "type":"remove_status", "dispelTag":"elemental", "count":2 }
```

Removes up to 2 status instances matching tag. Status `dispelTags` is union — `["water","heal","fire"]` means dispelled by any of those tags' abilities.

`remove_status` by category (e.g., remove all buffs) uses `category` selector.

## 7. UI

Status chip per design-system spec §6.1:
- 28×28 square, icon centered, duration counter top-right.
- Tooltip on hover shows name, description, source, remaining duration, stacks.
- Color by category (brass buff, red-pulse debuff, violet neutral).

Stack count: badge bottom-right when > 1.

## 8. Validation

Content-pipeline:
- All referenced status ids in abilities + AI profiles + dialogue resolve.
- All `dispelTags` are documented in `content/statuses/_tags.json` enum.
- `interactsWith` references existing statuses.
- `tickEffects` use valid effect types (per ability-system spec).
- `maxStacks ≥ 1`, `maxDuration ≥ 1`.

## 9. Tests

- xUnit: stack policy matrix.
- xUnit: tick phase ordering deterministic.
- xUnit: interaction resolution (burning + wet → cancel; burning + oil → infernal).
- xUnit: dispel by tag removes correct count.
- xUnit: modifier stacking from multiple statuses sums correctly.
- Snapshot: 5 multi-status combat scenarios.

## 10. Out of scope

- Triggered statuses on specific events (e.g., "on take damage, apply X") — Phase 2 reactions.
- Conditional removal (e.g., "expires when bearer moves") — Phase 2.
- Visual particle effects per status — Phase 2 atmosphere expansion.
