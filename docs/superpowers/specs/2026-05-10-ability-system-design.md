# Ability System — Design Spec
Date: 2026-05-10
Status: design — formalizes a system referenced by many specs (classes, enemies, combat, combat-ai, synergies, dialogue, journal) but never canonical
Depends on: combat-state-extension, inventory-model (component costs), combat-ai (target/cost interaction)
Scope: ability schema, cost types, targeting, effects, resolution order. Authored data; engine resolves.

## 1. Data

`content/abilities/<id>.json`:

```json
{
  "id": "bone_spear",
  "name": "Bone Spear",
  "kind": "attack",
  "actionCost": "standard",
  "resourceCost": [
    { "component": "bone_fragment", "qty": 1 }
  ],
  "targeting": {
    "scope": "enemy",
    "count": 1,
    "ranges": ["short", "long"]
  },
  "effects": [
    { "type":"damage", "amount":"1d8+STR", "damageType":"piercing" }
  ],
  "fallback": {
    "condition": { "type":"no_component", "component":"bone_fragment" },
    "resourceCost": [ { "self_hp": 4 } ],
    "note": "Blood-cast at 2× HP cost (Bonewarden)"
  },
  "cooldown": 0,
  "tags": ["bonewarden","ranged","necromantic"],
  "lore": "The first bone you raise will not be the last."
}
```

### 1.1 Kinds

| Kind | Purpose |
|---|---|
| `attack` | direct damage to enemies |
| `heal` | restore HP to allies |
| `buff` | apply positive status to allies/self |
| `debuff` | apply negative status to enemies |
| `summon` | create combatant ally |
| `dispel` | remove statuses |
| `mark` | apply marked status (synergy trigger) |
| `move` | change band / row |
| `interrupt` | reaction-style (Phase 2) |
| `utility` | non-combat (lockpick, detect, traverse) |

### 1.2 Action cost

| Cost | When |
|---|---|
| `standard` | one per turn (default) |
| `quick` | bonus action; one per turn shared (Phase 1.5+) |
| `reaction` | out-of-turn, one per round (Phase 2) |
| `free` | doesn't consume action slot (rare; movement) |

### 1.3 Resource cost types

| Type | Source |
|---|---|
| `component` | inventory item from inventory-model components |
| `self_hp` | blood magic; current HP |
| `memory` | Inkblood temp stat reduction (recovers at rest) |
| `cooldown` | turns of cooldown imposed |
| `morale` | reduce party morale (Phase 2) |
| `tithe_token` | rare; ritual abilities |

Multiple costs ANDed: ability requires all. Insufficient any → ability disabled.

`fallback`: alternate cost path when primary unavailable (Bonewarden blood-cast).

## 2. Targeting

```json
"targeting": {
  "scope": "enemy" | "ally" | "self" | "any" | "tile",
  "count": 1 | "group" | "all",
  "ranges": ["melee"|"short"|"long"],
  "filter": [ /* optional predicates */ ]
}
```

### 2.1 Filters

| Filter | Meaning |
|---|---|
| `alive` | only non-Dead targets |
| `downed` | only Downed (for stabilize) |
| `injured` | hp < max |
| `band == self` | only own band |
| `has_status:X` | has named status |
| `not_self` | exclude caster |
| `same_row` | same row as caster |

Engine intersects filters with valid targets. Empty intersection → ability disabled.

### 2.2 Scope `tile`

For utility abilities (e.g., trap-disarm). Phase 1.5+. Casts at a tile, not combatant.

## 3. Effects

Ordered list. Resolved sequentially. Each effect:

```json
{ "type": "...", ...params, "appliesIfPrev": true|false }
```

`appliesIfPrev`: if false, effect runs regardless of prior. If true (default), runs only if prior effect succeeded (e.g., damage applied → bleed applied).

### 3.1 Effect types

| Type | Params | Notes |
|---|---|---|
| `damage` | `amount` (formula), `damageType` | apply damage; respects resistances |
| `heal` | `amount` | restore HP |
| `stabilize` | — | Downed → Stabilized (combat-state spec) |
| `apply_status` | `statusId`, `duration`, `stacks?` | add status |
| `remove_status` | `statusId` or `category` | dispel |
| `summon` | `enemyId`, `band` | spawn ally combatant |
| `move_band` | `delta` | change own/target band |
| `move_row` | `target` | swap row |
| `mark` | `statusId`, `duration` | shorthand for `apply_status mark` |
| `set_flag` | combat-scope flag | local to combat |
| `play_fx` | cue key | client cue (audio-architecture) |
| `narrate` | text key | log narrative line |

### 3.2 Formulas

Damage / heal `amount` is a small expression language:

```
expr = literal | dice | stat | binop | clamp
literal = integer
dice = NdM "+|-" expr        e.g.  "1d8+2", "2d6"
stat = "STR" | "DEX" | "INT" | "WIL" | "CON" | "LVL"
binop = expr "+|-|*|/" expr
clamp = "clamp(" expr "," lo "," hi ")"
```

Examples:
- `1d8+STR` — physical attack
- `2d6+INT*2` — magic spell
- `LVL*3` — level scaling
- `clamp(WIL-target.WIL, 0, 10)` — opposed roll

Parser: small recursive-descent, ~80 LOC. Lives in `src/engine/RPC.Engine/Abilities/Formula.cs`.

### 3.3 Damage types

| Type | Resistances common |
|---|---|
| `slashing` | armor |
| `piercing` | armor, bone constructs |
| `bludgeoning` | armor, soft creatures weak |
| `fire` | bloom creatures weak, mechanical resist |
| `cold` | most creatures neutral |
| `lightning` | water-touched weak |
| `necrotic` | undead/bone resist; Stillblade resist |
| `radiant` | undead/bloom weak |
| `acid` | armor reduces over time |
| `bloom` | corrupting; mutates target |

Resistances expressed per target as `{ "fire": -50, "necrotic": +75 }` percent. Damage final = base × (1 - resist/100). Clamped 0.

## 4. Hit / miss / crit

Standard attack roll:

```
attack = d20 + attacker.DEX_mod + abilityBonus
defense = 10 + target.DEX_mod + armorBonus

if attack >= defense + 10: critical hit (damage × 1.5, all dice max-result instead of rolled)
if attack >= defense: hit
if attack <= defense - 5: miss with style (no fx)
else: miss
```

Spells that "always hit" skip the roll. Save-based effects: target rolls vs DC instead.

Critical formula handled in `Resolution.cs`. Configurable per ability:

```json
"critPolicy": "standard" | "always_hit" | "save_for_half"
```

## 5. Engine

`src/engine/RPC.Engine/Abilities/`:

```
Abilities/
  AbilityDef.cs              // from content
  AbilityRegistry.cs         // load all
  AbilityResolver.cs         // pipeline: validate → roll → effects → log
  Formula.cs                 // expression parser + evaluator
  EffectApplier.cs           // dispatcher
  DamageTypes.cs             // enum + resistance table
```

`AbilityResolver.Resolve(combatant caster, ability def, target spec, GameRandom rng)`:

```csharp
public AbilityResult Resolve(...) {
    if (!ValidateCost(caster, def)) return AbilityResult.Insufficient;
    if (!ValidateTargets(...)) return AbilityResult.NoTargets;
    PayCost(caster, def);
    var effects = new List<EffectOutcome>();
    foreach (var fx in def.Effects) {
        var prev = effects.LastOrDefault();
        if (fx.AppliesIfPrev && (prev?.Success == false)) continue;
        var outcome = ApplyEffect(fx, ...);
        effects.Add(outcome);
    }
    return new AbilityResult { Effects = effects };
}
```

Deterministic via passed `GameRandom` (determinism-replay spec).

## 6. Class abilities

`content/classes/<class>.json` references abilities by id:

```json
{
  "id": "bonewarden",
  "starterAbilities": ["bone_spear","tithe_touch"],
  "levelUnlocks": {
    "2": ["reinforced_marrow"],
    "3_branch_animator": ["bone_servant","commune_with_bones"],
    "3_branch_tithebinder": ["bone_link","share_burden"],
    "6_branch_remnant": ["pyrrhic_offering","ossuary_storm"]
  }
}
```

Level-up applies abilities by unlock keys. Branch choices add their list (per progression-system spec — separate).

## 7. Enemy abilities

Same schema. Enemy AI references ability ids in their `abilities` block (combat-ai spec). Engine resolves identically — no separate enemy ability path.

## 8. Validation

Content-pipeline:
- All ability `id` referenced from classes/enemies must exist.
- All formula expressions parse-validated at load (not runtime).
- All `damageType` strings in damage-status-system enum.
- All `apply_status` references must resolve.
- `targeting.ranges` must be valid bands.

## 9. Tests

- xUnit: formula parser + evaluator for 20 expressions.
- xUnit: damage type resistance applied correctly.
- xUnit: fallback resource cost engaged when primary missing.
- xUnit: crit threshold + bonus damage applied.
- xUnit: per-ability roundtrip — load def → resolve → assert effects.
- Snapshot: 10 ability scenarios.

## 10. Out of scope

- Reaction-style abilities (Phase 2).
- Channeled / multi-turn abilities (Phase 2).
- Counter-spell / interrupt timing (Phase 2).
- Player-defined abilities / modding (Phase 3 via content overlay).
