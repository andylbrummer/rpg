# Vendor Storefront + Tavern Characters — Design

Date: 2026-06-20

## Goal

Turn the town's flat text lists into character-driven, image-rich panels, and make the
market actually functional:

1. **Market** — real vendor storefront with AI shopkeeper portraits and per-item images,
   and a fix for the dead market (no purchasable stock for new players).
2. **Tavern** — recruits become characters: AI class portraits and rep-tiered dialogue lines.
3. **Dialogue layer** — replace hardcoded faction quotes with a small content-driven format.
4. **Name variety** — expand the random recruit name pool.
5. **Image pipeline** — a generation script mirroring the existing enemy-portrait tool.

No runtime image API dependency: all PNGs are pre-generated and committed, exactly like
`src/client/public/enemies/*.png`.

## Background (current state)

- Enemy art pipeline: `tools/gen-enemy-images.mjs` → Together AI `FLUX.1-schnell`, 512×512,
  b64, skip-existing, `--force`. Reads `content/enemies/*.json`, writes `public/enemies/<id>.png`.
  Keys `TOGETHER_KEY` / `REPLICATE_KEY` live in `.env`.
- Market buy flow is wired end-to-end (`TownServicesPanel` → `App.handleVendorPurchase` →
  `vendorPurchase` intent → `TownService.PurchaseVendorItem`). The problem is **empty stock**:
  generic `Town.VendorStock` is never seeded on a new game (only rep-locked faction vendors are
  seeded in `GameState.InitializeTown`), so new players have nothing buyable.
- Party duplicate-name crash already fixed (`PartyStatusBar` keyed by `member.slot`).
- Recruit name dedup already exists within a single roster (`GenerateRoster` HashSet).
- Items already carry inline SVG `icon` data-URIs (`content/items/*.json`, 31 items:
  12 components, 8 consumables, 6 armor, 5 weapons).
- No real dialogue system exists — only hardcoded `factionDialogue` quotes in
  `TownServicesPanel.svelte` keyed by rep tier, plus an `ApplyDialogueReputationCommand` rep hook.

## A. Name variety

Expand `TavernRecruitGenerator.Names` in `src/engine/RPC.Engine/Town/TownState.cs` from 12 to
~40 dark-fantasy first names. Pure data change; existing dedup and slot-keying handle collisions.
Update any test that asserts the old count.

## B. Fix dead market

Add `TownService.GenerateVendorStock()` returning a starter generic stock — a curated set of
affordable consumables plus a couple of basic components/gear, sourced by id from
`content/items/*.json` (id, name, price, quantity). Seed it in `GameState.InitializeTown()`:

```
if (Town.VendorStock.Count == 0)
    Town.VendorStock = _townService.GenerateVendorStock();
```

Engine test: new game has non-empty `VendorStock`, and a purchase deducts gold and grants the
item (reuse existing purchase path). Prices come from a single source of truth (item content or a
small price table in the service) — no magic numbers scattered in the UI.

## C. Vendor storefront UI

Rework the `activeTab === 'market'` block in
`src/client/src/features/town/TownServicesPanel.svelte`:

- **Gold header** showing `partyGold`.
- **Shopkeeper card** per vendor (generic + each *visible* faction vendor): AI portrait,
  vendor name, faction-colored frame, a greeting line (from the dialogue layer, §E). Rep-locked
  faction vendors render the portrait dimmed with the rep requirement.
- **Item grid** beneath each vendor: one card per item = AI image, name, price, quantity, Buy
  button. Rep-locked faction items show a lock overlay instead of Buy.
- **Fallbacks**: item `<img onerror>` falls back to the item's existing inline SVG icon; vendor
  portrait `onerror` falls back to the current faction-color block. Never a broken image.

Image paths: `/vendors/<factionId>.png` (+ `/vendors/generic.png`), `/items/<id>.png`.

## D. Tavern characters

In the `activeTab === 'tavern'` block:

- Each recruit card gains a **portrait** keyed by **class** (`/tavern/<classId>.png`, 8 classes),
  since recruits are randomly generated and class is the stable trait. Fallback to the existing
  class-color swatch on error.
- A **dialogue line** per recruit from the dialogue layer (§E), flavored by class and (where
  relevant) the gating faction's rep tier for exclusive recruits.
- Recruit/Cost/Level retained; layout becomes a character card, consistent with the contact cards.

## E. Dialogue layer (content-driven)

Replace the hardcoded `factionDialogue` map with a small JSON content format, e.g.
`content/dialogue/*.json`, keyed by speaker (faction id / class id / `generic`) with rep-tier
buckets:

```json
{
  "speaker": "bureau",
  "vendorGreeting": { "low": "...", "neutral": "...", "high": "..." },
  "lines": { "dismissive": "...", "rumor": "...", "hostility": "..." }
}
```

- Engine: a `DialogueRepository` loads these via the content catalog (mirror `RumorRepository` /
  faction content loading), exposed through the existing town/game state to the client.
- Client: `TownServicesPanel` reads dialogue from game state instead of the inlined map. Keep a
  safe default line if a speaker/tier is missing.
- Tier selection reuses the existing rep thresholds (`< 0` low, `>= 30` high, else neutral).

Scope guard: this is a flat lookup table, **not** branching conversation trees. No dialogue
`GameMode`, no state machine.

## F. Image pipeline

New `tools/gen-vendor-images.mjs`, mirroring `gen-enemy-images.mjs` (Together AI `FLUX.1-schnell`,
512×512, b64, skip-existing, `--force`, gentle rate limit). Three target sets:

- **Vendor portraits** → `public/vendors/<factionId>.png` + `generic.png`; prompts built from
  faction `name` + `identity` + a "shopkeeper portrait" style.
- **Tavern portraits** → `public/tavern/<classId>.png` (8 classes); prompts from class identity +
  a "character portrait" style.
- **Item images** → `public/items/<id>.png` for every id in `content/items/*.json`; prompts from
  item `name` + `description` + an "object on black background" style (not creature portraits).

Style strings are per-set constants so creature/character/object art stay distinct.

## Out of scope

- Branching dialogue trees / conversation mode.
- Runtime image generation.
- Sell-back / vendor restock economy changes beyond seeding starter stock.
- Reworking the contact-card faction dialogue beyond sourcing it from the new dialogue layer.

## Testing

- Engine: name-pool size; `GenerateVendorStock` non-empty; purchase deducts gold + grants item;
  `DialogueRepository` loads and returns lines with safe fallback.
- Client: market renders shopkeeper + item cards; Buy calls `onVendorPurchase`; image `onerror`
  falls back to SVG/color; locked faction items show lock, not Buy.
- All existing tests stay green.

## File touch list

- `src/engine/RPC.Engine/Town/TownState.cs` — name pool.
- `src/engine/RPC.Engine/Town/TownService.cs` — `GenerateVendorStock`.
- `src/engine/RPC.Engine/GameState.cs` — seed generic stock in `InitializeTown`.
- `src/engine/RPC.Engine/Town/DialogueRepository.cs` (new) + content wiring.
- `content/dialogue/*.json` (new).
- `src/client/src/features/town/TownServicesPanel.svelte` — market + tavern rework.
- Client game-state types / presenter for dialogue passthrough.
- `tools/gen-vendor-images.mjs` (new); generated PNGs under `public/{vendors,tavern,items}/`.
