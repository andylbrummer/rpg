# Design System — Design Spec
Date: 2026-05-10
Status: design — not yet implemented
Scope: tokens, primitives, components, layered UI, indicators, flash alerts, atmospherics. Adaptive tablet (≥768px) and up.

## 1. Design Direction

Grim low-magic dungeon crawler. Bone, ink, candlelight, oxidized brass. Diegetic-feeling chrome: panels look like parchment pinned over stone, bars look like ink wells, buttons look like waxed seals. Avoid flat-modern gloss. Restrained motion — atmospherics breathe, controls snap.

Tone references inherited from `docs/design/02-world-and-setting.md`: Abercrombie / Locked Tomb / Vandermeer. UI must read as a 19th-century field journal kept by a mercenary, not a SaaS dashboard.

## 2. Token Set (CSS custom properties, dark default)

Replace current `:root` palette in `src/client/src/app.css` with the following. Light palette is fallback only — game is dark-first.

```css
:root {
  /* Surfaces (warm dark, slight blue) */
  --c-bg-0: #0b0a10;          /* black-iron, behind everything */
  --c-bg-1: #14131c;          /* base panel */
  --c-bg-2: #1d1b27;          /* raised panel / card */
  --c-bg-3: #272432;          /* hover / pressed */
  --c-veil:  rgba(8, 6, 13, 0.72); /* modal scrim */

  /* Ink (text) */
  --c-ink:    #d8d1c1;        /* parchment ink */
  --c-ink-dim:#9b9384;        /* secondary */
  --c-ink-off:#5e574b;        /* disabled / faded lore */
  --c-ink-hi: #f4ecd6;        /* highlight (names, totals) */

  /* Edges */
  --c-edge:    #3a3548;       /* hairline */
  --c-edge-hi: #5a5168;       /* focused / selected hairline */
  --c-rule:    #2a2638;       /* internal divider */

  /* Brand / accent (alchemical brass + violet) */
  --c-brass:   #c9a04a;       /* gold leaf */
  --c-brass-d: #8a6d2c;
  --c-violet:  #aa7bff;       /* arcane */
  --c-violet-d:#6b46b8;

  /* Semantic */
  --c-good:    #7fb069;       /* heal, success */
  --c-warn:    #d99a36;       /* low resource, warning */
  --c-bad:     #c9445b;       /* damage, danger */
  --c-info:    #5fa8c9;       /* hint, neutral pulse */

  /* Resource hues */
  --c-hp:      #c9445b;
  --c-hp-dim:  #5b1f2a;
  --c-bone:    #e8dec2;       /* bone fragments */
  --c-blood:   #8a1f2a;       /* blood magic */
  --c-cautery: #d99a36;       /* cautery supplies */
  --c-xp:      #aa7bff;

  /* Spacing scale (8px grid w/ half-step) */
  --s-1: 0.25rem;   /* 4 */
  --s-2: 0.5rem;    /* 8 */
  --s-3: 0.75rem;   /* 12 */
  --s-4: 1rem;      /* 16 */
  --s-5: 1.5rem;    /* 24 */
  --s-6: 2rem;      /* 32 */
  --s-7: 3rem;      /* 48 */
  --s-8: 4rem;      /* 64 */

  /* Radii (small — UI is wax-stamp, not pill) */
  --r-1: 2px;
  --r-2: 4px;
  --r-3: 6px;
  --r-pill: 999px;

  /* Borders */
  --b-1: 1px solid var(--c-edge);
  --b-2: 2px solid var(--c-edge);
  --b-hi: 1px solid var(--c-edge-hi);

  /* Elevation (soft inner + dark outer drop) */
  --el-1: 0 1px 0 #00000060, 0 2px 4px #00000040;
  --el-2: 0 1px 0 #00000080, 0 6px 18px #00000060,
          inset 0 1px 0 #ffffff08;
  --el-3: 0 1px 0 #00000080, 0 18px 48px #00000088,
          inset 0 1px 0 #ffffff10;

  /* Typography */
  --f-display: 'Cinzel', 'Trajan Pro', serif;     /* headings, screen titles */
  --f-body:    'EB Garamond', Georgia, serif;     /* lore, descriptions */
  --f-ui:      'Inter', system-ui, sans-serif;    /* labels, numbers */
  --f-mono:    ui-monospace, Consolas, monospace; /* damage, coords, dice */

  --t-h1: 700 2rem/1.1 var(--f-display);
  --t-h2: 600 1.5rem/1.15 var(--f-display);
  --t-h3: 600 1.125rem/1.2 var(--f-ui);
  --t-body: 400 1rem/1.45 var(--f-body);
  --t-ui: 500 0.875rem/1.3 var(--f-ui);
  --t-label: 600 0.75rem/1 var(--f-ui);
  --t-num: 600 1rem/1 var(--f-mono);

  /* Motion */
  --dur-1: 80ms;   /* snap */
  --dur-2: 160ms;  /* settle */
  --dur-3: 320ms;  /* enter */
  --dur-4: 600ms;  /* atmospheric */
  --ease-out: cubic-bezier(.2,.7,.2,1);
  --ease-in:  cubic-bezier(.6,0,.8,.2);

  /* Z-layers (see §4) */
  --z-world:    0;
  --z-hud:      100;
  --z-overlay:  200;
  --z-modal:    300;
  --z-toast:    400;
  --z-atmos:    500;   /* full-screen fx like vignette */
  --z-debug:    900;
}

@media (prefers-reduced-motion: reduce) {
  :root { --dur-1: 0ms; --dur-2: 0ms; --dur-3: 0ms; --dur-4: 0ms; }
}
```

Light palette: deferred — Phase 1 is dark only. `@media (prefers-color-scheme: light)` is unsupported until Phase 2.

## 3. Breakpoints — Adaptive Tablet+

Game is **tablet-portrait minimum** (768×1024). Phone (<768) is unsupported in Phase 1.

| Token | px | Use |
|---|---|---|
| `--bp-tp` | 768 | tablet portrait (min supported) |
| `--bp-tl` | 1024 | tablet landscape — sidebars un-collapse |
| `--bp-sd` | 1280 | small desktop — full HUD visible |
| `--bp-d`  | 1440 | desktop — wider party rail, larger inventory grid |
| `--bp-w`  | 1920 | wide — max content width caps, ambient sidebars appear |

Implementation: use `@media (min-width: 48rem)` style queries, not container queries (Photino webview is single root). Use `clamp()` for fluid type and spacing rather than step breakpoints where viable.

Adaptation rules:

- **<1024 (tablet portrait):** single primary panel + bottom drawer secondary. HUD docks to top. PartyStatusBar collapses to icon row, expandable on tap. Inventory becomes single column with tab strip. Modals = full-screen sheets.
- **1024–1280 (tablet landscape):** primary panel + right rail (party, log). Inventory split: list left, detail right. Modals = centered card max 720px.
- **≥1280 (desktop):** dual rails (left = party, right = log/minimap). Inventory three-column (list, detail, equipped slots). Modals max 880px.

Touch target minimum **44×44 CSS px** on all interactive elements. Maintain on every breakpoint — desktop does not shrink hit boxes.

## 4. Layered UI Architecture

Five vertically stacked layers, each owns a fixed z-band:

```
┌──────────────────────────────────────────────┐ z=500  atmos
│  Vignette · dust · damage flash · screen     │
│  shake · low-HP red pulse · scene transition │
├──────────────────────────────────────────────┤ z=400  toast
│  Combat result · level-up burst · save       │
│  confirm · disconnect · loot popups          │
├──────────────────────────────────────────────┤ z=300  modal
│  Character sheet · shop · inventory          │
│  detail · confirm · settings                 │
├──────────────────────────────────────────────┤ z=200  overlay
│  CombatOverlay · dialog box · pause          │
├──────────────────────────────────────────────┤ z=100  hud
│  ExplorationHUD · PartyStatusBar · automap   │
│  · minimap · compass · objective ticker      │
├──────────────────────────────────────────────┤ z=0    world
│  Three.js canvas (renderer)                  │
└──────────────────────────────────────────────┘
```

**Rules:**
- Only one **overlay** active at a time (Combat OR Dialog OR Pause). Mode in `GameState.mode` drives which mounts.
- Up to one **modal** atop any overlay. Modal mount blurs the layer below (`backdrop-filter: blur(8px) saturate(.8)`) and dims with `--c-veil`.
- **Toast** stack max 4. Older ones fade out when 5th arrives.
- **Atmos** never blocks pointer events (`pointer-events: none`).
- HUD elements hide when their owning mode is inactive (PartyStatusBar visible in Exploration + Combat; AutoMap visible in Exploration only).

**Component placement (Svelte):**
- `src/client/src/ui/layers/` — new folder.
  - `WorldLayer.svelte` — wraps three.js mount.
  - `HudLayer.svelte` — slot per HUD widget.
  - `OverlayLayer.svelte` — switches on mode.
  - `ModalLayer.svelte` — `<dialog>` stack, native `showModal()` + ESC handling.
  - `ToastLayer.svelte` — fixed-position queue.
  - `AtmosLayer.svelte` — fixed inset 0, owns CSS-only effects, listens to combat/state events for triggers.

`App.svelte` becomes a thin composition of the five layers; current monolith logic moves into the layer components.

## 5. UI Primitives

### 5.1 Button (`<Button>`)

Variants × sizes matrix:

| Variant | Use | Default fill | Hover | Pressed | Disabled |
|---|---|---|---|---|---|
| `primary` | confirm, attack | brass fill, ink-hi text | brighten brass 8% | sink 1px | bg-3 / ink-off |
| `ghost`   | secondary action | transparent, edge-hi | bg-2 | sink 1px | edge / ink-off |
| `danger`  | flee, delete | bad outline, bad text | bad bg-tint | sink 1px | -- |
| `flat`    | menu rows | no border, hover bg-2 | bg-2 | bg-3 | ink-off |

Sizes: `sm` 32px, `md` 40px, `lg` 48px. Min width `--s-7`.

Focus: 2px brass ring offset 2px, never removed. Keyboard activation: Space / Enter. Loading state replaces label with 3-dot inkblot animation.

### 5.2 Card (`<Card>`)

Atomic surface, ink-on-parchment:

```
┌─────────────────────────────┐  bg-2, border edge, radius r-2, el-1
│  Header (optional)          │  border-bottom rule, h3
│  ─────────────────────────  │
│  Body (slot)                │  s-4 padding
│  Body                       │
│  ─────────────────────────  │
│  Footer (optional)          │
└─────────────────────────────┘
```

Props: `title?`, `subtitle?`, `tone?: 'neutral' | 'good' | 'bad' | 'arcane'` (tints left edge 3px), `interactive?: bool` (adds hover lift + cursor pointer), `selected?: bool` (brass edge + el-2).

Compositional: inventory tile, party slot, shop entry, mission board entry are all `<Card>` with custom body.

### 5.3 Modal (`<Modal>`)

Native `<dialog>` element. Use `showModal()` so ESC + form method=dialog work without bespoke focus traps.

States:
- **sheet** (tablet portrait): slides up from bottom, full width, max-height 90vh, rounded top only.
- **center** (≥1024): centered card, max-width 720–880px, el-3.
- **rail** (≥1280, optional): docks to right edge full height, 480px wide. Used for inventory detail.

Composition:

```
[scrim: --c-veil + backdrop-blur]
[modal frame: bg-1, edge, r-3, el-3]
  [header: brass underline, h2 title, × close button (44×44)]
  [body: scroll if overflow, max-height calc(90vh - header - footer)]
  [footer: right-aligned button row, optional]
```

ESC closes. Click on scrim closes if `dismissible=true` (default). Confirm-destructive modals require explicit button. Trap focus inside modal — `<dialog>` does this natively.

Stack: up to 2 modals. Second modal pushes first to scrim opacity 0.5 and disables it. Avoid 3+ — refactor flow instead.

### 5.4 Inventory grid (`<InventoryGrid>`)

Grid of `<InventorySlot>` cells. Each slot is a fixed 64×64 (tablet) → 72×72 (≥1280) square.

```
slot states:
  empty:    bg-1, dashed edge
  filled:   bg-2, edge, item icon centered, quantity badge bottom-right
  selected: brass edge + el-2 + slight scale 1.02
  invalid:  bad tint (drop preview when item type rejected)
  highlight:violet pulse (quest item, new item)
```

Rules:
- Drag/drop via Pointer Events (touch + mouse unified). `pointerdown` arms drag at 4px threshold.
- Long-press (450ms) on touch opens context menu (use, equip, drop, inspect).
- Tooltip on hover (mouse): 320ms delay, item card with stats, lore italic body font.
- On tablet: tap = select, double-tap = primary action (use/equip), long-press = context menu.

Encumbrance: footer shows `weight / capacity` with bar. ≥90% → warn color, ≥100% → bad color + status icon "Encumbered" added.

### 5.5 Bar (`<Bar>`)

HP/resource/XP rendering primitive:

```
[ ▓▓▓▓▓▓▓▓▓░░░░░░░░ ]  HP 18 / 30
```

Props: `value`, `max`, `color`, `label?`, `compact?`, `pulse?`. Compact removes label, used in PartyStatusBar.

Visuals:
- Background `--c-bg-1`, border edge, height 8px (compact) / 14px (full).
- Fill clipped, animates via `transform: scaleX()` (not `width`) for performance.
- Damage tick: white-ish flash over the lost segment for 400ms before settling.
- Heal tick: brief green over the gained segment for 400ms.
- Below 25%: fill color shifts to `--c-bad` and adds 1.2s pulse animation.

### 5.6 Iconography

Use a single SVG sprite sheet `src/client/public/icons.svg` with `<symbol>` definitions. Reference via `<svg><use href="/icons.svg#sword"/></svg>`. Phase 1 icons (~40): combat actions, status effects, classes, resources, item categories, nav arrows, system (save, gear, close).

Style guide: 24px viewBox, 1.5px stroke, no fill (or single fill), brass color via `currentColor`. Outlined-style, lean toward heraldic/woodcut, not flat material.

## 6. Indicators

Indicators communicate persistent state. Distinct from flash alerts (transient events).

### 6.1 Status effect chip

Shape: 28×28 square, r-1 corners, item icon centered, duration counter top-right (small mono number).

Tint by category:
- Buff: brass border + brass glow
- Debuff: bad border + faint bad pulse every 2s
- Neutral (mark, stance): violet border

Stack: chips in a horizontal row, max visible 4, overflow shows `+N` chip with click/tap to expand.

Per combatant: row above HP bar in CombatOverlay; row under name in PartyStatusBar.

### 6.2 Initiative timeline

Horizontal strip at top of CombatOverlay. Each combatant = 48×48 portrait card, ordered by initiative. Current actor = scaled 1.1, brass underline, subtle bounce. Completed actors (already acted this round) = 0.5 opacity. Round counter on far left.

On tablet portrait: collapses to scrollable strip, current actor pinned center.

### 6.3 Range bands (combat scene)

Three vertical zones in CombatOverlay backdrop:

```
┌──────────────┬─────────────┬─────────────┐
│  Long        │  Short      │  Melee      │
│              │             │  [front row]│
│              │             │  [back row] │
│  Enemies →   │  Enemies →  │  Enemies →  │
└──────────────┴─────────────┴─────────────┘
```

Each band = subtle gradient backdrop. Faint vertical rule between. Selected target's band highlights with violet bottom border. Out-of-range attacks: target dims + cursor shows "out of range" icon.

### 6.4 Compass / facing (exploration HUD)

Top-center: brass ring 56px, N/E/S/W tick marks, needle rotates with `Player.Facing`. Below ring: small mono coords `(x, y)` and current dungeon name.

### 6.5 Threat / encounter pacing

Subtle: a thin "tension bar" rides under the compass, fills as `_stepsSinceEncounter` grows. At ≥75% full, gains slow pulse. At 100%+ (encounter due): bar throbs red.

Player never sees raw numbers but reads tempo. Prevents purely-random feel.

### 6.6 Resource pips (party rail)

Per-character vertical strip of three resource pips below HP bar — class-dependent:

| Class | Pip A | Pip B | Pip C |
|---|---|---|---|
| Bonewarden | Bone fragments | — | — |
| Stillblade | Stances (3 states) | — | — |
| Cauterist | Cautery supplies | — | — |
| Hollow | Shadow charges | — | — |

Pip rendering: 8px squares, filled brass when available, hollow when spent. Cap visible at 6 — beyond shows numeric.

### 6.7 Objective ticker

Single line at top of ExplorationHUD: current mission text, brass underline. Updates ease-in/out crossfade when content changes.

### 6.8 Network / save state

Top-right corner, 20px icon row:
- Connection: green dot (live), amber (reconnecting, animated), red (failed). Click → reconnect.
- Unsaved changes: small ink-blot icon, brightens when state diverged from last save.
- Saving: brief spinner replaces save icon during save.

Hover reveals tooltip with last save time + reconnect attempts.

## 7. Flash Alerts (transient FX)

Distinct from indicators: animate in, peak ~200–400ms, animate out. Never block input. Spawned by events emitted on a global `FxBus`.

### 7.1 Damage / heal numbers

Spawn at target's screen position (combatant portrait or 3D billboard projection). Float upward 32px over 700ms, fade last 200ms.

Variants:
- **Damage**: bad color, mono 1.5rem, slight wobble
- **Crit**: bad color, mono 2.25rem, brass stroke outline, 100ms shake at spawn, `!` suffix
- **Heal**: good color, prefixed `+`
- **Miss**: ink-dim, italic body font, "miss"
- **Dodge**: violet, italic, "dodged"
- **Resist X**: info color, small caps, e.g. "RESIST FIRE"
- **Status applied**: chip mini (status icon + name) drifts up

Stagger: multiple numbers from same target offset 80ms each so they don't overlap.

### 7.2 Hit / impact

On hit, the **target portrait** in PartyStatusBar or CombatOverlay flashes bad color overlay 120ms then fades. On crit, brief screen shake (3px max, 180ms) — disabled with `prefers-reduced-motion`.

### 7.3 Damage vignette

Full-screen red vignette pulse when **any party member** takes damage. Opacity scales with `damage / maxHp`. Single pulse 180ms in, 320ms out. Stacked damage within 100ms collapses to single pulse with max opacity.

### 7.4 Low-HP warning

When any party member's HP drops below 25%, the screen edges gain a slow red breath (1.2s loop, opacity 0.0→0.18). Persists until HP rises above 25% or character dies. Companion: PartyStatusBar portrait gains red border + slow pulse.

### 7.5 Level-up burst

Brass radial burst behind character portrait, 800ms, with mono "+1 LEVEL" toast. Sound (later) and a one-time confetti of small bone-shard particles (~30) drifting up 1200ms. Triggers `<Modal>` to confirm stat allocation (Phase 1.5; Phase 1 auto-applies).

### 7.6 Loot / pickup popup

Bottom-center toast (toast layer). Inline icon + name + quantity, italic body for flavor. Stacks vertically. Auto-dismiss 3s.

### 7.7 Save / load / disconnect toasts

Top-right toast strip:
- Save success: brass check, "Saved."
- Save fail: bad x, "Save failed" + reason. Auto-dismiss 8s.
- Disconnect: amber wifi, "Reconnecting…", persists until resolved.
- Reconnected: brief good check, "Connected.", 2s.

### 7.8 Encounter incoming

Brief 500ms fade-to-black-with-clash-of-violet then CombatOverlay mounts. Two phases: dungeon view dims to 0.3, brass title "Encounter — {name}" types in over 240ms, then full overlay enters from below.

### 7.9 Scene transition

Generic transition between modes:
- Exploration → Combat: see 7.8.
- Exploration → Town: parchment-fold wipe (2 panels meet center, 320ms). New scene under.
- Town → Dungeon: ink bleeds from center outward, 600ms, into entrance scene.
- Combat → Exploration (victory): brass radial pulse outward, fade 400ms.

All transitions skippable with `prefers-reduced-motion` (instant cut + flash) and with a setting toggle.

## 8. Atmospherics

Persistent ambient layer. Always on unless disabled in settings ("Reduce Atmosphere").

### 8.1 Vignette

Static radial gradient `radial-gradient(ellipse at center, transparent 55%, #00000088 100%)` on AtmosLayer, multiply blend. Stronger in Dungeon mode, lighter in Town.

### 8.2 Dust motes

CSS-only: 12 small `<span>`s with subtle drift animations (random `translateY` -200 → 600 over 12–22s, opacity sinusoidal). Use `transform` + `will-change: transform` for perf. Disabled in low-power mode.

### 8.3 Torch flicker

In Dungeon mode, AtmosLayer applies a 0.04 max-opacity warm orange overlay with a perlin-like flicker (precomputed 1s loop). Synced with three.js torchLight via the same flicker buffer (FxBus broadcasts current intensity).

### 8.4 Combat tension shading

Background of CombatOverlay tints toward bad color as average party HP drops:

```
avgHpPct >= 75: no tint
50-75%: subtle bad tint 0.04
25-50%: 0.08
<25%:   0.12 + slow 1.6s pulse
```

### 8.5 Town warmth

In Town, AtmosLayer applies a warm cream overlay (0.04) and disables vignette. Town feels lighter — safe.

### 8.6 Weather / world-state hooks

Atmospherics expose hooks for future world-state effects:
- `data-bloom-level="{0..3}"` on `<body>` shifts AtmosLayer tints toward violet/green (the Blooms).
- `data-faction-control="ash|tithe|engineers|..."` shifts ambient hue subtly. Reserved for Phase 2.

## 9. Motion & Sound Hooks

All animations route through CSS variables (`--dur-*`, `--ease-*`) so a single setting can scale them. Sound is a no-op stub in Phase 1: `Fx.play('hit_crit')` resolves to nothing but lands the call sites for Phase 1.5 audio.

`prefers-reduced-motion: reduce`:
- All `dur-*` → 0
- Vignette static
- Dust motes hidden
- Damage vignette → 60ms flash
- Screen shake disabled
- Scene transitions → instant

## 10. Accessibility

- All interactive elements ≥44×44 hit area, focus ring always visible (brass 2px).
- Color never sole signal: damage = number, status = icon + text, low HP = pulse + label, threat = bar fill + pulse, miss = text.
- Contrast: text on `--c-bg-1` ≥ 4.5:1 (verified); `--c-ink-dim` capped to body/secondary roles only.
- Live regions: combat log uses `aria-live="polite"`; encounter alerts use `aria-live="assertive"`.
- Keyboard: every action reachable. Tab order documented per screen. Skip-to-content on game start.
- Captions: damage/heal numbers also recorded into combat log for screen-reader replay.

## 11. Implementation Plan (Phase 1.5)

| Step | Touches | Output |
|---|---|---|
| 1 | `src/client/src/app.css` | replace tokens with §2 set, update existing components to reference new names |
| 2 | `src/client/src/ui/layers/` | create 5 layer components, refactor `App.svelte` to compose them |
| 3 | `src/client/src/ui/primitives/` | `Button`, `Card`, `Modal`, `Bar`, `InventoryGrid`, `InventorySlot` |
| 4 | `src/client/src/fx/FxBus.ts` | event bus + Svelte stores for damage numbers, toasts, vignette pulses |
| 5 | `src/client/src/ui/atmos/` | `AtmosLayer.svelte`, `DustMotes.svelte`, `TorchFlicker.svelte`, vignette CSS |
| 6 | Refactor `CombatOverlay`, `PartyStatusBar`, `ExplorationHUD`, `TownMenu` onto primitives + tokens |
| 7 | Add icon sprite `src/client/public/icons.svg` + minimal Phase 1 set |
| 8 | Storybook-like dev route `/app/dev/styleguide` showing every primitive + state |
| 9 | Playwright visual snapshots for primitives at 768 / 1024 / 1280 widths |

Backwards compatibility during migration: keep old class names as aliases (`.btn` → uses Button primitive markup) for 1–2 commits, then remove.

## 12. Out of Scope

- Phone (<768px) layouts.
- Light theme.
- Animated illustrated portraits (Phase 3).
- WebGL post-processing for atmospherics (Phase 2 — currently CSS only).
- Sound design (Phase 1.5 audio spec, separate doc).
- Localization (Phase 2 — strings remain inline English for Phase 1).
