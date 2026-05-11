# Screens — Design Spec
Date: 2026-05-10
Status: design — not yet implemented
Depends on: `2026-05-10-design-system-design.md`
Scope: full layout + behaviour for Dungeon, Town, World Map, Character Creation, Inventory, Party Management, Stores. Adaptive ≥768px.

All ASCII mockups assume **1280×800** unless noted. Tablet portrait (768×1024) variants follow each section.

## 0. Common Frame

Every screen inherits the layered shell from the design system. Two persistent globals:

- **TopBar** (40px): brass-underlined strip with game logo (left), objective ticker (center), system icons (right: save status, conn status, settings).
- **AppRoot** (flex column): TopBar → ScreenContent (flex-grow) → optional ContextBar.

ScreenContent owns the layout per screen. ContextBar appears in tablet portrait when secondary panels (party rail, log) are collapsed; tap to expand drawer.

## 1. Dungeon Screen (Exploration mode)

```
1280×800 — desktop
┌──────────────────────────────────────────────────────────────────────────┐
│ [logo]  The Engine Reclaims What It Was Given        [💾][⚙][⏶ conn]    │ TopBar 40
├──────┬───────────────────────────────────────────────────┬──────────────┤
│      │                                                   │              │
│ Pty  │                                                   │  Automap     │
│ Rail │       three.js dungeon view (first-person)        │  ┌─────────┐ │
│ 280  │                                                   │  │ █ █ █ . │ │
│      │             [Compass + coords overlay]            │  │ █ . . . │ │
│ 4 ×  │                                                   │  │ . . ▲ . │ │
│ slot │             [tension bar]                         │  │ . . . . │ │
│      │                                                   │  └─────────┘ │
│      │                                                   │              │
│      │                                                   │  Log         │
│      │                                                   │  • You enter │
│      │                                                   │  • Bloom mold│
│      │                                                   │  • You hear  │
│      │                                                   │              │
├──────┴───────────────────────────────────────────────────┴──────────────┤
│  [W ↑] [A ⟲] [D ⟳]   [E Interact]  [I Inventory]  [Esc Pause]           │ HUD bottom
└──────────────────────────────────────────────────────────────────────────┘
```

### Layout
- Left rail 280px (≥1024): `<PartyRail>` — 4 stacked party slots (Card variant), name + Bar (HP), resource pips, status chip row. Click slot → opens CharacterSheet modal.
- Center: three.js viewport, edge-to-edge. Overlay HUD elements absolute-positioned in `HudLayer`:
  - **Compass** top-center, 56px brass ring + coords + dungeon name.
  - **Tension bar** under compass, 240px × 4px.
  - **Objective ticker** echoes TopBar but on the world layer for accessibility.
  - **Hotkey hint strip** bottom-center, ghost buttons. Fades to 0.3 opacity after 4s idle.
- Right rail 320px (≥1024): `<AutoMap>` (top, square aspect) + `<EventLog>` (below, scrolling).

### Inputs (Phase 1)
| Input | Action | Sent |
|---|---|---|
| `W` / `↑` / forward swipe (touch) | move forward | `{type:'move_forward'}` |
| `A` / `←` | turn left | `{type:'turn_left'}` |
| `D` / `→` | turn right | `{type:'turn_right'}` |
| `S` / `↓` | turn 180° (chord: turn_right ×2 server-side; new action `turn_around` preferred) | `{type:'turn_around'}` |
| `E` / tap interactable | interact | `{type:'interact'}` (Phase 1.5) |
| `I` / inventory tap | open Inventory modal | client-only |
| `M` / map tap | enlarge AutoMap into modal | client-only |
| `Esc` | Pause menu modal | client-only |
| `Tab` | cycle selected party member (drives sheet) | client-only |

Touch (tablet): on-screen D-pad bottom-left when input has not been touched within 2s. Long-press dungeon view to recenter compass.

### Tablet portrait (<1024)
- PartyRail collapses to top horizontal strip (4 compact slots, 80px tall, just portrait+HP).
- AutoMap moves into a drawer behind a top-right map button. Tapping pops a 70%-height sheet modal.
- EventLog collapses behind bottom ContextBar — pull up to expand.
- D-pad bottom-left + Action buttons (Interact / Inventory / Map) bottom-right.

### Atmospherics
- AtmosLayer dust motes + vignette + torch flicker active.
- Encounter approach: tension bar fills, brief audio cue stub (`Fx.play('tension_rise')`).

### Indicators visible
- Compass + coords, tension bar, objective ticker.
- Party HP + resource pips per slot.
- Status chip rows.
- Network/save state in TopBar.

### Flash alerts
- Loot popup bottom-center on pickup.
- Damage vignette on environmental damage (traps).
- New tile reveal: faint brass shimmer on automap.

## 2. Town Screen (Hub)

```
1280×800
┌──────────────────────────────────────────────────────────────────────────┐
│ TopBar                                                                   │
├──────────────────────────────────────────────────────────────────────────┤
│   ┌──────────────────────────────────────────────────────────────────┐   │
│   │           THE REACH — Embers of the Bone Tithe                   │   │ Hero
│   │      (illustrated banner placeholder, sepia parchment)           │   │
│   └──────────────────────────────────────────────────────────────────┘   │
│                                                                          │
│   ┌────────────┬────────────┬────────────┬────────────┬────────────┐    │
│   │ Tavern     │ Market     │ Mission    │ Smith      │ Sanctum    │    │ District tiles
│   │ (recruit)  │ (buy/sell) │ Board      │ (upgrade)  │ (rest/heal)│    │ 5 × Card
│   │            │            │ — dungeons │            │            │    │
│   └────────────┴────────────┴────────────┴────────────┴────────────┘    │
│                                                                          │
│   ┌────────────────────────┬───────────────────────────────────────┐    │
│   │  Your Party (4)        │  Active Missions                       │    │
│   │  ─────────────         │  ─────────────                         │    │
│   │  [slot card] x4        │  • Broken Engine — Lv 1                │    │
│   │                        │  • Sewer Warrens — Lv 3 (locked)       │    │
│   │                        │  • Crypt of Whispers — Lv 5 (locked)   │    │
│   └────────────────────────┴───────────────────────────────────────┘    │
│                                                                          │
│   [Save Game]  [Settings]  [Reset]                                       │ Utility row
└──────────────────────────────────────────────────────────────────────────┘
```

### Layout
- **Hero strip** (top, 160px): parchment banner with district name. Placeholder art Phase 1, illustrated Phase 2.
- **District tiles** (5 × Card, horizontal grid): each is a large interactive card. Hover/tap → enters that district view (modal or in-place swap). Phase 1 districts:
  - Tavern (recruit, Phase 1.5)
  - Market (general store, Phase 1)
  - Mission Board (dungeon selection, Phase 1)
  - Smith (equipment store, Phase 1)
  - Sanctum (rest at inn, Phase 1)
- **Party panel** (left, 50%): four `<PartyCard>`s in 2×2 grid (≥1024) or list (768–1023). Each card clickable → CharacterSheet.
- **Active Missions panel** (right, 50%): list of available + locked missions. Selected mission → enables "Embark" CTA on Mission Board card.
- **Utility row** (bottom): Save, Settings, Reset.

District flow: clicking a district opens a modal sheet (`rail` variant on ≥1280, `center` on 1024–1279, `sheet` on tablet portrait). Mission Board is the exception — it's primary, so it expands inline into a Mission Detail view.

### Tablet portrait
- Hero strip 100px.
- District tiles → 2-column grid, scroll.
- Party panel: single column list of compact PartyCards (HP + resources only, no equipment preview).
- Missions panel below party panel (collapsible header).
- Utility row pinned to bottom ContextBar.

### Indicators
- TownClock badge (top-right of hero, Phase 2): faction tick indicator. Phase 1 placeholder text "Day —".
- Per-PartyCard: HP bar, resource pips, equip-warning icon if any slot empty post-loot.
- Mission cards show recommended level, reward preview, "completed" stamp.

### Flash alerts
- Save success/fail toasts.
- Party returned from dungeon: brass burst around any leveled-up character.
- Loot integration: any unread inventory items badge on the Inventory icon in TopBar.

### Atmospherics
- AtmosLayer in town mode: warm cream overlay, no vignette, gentle parchment grain texture (CSS gradient + svg noise).

## 3. World Map Screen

Phase 1.5/2 — pre-spec'd here so the navigation pattern is fixed.

```
1280×800
┌──────────────────────────────────────────────────────────────────────────┐
│ TopBar                                                                   │
├────────────┬─────────────────────────────────────────────────────────────┤
│  Legend    │                                                             │
│  ────      │             [hand-drawn parchment map — SVG]                │
│  🏰 city   │                                                             │
│  ⚔ mission │     ●  Whitepeak     ⟶ road                                 │
│  ☩ shrine  │      \\                                                     │
│  ⌬ bloom   │       ●  Ashreach     ⚔  ⚔                                  │
│  ⌧ engine  │      /                                                      │
│  ▲ peril   │     ●  Old Calder  (current)                                │
│            │      \\                                                     │
│  Filters   │       ●  Bonefen     ⌧                                      │
│  [city]✓   │                                                             │
│  [misn]✓   │                                                             │
│  [bloom]   │                                                             │
│  [engn]    │                                                             │
├────────────┴─────────────────────────────────────────────────────────────┤
│  Selected: Ashreach  ·  Lv 3 city  ·  Faction: Tithe Concord  · [Travel] │ Detail bar
└──────────────────────────────────────────────────────────────────────────┘
```

### Layout
- Left rail 220px: legend + filter checkboxes. Filters toggle marker categories without re-fetching.
- Center: SVG parchment map, pan + zoom (`d3-zoom` or manual pointer events). Hex / point graph of locations. Lines for known roads (ink), dashed for rumored.
- Bottom detail bar (72px): on selection, shows location card + Travel CTA. Travel CTA enabled only if adjacent on the road graph and party can afford travel time.

### Interaction
- Click / tap a location node = select.
- Double-click = quick Travel if eligible.
- Drag = pan. Pinch / wheel = zoom (clamped 0.8–3.0).
- `F` = focus on current party location. `R` = reset zoom.

### Tablet portrait
- Legend + filters drop into a Filters drawer accessed by top-left button.
- Detail bar pinned to bottom.
- Map fills the rest. Pinch-zoom is primary interaction.

### Indicators
- Markers tint by faction control (Phase 2).
- Bloom markers pulse violet at increased intensity.
- Travel routes: green if cleared, amber if known with peril, red if not yet scouted.

### Flash alerts
- New rumor: pin pulse + log entry.
- Faction tick (Phase 2): brief whole-map ink-bleed transition over 600ms when world state changes.

## 4. Character Creation Screen

Phase 1.5. Phase 1 uses fixed starting party; spec here drives Phase 1.5 implementation.

```
1280×800
┌──────────────────────────────────────────────────────────────────────────┐
│ TopBar:  Create Character — Step 2 of 4: Class                           │
├──────────────────────────────────────────────────────────────────────────┤
│  Step rail (top inside): ① Identity → ② Class → ③ Stats → ④ Review        │
├────────────────────┬───────────────────────────────┬────────────────────┤
│                    │                               │                    │
│  Class List        │  Class Detail                 │  Preview           │
│  ────────────      │  ─────────────                │  ─────────         │
│  ▣ Bonewarden  ←   │  THE BONEWARDEN               │  [portrait]        │
│  ▢ Stillblade      │  Tithe-keeper, front-liner    │                    │
│  ▢ Cauterist       │                               │  HP   28           │
│  ▢ Hollow          │  "Every fragment paid is a    │  ATK  7            │
│                    │  fragment that won't betray   │  DEF  5            │
│                    │  you."                        │  SPD  3            │
│                    │                               │  WIL  4            │
│                    │  Starting abilities:          │                    │
│                    │  • Bone Spear (long)          │  Resource:         │
│                    │  • Tithe Touch (melee)        │  Bone Fragments    │
│                    │                               │  ◧◧◧◧◧◧            │
│                    │  Progression (cap 5):         │                    │
│                    │  1 → 2  Reinforced Marrow     │                    │
│                    │  2 → 3  Choose: Spear / Wall  │                    │
│                    │  ...                          │                    │
│                    │                               │                    │
├────────────────────┴───────────────────────────────┴────────────────────┤
│  [← Back]                                            [Continue →]        │
└──────────────────────────────────────────────────────────────────────────┘
```

### Steps
1. **Identity** — name (text field), portrait (3 choices Phase 1.5, 6+ Phase 2), pronouns, short backstory hook (3 picks → 1).
2. **Class** — pick 1 of 4 (Phase 1.5 — Bonewarden, Stillblade, Cauterist, Hollow). Phase 2 expands list per `docs/design/05-characters-and-classes.md`.
3. **Stats** — 3 modes:
   - **Standard array** (default): assign predefined values to slots.
   - **Point buy** (advanced): 24 points, 1–8 per stat, costs scale 2 per pt over 5.
   - **Roll** (chaos): 4d6-drop-lowest × 5. One re-roll allowed.
4. **Review** — Card summary + final Confirm.

### Layout
- Top step-rail: 4 step pills with progress fill. Tappable to jump backward, never forward of completion.
- Three-column body on ≥1024: list / detail / preview.
- Bottom action row: Back / Continue. Continue disabled until step complete.

### Tablet portrait
- Single column. List → detail → preview stack as accordion / scroll.
- Step rail collapses to "Step 2/4" label + dots.

### Indicators
- Stat preview live-updates as user selects class / allocates points.
- Reset/randomize buttons in step 3.
- Validation chips: red border on invalid points, brass check when valid.

### Flash alerts
- Confirm-on-finalize modal: "Create {name}? Cannot undo until next mission." Then brass burst + transition back to Tavern/Town.

## 5. Inventory Screen

```
1280×800  (modal — rail variant docks right; primary covers center)
┌──────────────────────────────────────────────────────────────────────────┐
│ Modal Header: Inventory — Carrying for 4                          [ × ] │
├────────────────────┬───────────────────────┬───────────────────────────┤
│  Tabs              │  Detail               │  Equipped (selected hero) │
│  ─────             │  ─────                │  ─────                    │
│  All  Weapons      │  Bone Spear           │  Kael — Bonewarden        │
│  Armor  Cons       │  ───                  │                           │
│  Materials Quest   │  Long range polearm   │  Main Hand  [Bone Spear]  │
│                    │  Damage 1d8+2         │  Off Hand   [empty]       │
│  Filter ▼  Sort ▼  │  Range  Long          │  Head       [Tithe Hood]  │
│                    │  Weight 3.4           │  Chest      [Patched Mail]│
│  [Grid 7×N]        │  ──                   │  Legs       [Leather]     │
│   ▣ ▣ ▣ ▣ ▣ ▣ ▣    │  "The point matters   │  Boots      [Iron-shod]  │
│   ▣ ▣ ▣ . . . .    │   less than what is   │  Trinket    [empty]       │
│   . . . . . . .    │   on the point."      │                           │
│                    │                       │  [Equip] [Use] [Drop]     │
│                    │                       │                           │
├────────────────────┴───────────────────────┴───────────────────────────┤
│  Encumbrance:  ▓▓▓▓▓▓▓░░░░░░░░░  12.4 / 30  · party of 4              │
└──────────────────────────────────────────────────────────────────────────┘
```

### Layout
- 3 columns ≥1280: tab/filter+grid (40%), detail (30%), equipped (30%).
- 1024–1279: 2 columns (grid+detail, equipped slides in as right modal-rail on equip).
- Tablet portrait: tabs → grid → detail as a vertical stack with bottom action sheet.

### Tabs
`All`, `Weapons`, `Armor`, `Consumables`, `Materials`, `Quest`. Active tab brass underline.

### Filters / Sort
Filter: equipped-by-anyone, can-equip-by-selected, new, broken. Sort: name, weight, value, type.

### Grid behaviour
- `<InventoryGrid>` primitive (see design system §5.4).
- Selected slot → detail panel populates.
- Drag to equipped slot → equip if compatible.
- Drag to party portrait (in left rail or PartyStatusBar) → transfer to that character.
- Drag outside grid → drop confirmation modal.
- Right-click / long-press → context menu: Use, Equip, Split Stack, Mark Junk, Drop.

### Equipped panel
- Slots per `docs/design/05-characters-and-classes.md`: main, off, head, chest, legs, boots, trinket.
- Each slot is an `<InventorySlot>` accepting compatible types.
- Bottom: Equip / Use / Drop primary actions for the selected backpack item.
- Switch active character via tab strip across top (4 portrait pills) or arrow keys.

### Indicators
- New item badge (violet dot top-left of slot).
- Equipped-elsewhere marker (small ring icon).
- Stat-comparison: when an item is selected and an equip-target is hovered, deltas show `+2 ATK / -1 SPD` in tooltip and on the equipped slot border (green/red bias).

### Flash alerts
- Equip success: brass shimmer on equipped slot.
- Drop confirm: amber toast "Dropped Iron Sword (×1)".
- Stat increase: green flash on stat number in CharacterSheet (linked open).

### Tablet portrait
- Grid 4 columns (slot size 72px).
- Detail and Equipped panels become bottom sheets: tap item → detail sheet from bottom, swipe up for equip sheet. Persistent action bar at sheet bottom.

## 6. Party Management Screen

```
1280×800
┌──────────────────────────────────────────────────────────────────────────┐
│ TopBar                                                                   │
├──────────────────────────────────────────────────────────────────────────┤
│  Formation — drag to reposition                                          │
│  ────────────                                                            │
│                                                                          │
│   Front Row              Back Row                                        │
│   ┌──────┬──────┐        ┌──────┬──────┐                                 │
│   │ Kael │ Sera │        │ Mira │ Vex  │                                 │
│   │ Bone │ Stl  │        │ Cau  │ Hol  │                                 │
│   │ 28/28│ 14/14│        │ 14/14│ 11/11│                                 │
│   └──────┴──────┘        └──────┴──────┘                                 │
│                                                                          │
│  Bench (Phase 1.5+):                                                     │
│   [Recruit slot] [Recruit slot] [Recruit slot]                           │
│                                                                          │
├──────────────────────────────────────────────────────────────────────────┤
│  Selected: Kael — Bonewarden Lv 1                                        │
│  ───────────────────                                                     │
│   ┌─Stats──┐ ┌─Abilities─┐ ┌─Equipped──┐ ┌─Lore────────────────────┐    │
│   │ HP 28  │ │ Bone Spear │ │ [slots]   │ │ Born to a tithe-keeper..│   │
│   │ ATK 7  │ │ Tithe Touch│ │           │ │                          │   │
│   │ DEF 5  │ │            │ │           │ │                          │   │
│   │ SPD 3  │ │            │ │           │ │                          │   │
│   │ WIL 4  │ │            │ │           │ │                          │   │
│   └────────┘ └────────────┘ └───────────┘ └──────────────────────────┘   │
│                                                                          │
│  [Open Sheet]  [Level Up (1)]  [Swap with Bench]  [Dismiss]              │
└──────────────────────────────────────────────────────────────────────────┘
```

### Layout
- **Formation board** top: drag-and-drop 2+2 grid + bench row.
  - Drag a card from front → back swaps rows (uses existing `swap_row` server action).
  - Drag card to bench (Phase 1.5) un-actives and parks.
  - Slots accept only valid drops; show preview tint.
- **Selected character pane** bottom: 4 sub-cards (Stats, Abilities, Equipped, Lore) in a 4-column row ≥1280; 2×2 grid 1024–1279; vertical stack <1024.

### Actions
- **Open Sheet** → CharacterSheet modal (existing, refactored onto Modal primitive).
- **Level Up** → modal flow if XP threshold reached. Shows next-level changes + class choices.
- **Swap with Bench** → opens BenchSwap modal listing recruitable bench (Phase 1.5+).
- **Dismiss** → confirm-destructive modal.

### Indicators
- Each formation slot: HP bar, resource pips, status chips, equip-warning icon, "level-up available" brass badge.
- Front-row indicator: ⚔ icon (takes melee hits first).
- Back-row indicator: 🛡 icon (protected, ranged actions favored).

### Flash alerts
- Successful swap: brass shimmer on both slots + toast "Kael moved to Back Row".
- Level-up burst.
- Dismiss confirm executes character delete + ink-blot fade.

### Tablet portrait
- Formation board compresses to a 2-row strip (front above, back below), bench scrolls horizontally.
- Sub-cards stack vertically.
- Action buttons pinned to bottom ContextBar.

## 7. Store Screens (Market / Smith / Tavern / Sanctum)

All four "stores" share a base template `<StorePanel>` driven by storefront type. Phase 1 implements Market + Smith + Sanctum; Tavern in Phase 1.5.

```
1280×800
┌──────────────────────────────────────────────────────────────────────────┐
│ Modal Header: Market — Old Calder Outfitters             [ Gold: 240 ]   │
├──────────────────────────────┬─────────────────────────────────────────┤
│  Shop Inventory              │  Detail / Trade Panel                    │
│  ─────────                   │  ─────────                               │
│  [Buy] [Sell] tab toggle     │  Bone Spear                              │
│                              │  ───                                     │
│  [filter ▾]  [sort ▾]        │  Damage 1d8+2 · Range Long · Weight 3.4  │
│                              │                                          │
│  ▣ Bone Spear      32g       │  Vendor price: 32g                       │
│  ▣ Iron Sword      18g       │  You have:    0                          │
│  ▣ Hooded Cloak    25g       │                                          │
│  ▣ Healing Drop    8g        │  Quantity:  [-] 1 [+]                    │
│  ▣ Cautery Kit     15g       │                                          │
│  ▣ Bone Fragment   2g        │  Total: 32g                              │
│                              │                                          │
│  …                           │  Equipping on: [Kael ▾]                  │
│                              │                                          │
│                              │  [ Buy ]   [ Cancel ]                    │
├──────────────────────────────┴─────────────────────────────────────────┤
│  Encumbrance preview: 12.4 → 15.8 / 30                                  │
└──────────────────────────────────────────────────────────────────────────┘
```

### Variants
- **Market**: general items, Buy/Sell tabs.
- **Smith**: weapons/armor only + Repair tab. Repair cost = `(maxDurability - currentDurability) * unitCost`.
- **Sanctum**: services list (Rest, Heal Wounds, Cure Status, Resurrect (Phase 2)). No grid — list of service cards with price + Apply CTA.
- **Tavern**: recruitable NPCs as cards with portrait + stats + price + Recruit CTA (Phase 1.5).

### Common rules
- Currency: gold, displayed in modal header. Always also visible in TopBar.
- Buy: deduct gold, add to inventory, refresh both lists.
- Sell: shop offers `floor(price * 0.4)`. Confirm if item is equipped (will unequip).
- Quantity stepper for stackable items; capped by gold + encumbrance.
- Equipping-on selector lets the buy flow immediately equip purchase to a chosen party member (saves a step).

### Indicators
- Gold change preview live in the trade panel.
- Encumbrance preview footer with delta (current → after).
- Affordability: items priced > available gold are dimmed and disabled.
- Item-already-owned badge: small dot.

### Flash alerts
- Purchase success: brass coin-flip animation on gold counter (decrement) + brass shimmer on new inventory slot.
- Insufficient funds: amber pulse on gold counter + toast.
- Sell confirmation: ink-blot transition on the sold slot, gold counter increments.
- Rest success (Sanctum): vignette-clear effect, green flash on each party HP bar full.

### Tablet portrait
- Tab toggle at top, single column. Tap item → detail sheet from bottom.
- Quantity stepper + Buy button pinned to sheet bottom.

## 8. Cross-Screen Behaviours

### 8.1 Navigation contracts

| From | To | Trigger | Transition |
|---|---|---|---|
| Town | Dungeon | Mission Board Embark | Ink-bleed 600ms |
| Dungeon | Town | Return to Town button / Esc → Quit | Parchment fold 320ms |
| Dungeon | Combat | Encounter | Tension fade-to-clash (see design-system §7.8) |
| Combat | Dungeon (victory) | Last enemy dies | Brass radial pulse 400ms |
| Combat | Town (party wipe) | All dead | Slow fade-to-black 1.2s + "The Reach takes its due." toast → Town |
| Any | Inventory | I key / button | Modal slide-up |
| Any | Pause | Esc | Modal center |

### 8.2 Modal stacking limits

Hard cap **2 modal layers**. Examples allowed: Inventory → ConfirmDrop. Examples forbidden: CharacterSheet → Inventory → ConfirmDrop. The CharacterSheet must close itself or yield.

### 8.3 Save points

Auto-save on town entry and on save-game action (manual). Save state shows the floating ink-blot indicator in TopBar between auto-saves to communicate "unsaved progress".

### 8.4 Pause / settings modal

`Esc` opens Pause modal: Resume, Save, Settings (audio, motion, atmospherics toggles, hotkey rebinds Phase 2), Return to Town (if in dungeon), Quit. Settings is a sub-modal inside Pause.

### 8.5 Reconnect overlay

If WebSocket disconnects, AtmosLayer dims to 0.4 and a centered amber card appears: "Connection lost. Reconnecting… attempt N/5." Cancel button reveals on attempt 3. Successful reconnect dismisses with toast.

## 9. Implementation Order

Phase 1 (existing screens, refactor onto primitives + tokens):
1. Town screen — adopt district tile grid + party panel + missions panel layout.
2. Dungeon screen — wire 3-pane layout (PartyRail / world / AutoMap+Log) and HUD overlay positions.
3. Inventory modal — replace placeholder UI with full 3-pane modal per §5.
4. Party management — promote CharacterSheet to full screen + formation board.
5. Store screens — Market, Smith, Sanctum onto common `<StorePanel>`.

Phase 1.5:
6. Character creation (4-step wizard).
7. Tavern (recruitment) + bench.
8. World map skeleton (single region, 4–6 nodes) — full faction layer deferred.

Phase 2:
9. World map full graph + faction tinting + travel encounters.
10. Light theme (if explored).
11. Portrait illustration set.

## 10. Test Coverage (Playwright)

For each screen, add a smoke test at 768×1024, 1024×768, and 1280×800 resolutions verifying:
- Primary CTAs visible and reachable.
- Modal opens/closes via keyboard.
- Layout doesn't overflow viewport (no horizontal scroll).
- Snapshot of layered z-ordering (HUD, modal, toast visible above world).

Visual regression: capture token snapshot of the design system styleguide page across breakpoints.

## 11. Out of Scope

- Multiplayer / co-op screens.
- Phone (<768) layouts.
- Touch gestures beyond tap, long-press, drag, pinch.
- Streamer / spectator mode.
- VR or controller schemes.
