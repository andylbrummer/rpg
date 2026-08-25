# Vendor & Economy — Design Spec
Date: 2026-05-10
Status: design — Phase 1 needs basic Market + Smith; Phase 2 expands with faction vendors
Depends on: inventory-model, faction-system, world-clock
Scope: vendor schema, pricing, stocking, faction tie-in, restock cycle, sell-back, rare-item slots.

## 1. Vendor definition

`content/vendors/<id>.json`:

```json
{
  "id": "vendor-old-calder-market",
  "name": "Old Calder Outfitters",
  "kind": "market",
  "location": "old-calder",
  "factionAffinity": null,
  "factionRepRequirement": null,
  "buyMultiplier": 1.0,
  "sellMultiplier": 0.4,
  "stockSlots": 18,
  "restockCycle": 3,
  "stock": [
    { "itemId":"iron_sword",     "qty":2, "weight":10 },
    { "itemId":"healing_draught","qty":5, "weight":20 },
    { "itemId":"bone_fragment",  "qty":10, "weight":15 },
    { "itemId":"cautery_supply", "qty":4, "weight":10 },
    { "itemId":"hooded_cloak",   "qty":1, "weight":5 }
  ],
  "rareSlots": {
    "count": 2,
    "pool": "market-rare-tier1",
    "rerollOnRestock": true
  },
  "dialogueId": "dialog-shopkeeper-old-calder"
}
```

### 1.1 Kinds

| Kind | Specialty | Buy/Sell |
|---|---|---|
| `market` | general items, consumables, basic gear | both |
| `smith` | weapons, armor, repairs | both |
| `sanctum` | services: rest, heal, resurrect (Phase 2) | services only |
| `apothecary` | consumables, components | both |
| `archive` | lore documents, maps (Phase 2 Inkblood-aligned) | mostly sell-to-player |
| `tavern` | recruits, rumors (Phase 1.5) | recruits priced |
| `faction` | faction-locked vendor (Phase 2) | both, gated |

## 2. Pricing

### 2.1 Base item value

Item def carries `value` (gold). All price computations derive from this.

```
buyPrice  = item.value × vendor.buyMultiplier  × factionRepDiscount × supplyMultiplier
sellPrice = item.value × vendor.sellMultiplier × factionRepDiscount
```

### 2.2 Faction reputation discount

Faction-affiliated vendor adjusts prices by rep:

| Rep band | Buy discount | Sell bonus |
|---|---|---|
| feared (-100) | +50% buy | -25% sell |
| hostile (-50) | +25% buy | -10% sell |
| neutral (0) | 0% | 0% |
| trusted (25) | -10% buy | +10% sell |
| exalted (75) | -25% buy | +20% sell |

Linear interpolation between thresholds. Phase 2 only.

### 2.3 Supply multiplier

Stock quantity affects price for rare commodities:

```
if item.kind == "component" and qty <= 2:
    supplyMultiplier = 1.5
elif qty >= 8:
    supplyMultiplier = 0.85
else:
    supplyMultiplier = 1.0
```

Tunable per item via `priceCurve` field.

## 3. Stock & restock

### 3.1 Initial stock

On first town visit: `stock` array seeded as authored. Plus `rareSlots.count` items drawn weighted from `rareSlots.pool`.

### 3.2 Restock cycle

Every `restockCycle` world turns (per world-clock spec), restock fires:

```
for each stock entry:
    qty = min(qty + restockRate, authoredQty)
if rareSlots.rerollOnRestock:
    redraw rare items from pool
```

`restockRate` per item, default 1.

Phase 1: simpler — restock to authored qty fully on each town entry. Phase 2 uses world-turn cycle.

### 3.3 Rare slots

`rareSlots.pool` references a loot pool:

```json
// content/loot/market-rare-tier1.json
{
  "id": "market-rare-tier1",
  "entries": [
    { "itemId":"steel_sword",         "weight":10 },
    { "itemId":"runed_buckler",       "weight":5 },
    { "itemId":"engine_charge_x3",    "weight":8 },
    { "itemId":"forbidden_text",      "weight":2 }
  ]
}
```

Drawn at restock time, deterministic per `WorldRng` (determinism-replay spec).

## 4. Transactions

### 4.1 Buy

Server action `vendor_buy`:

```json
{ "type":"vendor_buy", "vendorId":"...", "itemId":"...", "qty":1, "dst":"backpack" }
```

Validates:
- Vendor exists + reachable (in current town).
- Item in stock with qty available.
- Party gold sufficient: `gold >= buyPrice × qty`.
- Destination has space (`backpack`, `cache`, `storage`).

Atomic: deduct gold, decrement vendor stock, add to destination. Inventory-model spec enforces zone rules.

Side effects:
- Faction-affiliated vendor: +1 faction rep per 50 gold spent (cap +5/visit).
- Log to telemetry.

### 4.2 Sell

```json
{ "type":"vendor_sell", "vendorId":"...", "from":InventoryAddress, "qty":1 }
```

Validates:
- Source has item with qty.
- Item not `Locked` (quest items).
- Item kind acceptable to vendor (smith won't buy consumables; apothecary won't buy heavy armor — vendor `accepts:[Kind]` array).

Effect: remove from inventory, add gold = `sellPrice × qty`.

Vendor stock does NOT accept sold items (Phase 1 simplicity; Phase 2 may with `stocksFromSells:true` flag for circular economies).

### 4.3 Repair (Smith only, Phase 2)

```json
{ "type":"vendor_repair", "vendorId":"...", "from":InventoryAddress }
```

Cost: `(maxDurability - currentDurability) × item.value × 0.05`. Restores `currentDurability = maxDurability`.

### 4.4 Service (Sanctum)

Service catalog per vendor:

```json
{
  "services": [
    { "id":"rest",      "name":"Rest at Inn",  "cost":20,
      "effect":{"type":"rest"} },
    { "id":"heal_full", "name":"Heal Wounds",  "cost":40,
      "effect":{"type":"heal_party","amount":"full"} },
    { "id":"cure",      "name":"Cure Status",  "cost":30,
      "effect":{"type":"clear_statuses","category":"injury"} },
    { "id":"resurrect","name":"Bone Clerk Resurrection", "cost":"see combat-state spec",
      "effect":{"type":"resurrect"}, "phase":2 }
  ]
}
```

Server action `vendor_service`:

```json
{ "type":"vendor_service", "vendorId":"...", "serviceId":"rest", "characterId":"...optional..." }
```

## 5. UI

Per screens spec §7. Modal frame:

```
┌─Old Calder Outfitters — Market─────────────────────────────────┐
│                                          Gold: 240             │
│ [Buy] [Sell]                                                   │
│ ─────                                                          │
│ <list of stock with price + qty>                               │
│                                                                │
│ Selected: Iron Sword · 18g · qty 2 available                   │
│ [- 1 +]  Total: 18g     [Buy]                                  │
│                                                                │
│ Encumbrance preview: 8/8 → 9/9 (Kael) — full!                  │
└────────────────────────────────────────────────────────────────┘
```

Indicators (per design-system spec):
- Affordability: unaffordable items dimmed.
- Stock dwindling: qty 1 left → "Last one" badge.
- Faction discount: shown next to price when active.
- Sell-back stamp: when in Sell tab, items currently equipped show warning icon.

Flash alerts:
- Buy success: brass coin flip on gold counter.
- Insufficient funds: amber pulse + toast.
- Vendor stock depleted: greys out, "Sold out" overlay.

## 6. Tavern (recruit, Phase 1.5+)

Tavern vendor has special schema:

```json
{
  "kind": "tavern",
  "recruits": [
    {
      "id":"recruit-cauterist-mira-2",
      "class":"cauterist",
      "branch":null,
      "level":1,
      "name":"Esra",
      "cost":150,
      "lore":"...",
      "factionRepRequirement":null
    }
  ],
  "rumorTopics": ["stillness-scouts","engine-flicker"]
}
```

Recruiting:
- Adds character to bench (per `docs/design/05` bench size 12).
- Deducts gold.
- Marks recruit consumed (won't appear again at this tavern).

## 7. Phase 1 starter content

5 vendors hand-authored:

1. **Old Calder Market** — general items, low-tier stock.
2. **Smithy of Ashlock** — weapons + armor, includes a steel-tier rare.
3. **Apothecary** — consumables + components (Bone Fragments, Cautery Supplies).
4. **Sanctum of Stillness** — rest + heal services only.
5. **The Hollow Tavern** — Phase 1.5 only; Phase 1 placeholder.

## 8. Engine

`src/engine/RPC.Engine/Vendors/`:

```
Vendors/
  VendorDef.cs              // from content
  VendorState.cs            // runtime per save
  VendorRegistry.cs
  VendorService.cs          // buy/sell/service/restock
  LootTable.cs              // weighted draw
```

`GameState` extension:

```csharp
public Dictionary<string,VendorRuntime> VendorStates { get; }   // by vendorId
public record VendorRuntime(int LastRestockTurn, Dictionary<string,int> CurrentStock, List<string> RareItems);
```

## 9. Save / load

`SaveData`:

```csharp
public Dictionary<string,VendorRuntimeData> Vendors { get; set; }
public int LastVisitTownId { get; set; }
```

Save version → `"9"` (after faction-system v8 in earlier save-format spec).

## 10. Tests

- xUnit: buy depletes stock, deducts gold, adds to inventory atomically.
- xUnit: sell adds gold without polluting vendor stock.
- xUnit: rare-slot draw deterministic per seed.
- xUnit: restock cycle on world-turn boundary.
- xUnit: faction rep adjusts price linearly.
- Playwright: full Market visit — buy then sell loop.

## 11. Out of scope

- Black markets / illegal goods (Phase 3 if Hollow heat system warrants).
- Vendor haggling minigame.
- Vendor inventory persisting visually (it's data-driven).
- Stock rotation across towns by player travel (Phase 2 may add).
