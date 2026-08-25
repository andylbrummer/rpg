# Crafting — Design Spec
Date: 2026-05-10
Status: design — Phase 1.5 deliverable per `docs/design/05` downtime activity
Depends on: inventory-model, world-clock (downtime), progression-system (class gates)
Scope: recipes, components → outputs, restricted classes, station requirements, save persistence.

## 1. Scope

Per `docs/design/05` Downtime: Fieldwright + Bonewarden can `Craft` to convert raw materials into components/consumables. Phase 1.5 adds minimal crafting via downtime; Phase 2 may expand to free-form crafting at smith/apothecary station.

## 2. Recipe schema

`content/recipes/<id>.json`:

```json
{
  "id": "recipe-bone-fragment-from-bones",
  "name": "Refine Bone Fragments",
  "kind": "downtime",
  "classes": ["bonewarden"],
  "branchRequirement": null,
  "stationRequirement": null,
  "inputs": [
    { "itemId":"raw_bone_dust", "qty":3 }
  ],
  "outputs": [
    { "itemId":"bone_fragment", "qty":2 }
  ],
  "outputBonus": {
    "type":"per_stat", "stat":"WIL", "perN":3, "extraQty":1
  },
  "discovery": "starter",
  "lore": "The first cut removes the marrow; the second, the memory."
}
```

### 2.1 Fields

| Field | Meaning |
|---|---|
| `kind` | `downtime` (one per visit) or `station` (anytime in town) |
| `classes` | Which classes can perform |
| `branchRequirement` | Optional branch gate |
| `stationRequirement` | Where (Phase 2 — `smith`, `apothecary`, etc.) |
| `inputs` | Required items, consumed from inventory |
| `outputs` | Items produced |
| `outputBonus` | Optional scaling — e.g., +1 extra output per 3 WIL above 10 |
| `discovery` | When recipe becomes known — `starter`, `level_2`, `quest:<id>`, `item:<id>` |

## 3. Discovery

Recipes are not all known at start. Per `discovery`:

| Trigger | Effect |
|---|---|
| `starter` | known from L1 |
| `level_N` | unlocked at level N |
| `quest:<missionId>` | unlocked on mission completion |
| `item:<itemId>` | unlocked when character first picks up an associated item (e.g., recipe book) |
| `npc:<nodeId>` | unlocked through dialogue node |

`CharacterState`:

```csharp
public HashSet<string> KnownRecipes { get; init; } = new();
```

Discovery emits `event:fx recipe_learned` toast + Field Notes entry.

## 4. Crafting action

Server action `craft`:

```json
{ "type":"craft", "characterId":"...", "recipeId":"...", "iterations":1 }
```

Validates:
- Recipe known by character.
- Character class + branch matches recipe requirements.
- For downtime: character has not used downtime this visit (per world-clock spec §6).
- Station available if required.
- Inputs available in character backpack OR cache (preference: backpack first to keep cache for shared items).
- Iterations × inputs available.
- Output destination has space (cache → backpack → drop fail).

Atomic: consume inputs, produce outputs. Iterations process sequentially; partial completion respected if inputs run out mid-batch.

Side effects:
- For downtime: marks `LastDowntimeVisitId` to prevent re-use.
- For station: no downtime consumption.
- XP: small grant (10 XP) per successful craft (Phase 2 toggle).

## 5. Recipe categories

Initial recipe set Phase 1.5 (8 recipes):

| Recipe | Class | Phase |
|---|---|---|
| Refine Bone Fragments (raw bone → bone fragment) | Bonewarden | 1.5 |
| Brew Healing Draught (herbs → consumable) | Cauterist | 1.5 |
| Assemble Cautery Kit (supplies → cautery supply) | Cauterist | 1.5 |
| Fabricate Engine Charge (parts → engine charge) | Fieldwright | 1.5 (when Fieldwright lands) |
| Distill Ink Vial (essence → ink vial) | Inkblood | 1.5 (when Inkblood lands) |
| Forge Bone Spear (bone fragments → bone spear) | Bonewarden | 2 |
| Reinforce Armor (leather + bone → patched mail) | Smith station | 2 |
| Refine Bloom Sample (raw bloom → stabilized sample) | Heretic branch | 2 |

Each authored in `content/recipes/`.

## 6. Crafting UI

Phase 1.5 — downtime crafting is part of downtime modal flow (per world-clock spec §7.3):

Pick character → choose Downtime → Craft button shows recipe list filtered by class + known + inputs-available.

```
┌─Craft — Kael (Bonewarden)──────────────────────────┐
│                                                    │
│ Available recipes:                                 │
│ ▣ Refine Bone Fragments                            │
│   3× Raw Bone Dust → 2× Bone Fragment              │
│   (+1 per 3 WIL above 10)                          │
│   Available inputs: 6 / 3                          │
│                                                    │
│ ▢ Forge Bone Spear (locked — Lv 3)                 │
│                                                    │
│ Iterations: [- 1 +]   Max: 2                       │
│ Output preview: 4 Bone Fragments                   │
│                                                    │
│ [Craft]   [Cancel]                                 │
└────────────────────────────────────────────────────┘
```

Phase 2 station crafting: separate modal at smith / apothecary vendor, not tied to downtime.

## 7. Engine

`src/engine/RPC.Engine/Crafting/`:

```
Crafting/
  RecipeDef.cs              // from content
  RecipeRegistry.cs
  CraftingService.cs        // validate + execute
  RecipeDiscovery.cs        // event subscribers (level-up, mission, dialogue, pickup)
```

`CraftingService.Craft(characterId, recipeId, iterations)`:

```csharp
public CraftingResult Craft(...) {
    var def = registry.Get(recipeId);
    if (!ValidateRequirements(...)) return CraftingResult.Rejected(reason);
    var actualIterations = ClampToInputsAvailable(...);
    for (int i = 0; i < actualIterations; i++) {
        ConsumeInputs(...);
        ProduceOutputs(...);
    }
    if (def.Kind == "downtime") MarkDowntimeUsed(...);
    return CraftingResult.Success(iterations: actualIterations, outputs);
}
```

## 8. Save / load

Per save:
- `CharacterState.KnownRecipes` (set persisted in v9+).
- Inventory consumption already covered by inventory-model.

Save version → `"10"` (after vendor-economy v9).

## 9. Tests

- xUnit: recipe known requirement enforced.
- xUnit: class/branch gating.
- xUnit: input consumption atomic (insufficient inputs → no partial).
- xUnit: output bonus scaling correct.
- xUnit: downtime usage tracked per visit.
- Playwright: full flow — character downtime → craft → outputs in inventory.

## 10. Out of scope

- Free-form crafting (combine arbitrary items) — design choice for authored recipes only.
- Quality variance (every craft produces same quantity given bonus rules).
- Real-time crafting timer (turn-based design).
- Player-defined recipes / sharing (Phase 3 modding).
