# Inventory Model — Design Spec
Date: 2026-05-10
Status: design — not yet implemented
Supersedes: inventory section of `2026-05-10-screens-design.md` §5
Depends on: `docs/design/05-characters-and-classes.md` §Component Inventory
Scope: data model, server actions, UI rework, content schema additions. Phase 1 lock-in so Phase 2 doesn't migrate.

## 1. Model

Three storage zones per party. No weight model in Phase 1 — slot count only. (Weight may layer in Phase 2 as a parallel constraint, not a replacement.)

| Zone | Capacity | Access | Persistence |
|---|---|---|---|
| **Backpack** (per character) | 8 slots | Anywhere (combat / exploration / town) | Persists with character. Lost if character dies in dungeon and body not recovered. |
| **Expedition Cache** (per party) | 12 slots | Exploration only (NOT during combat) | Persists with party. Returns to town with party. |
| **Town Storage** (per save) | unlimited | Town only | Persists with save. Never lost. |

Equipment slots (Main, Off, Head, Chest, Legs, Boots, Trinket) are separate from Backpack — equipped items don't count against the 8.

### 1.1 Item kinds

```
ItemKind = Weapon | Armor | Consumable | Component | Material | Quest | Currency
```

- **Weapon / Armor**: single-stack, equippable. Carry durability (Phase 2).
- **Consumable**: stackable (default cap 10), used in combat or exploration. Examples: healing draught, antidote.
- **Component**: stackable, per-type stack cap (see §1.2). Spent during ability use.
- **Material**: stackable cap 99. Crafting input (Phase 1.5 craft system).
- **Quest**: single-stack, locked (cannot drop, sell, or move from backpack of the character holding it; transfers via swap).
- **Currency**: gold = not a slot. Held at party level. Tithe Tokens = per-save (no stack).

### 1.2 Component stack caps (per slot)

Pinned by `docs/design/05`:

| Component | Used by | Stack | Special |
|---|---|---|---|
| Bone Fragment | Bonewarden | 20 | — |
| Engine Charge | Fieldwright | 10 | — |
| Ink Vial | Inkblood | 15 | — |
| Cautery Supply | Cauterist | 12 | — |
| Bloom Sample | Heretic | 5 | **Decays 1/turn after 10 turns of carrying.** Counter persists on save. |
| Tithe Token | Resurrection, Tithebinder | ∞ (single slot, no stack cap) | Currency-like but slot-occupying |

Decay counter: each Bloom Sample stack carries `decayAt: turn` field. When `worldTurn >= decayAt`, stack decrements by 1, `decayAt += 1`. Continues per turn until depleted. Reset by Heretic's `Tend Blooms` downtime.

### 1.3 Item state

```csharp
public record ItemStack(
    string ItemId,        // content id, e.g. "bone_fragment"
    int Quantity,         // 1 for non-stackable
    int? Durability,      // null for non-durable
    int? DecayAt,         // null for non-decaying
    bool Locked           // quest item flag
);
```

Item definition (content): name, kind, stackCap (overrides default), equipSlot, statBonuses, durabilityMax, locked, tags, lore. Lives in `content/items/*.json` (already exists; extend schema).

### 1.4 Zone-mapping rules

- Items move **only** along edges:
  - Backpack(A) ↔ Backpack(B) — only if both characters in same party, both alive, in exploration or town
  - Backpack ↔ Cache — exploration or town (NOT combat)
  - Cache ↔ Town Storage — town only
  - Backpack ↔ Equipment slot (same character) — anywhere combat allows the action; equip swap is a combat **quick action** (Phase 1.5)
  - Town vendor (buy) → Backpack of any character or Cache or Town Storage — town only, choose at buy time
  - Backpack/Cache/Storage → Town vendor (sell) — town only

- Equip cross-character (e.g., transfer Kael's sword to Sera): requires Sera receive item to backpack first, then equip. Two-step. Phase 1.

- Quest items: cannot enter Storage (must travel with party). Cannot be sold. Cannot be dropped.

### 1.5 Stack merging

Auto-merge on move when source + dest are same `ItemId` AND (for decaying types) same `decayAt`. Overflow remains in source slot. Bloom Samples with different `decayAt` values **do not merge** — separate slots. UI displays decay timer on each stack.

### 1.6 Death and recovery

If character dies in dungeon:
- Their Backpack stays on the body (drops to ground at death tile).
- Surviving party can interact with body before exiting dungeon → transfer items into Cache or another Backpack.
- If party returns to town without recovering: Backpack contents lost permanently.
- Phase 3 ironman: bench recovery expedition can retrieve gear but not character.

## 2. Server (RPC.Engine)

### 2.1 Models

New files under `src/engine/RPC.Engine/Inventory/`:

```
Inventory/
  ItemStack.cs           // record (§1.3)
  Backpack.cs            // ItemStack?[8] + ops
  ExpeditionCache.cs     // ItemStack?[12] + ops
  TownStorage.cs         // List<ItemStack> + ops
  InventoryService.cs    // cross-zone moves, validation
```

`PartyState.cs` (existing): add
```csharp
public ExpeditionCache Cache { get; init; } = new();
public TownStorage Storage { get; init; } = new();
public int Gold { get; private set; }
public int TitheTokens { get; private set; }
```

`CharacterState.cs` (existing): add `Backpack Backpack` field. Existing `Equipment` stays; backpack is separate.

### 2.2 Atomic operations

`InventoryService` exposes:

```csharp
MoveResult Move(InventoryAddress from, InventoryAddress to, int quantity);
MoveResult Split(InventoryAddress src, int quantity, InventoryAddress dst);
MoveResult Equip(Guid characterId, InventoryAddress backpackSlot, EquipSlot target);
MoveResult Unequip(Guid characterId, EquipSlot from, InventoryAddress backpackDst);
MoveResult Drop(InventoryAddress src, int quantity);  // exploration: drops to current tile
MoveResult Use(InventoryAddress src, Guid? targetCharacterId);
```

`InventoryAddress`:

```csharp
public record InventoryAddress(InventoryZone Zone, Guid? CharacterId, int SlotIndex);
public enum InventoryZone { Backpack, Cache, Storage, Equipment }
```

`MoveResult`: `Success | Rejected(reason)`. Reasons: `OutOfRange`, `ZoneNotAccessible`, `IncompatibleSlot`, `LockedItem`, `WrongMode`, `StackFull`, `InvalidIndex`.

All moves **atomic** — never half-applied. Implementation: snapshot affected slots, compute outcome, swap in once validated. No partial writes per CLAUDE.md.

### 2.3 WebSocket actions

Add to `GameServer.cs` switch (existing has `swap_row` precedent):

| Action | Payload | Mode allowed |
|---|---|---|
| `inv_move` | `{from, to, qty}` | exploration, town (no combat) |
| `inv_equip` | `{characterId, slotIndex, equipSlot}` | exploration, town, combat-quick (Phase 1.5) |
| `inv_unequip` | `{characterId, equipSlot, dstSlotIndex}` | exploration, town |
| `inv_use` | `{from, targetCharacterId?}` | combat (consumable action), exploration (some items) |
| `inv_drop` | `{from, qty}` | exploration |
| `inv_split` | `{from, qty, to}` | exploration, town |
| `inv_sort` | `{zone, characterId?}` | town only (auto-organize) |

Server validates against `InventoryService` rules. Replies with state update (delta in Phase 2; full snapshot Phase 1).

### 2.4 Decay tick

`GameState.OnDungeonStep()` (currently `TryMoveForward`) advances a `worldTurn` counter Phase 1.5. Each step:

```csharp
foreach (var member in Party.Members where alive)
    member.Backpack.TickDecay(worldTurn);
Party.Cache.TickDecay(worldTurn);
```

Phase 1 stub: counter only, no decay applied (Bloom Samples Phase 1.5+).

## 3. UI

Replaces inventory layout from `2026-05-10-screens-design.md` §5. Same modal frame; internals re-paneled.

### 3.1 Layout (≥1280)

```
┌──────────────────────────────────────────────────────────────────────────┐
│ Inventory                                              Gold 240  Tithe 3 │
├─────────────────────────┬───────────────────────┬──────────────────────┤
│ Zone selector (tabs)    │ Selected item detail  │ Active character     │
│ ───────                 │ ───────               │ ───────              │
│ [Backpacks] [Cache] [Storage]                   │ [4 portrait pills]   │
│                                                  │                      │
│ Backpack — Kael (Bonewarden)                    │ Equipment slots (7): │
│ ┌─┬─┬─┬─┬─┬─┬─┬─┐                              │  Main:  Bone Spear  │
│ │ │ │ │ │ │ │ │ │ 8 slots                      │  Off:   —           │
│ └─┴─┴─┴─┴─┴─┴─┴─┘                              │  Head:  Tithe Hood  │
│                                                  │  Chest: Patched    │
│ Cache (party-shared)                            │  Legs:  Leather    │
│ ┌─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┐                      │  Boots: Iron-shod  │
│ │ │ │ │ │ │ │ │ │ │ │ │ │ 12 slots             │  Trinket: —        │
│ └─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┘                      │                      │
│                                                  │ Components carried: │
│ [filter ▾] [sort ▾]                             │  Bone Fragment ×7   │
│                                                  │  Cautery Supply ×4  │
└─────────────────────────┴───────────────────────┴──────────────────────┘
```

Zone selector shows Backpacks tab (per-character pages), Cache tab, Storage tab (town only — greyed out in dungeon). Cache greyed out in combat.

### 3.2 Tablet portrait (<1024)

Single column, vertical scroll:
1. Tabs (Backpacks / Cache / Storage)
2. Active character backpack grid
3. Equipment slots
4. Component readout
5. Detail sheet rises from bottom on item tap

### 3.3 Drag/drop / tap interactions

Per `2026-05-10-design-system-design.md §5.4` `InventoryGrid` primitive, but with **zone-aware targets**:

- Drag a Backpack item over Cache panel → cross-zone move preview (server-validated on drop).
- Drag onto another character portrait → transfer to their Backpack first empty slot.
- Drag onto Equipment slot → equip (only if compatible kind).
- Drag onto trash icon (bottom-right of zone) → Drop with confirm modal.
- Long-press / right-click → context menu: Use, Equip, Split Stack, Move to Cache, Move to Storage, Drop, Inspect.
- Locked items show lock icon overlay and reject all non-equipped destinations.

### 3.4 Indicators

- Stack quantity badge bottom-right (mono, brass when full to cap, ink-dim otherwise).
- New item violet dot top-left.
- Equipped marker on backpack item that is equipped by ANY character (ring icon).
- Bloom Sample decay timer top-right: orange when ≤3 turns left, red flashing when 1.
- Cache full warning when ≥10/12 slots used.
- Cross-zone availability dimming: if zone inaccessible (Cache in combat, Storage in dungeon), zone tab grayed + tooltip explains.

### 3.5 Flash alerts

- Item picked up → toast bottom-center, auto-stack into first viable Backpack of nearest character; if all full, into Cache; if all full, prompt modal "All inventories full — replace something?".
- Decay tick (turn advance dropping Bloom Sample by 1) → small amber toast "Bloom Sample decayed (3 remain)".
- Quest item received → brass burst on receiving Backpack slot + persistent log entry.
- Component depleted in combat → red flash on resource pip + log entry "Out of Bone Fragments".

## 4. Content schema additions

`content/schemas/item.schema.json` additions:

```json
{
  "kind": "Weapon|Armor|Consumable|Component|Material|Quest|Currency",
  "stackCap": 1,
  "equipSlot": null,
  "statBonuses": {},
  "durabilityMax": null,
  "decayTurns": null,
  "locked": false,
  "tags": [],
  "lore": "",
  "value": 0
}
```

Existing `content/items/*.json` files migrate:
- `weapons.json` — add `kind:"Weapon"`, `equipSlot:"main"|"off"`, durability later
- `armor.json` — add `kind:"Armor"`, `equipSlot:"head"|"chest"|"legs"|"boots"|"trinket"`
- `consumables.json` — `kind:"Consumable"`, `stackCap:10`
- `components.json` — `kind:"Component"`, per-component `stackCap` per §1.2, `decayTurns:10` only on bloom_sample

## 5. Save / load

`SaveSystem.cs` already handles Party. Extend `PartyData`:

```csharp
public BackpackData[] Backpacks { get; set; } = new BackpackData[4];
public CacheData Cache { get; set; } = new();
public StorageData Storage { get; set; } = new();
public int Gold { get; set; }
public int TitheTokens { get; set; }
public int WorldTurn { get; set; }
```

`Version` bumps to `"2"`. Loader migrates v1 → v2 by initializing empty inventories + gold from any party legacy field (none currently, so v1 saves get 0 gold + empty bags).

## 6. Tests

- Unit (xUnit, `RPC.Tests`): zone move matrix — exhaustive 16 combinations × 7 reject reasons.
- Snapshot: 5 inventory scenarios (full backpack overflow to cache, quest item lock, decay over 10 turns, equip cross-character, vendor buy-and-equip).
- Playwright: open Inventory modal at 768/1024/1280, drag item Backpack→Cache, equip from Backpack, split stack.

## 7. Migration plan

1. Implement `InventoryService` + types, no UI wiring (tests pass).
2. Wire server actions, replace any current placeholder inventory handling.
3. Refactor `CharacterSheet.svelte` + new `InventoryModal.svelte` onto `InventoryGrid` primitive.
4. Migrate item content schemas; run content-pack compiler to regenerate RPK.
5. Bump save version + migrator.
6. Delete any pre-existing weight-based bookkeeping (none currently in code, but design-spec inventory section §5 mentioned encumbrance — remove from that doc).

## 8. Out of scope

- Weight model (Phase 2 optional add).
- Item durability degradation (Phase 2 with Smith repair).
- Crafting (Phase 1.5).
- Set bonuses (Phase 2).
- Item identification / curses (Phase 2 if pursued).
