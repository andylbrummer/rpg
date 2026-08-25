# Accessibility — Design Spec
Date: 2026-05-10
Status: design — consolidates a11y touched across multiple specs into single normative reference
Depends on: design-system, settings-keybinds, audio-architecture, onboarding
Scope: WCAG 2.2 AA target, motor / visual / auditory / cognitive accommodations, assistive tech support, validation.

## 1. Target

WCAG 2.2 Level AA where applicable to game UI. Some criteria don't map cleanly to games (e.g., reading order in a 3D scene); document deviations.

Beyond WCAG: provide accommodations for common gaming accessibility needs (color blindness, slow reaction, photosensitivity, hearing loss).

## 2. Visual

### 2.1 Color

- All status communication uses **icon + color + text**, never color alone (per design-system spec §10).
- Contrast: text on background ≥ 4.5:1 (WCAG AA). UI element borders ≥ 3:1.
- `--c-ink-dim` capped to body / secondary; never primary actions.
- Color-blind palettes:
  - `none` (default)
  - `deuteranopia` (red-green; ~6% males)
  - `protanopia` (red-green)
  - `tritanopia` (blue-yellow; rare)

Color-blind modes shift accent hue assignments. `settings.accessibility.colorBlind` setting (per settings-keybinds spec §2).

Implementation: CSS variable swap-out per mode:

```css
:root[data-colorblind="deuter"] {
  --c-bad: #d57766;       /* shift bad toward orange */
  --c-good: #4d8db0;      /* shift good toward blue */
}
```

### 2.2 Text scaling

- `settings.accessibility.largeText` bumps `--font-scale` to 1.2 and increases line height.
- All UI scales fluidly; no fixed-pixel text.
- Tooltips, modals, captions all respect scale.
- Minimum readable size at 1.0 scale: 14px body.

### 2.3 Photosensitivity

- Damage vignette + screen shake disabled with `prefers-reduced-motion` or `settings.motion.reduce`.
- No strobing effects > 3 Hz anywhere.
- `combat tension shading` smoothed to ≤ 1 Hz pulse.
- `settings.atmosphere.combatTension` toggle to disable entirely.

### 2.4 High contrast

`settings.accessibility.highContrastText`:
- Boost text contrast to 7:1 (WCAG AAA).
- Add 1px text shadow for separation from busy backgrounds.
- Strong outline on all interactive elements (current 2px brass becomes 3px white).

## 3. Motor

### 3.1 Hit targets

- All interactive elements ≥ 44×44 CSS px (per design-system §3). Maintained at every breakpoint.
- Drag operations have fallback tap-to-pick + tap-to-place mode (`settings.accessibility.dragAlternative` toggle).
- Long-press duration configurable: default 450ms; `settings.accessibility.longPressMs` 200-1000.

### 3.2 Combat speed

- Game is turn-based — no timing pressure inherent.
- AI animations: `settings.gameplay.autoAdvanceAi` skips visual delay between enemy turns.
- Animation speed: `settings.gameplay.combatSpeed` (slow/normal/fast) multiplies all combat-related animation durations.

### 3.3 Sticky keys

Browser-level handles. We don't intercept system keys (Ctrl+, Shift+, etc. behave as accessibility tools expect).

Keybind dispatcher (settings-keybinds spec §4.2) supports single-key bindings without chord requirements where possible. Combat actions: 1-6 single keys, not requiring modifiers.

### 3.4 Hold-to-move avoidance

No press-and-hold required for movement — single-press grid movement (already by design).

## 4. Auditory

### 4.1 Captions

`settings.accessibility.captionCombatEvents` enables (per audio-architecture spec §6). Captions show:
- Damage / heal / miss events
- Status applied / removed
- Synergy triggers
- Encounter alerts
- Death / downed

Caption layer rendered at atmos z-band, fixed bottom, configurable size.

### 4.2 Visual sound cues

Beyond captions: all auditory signals have visual analog:
- Tension music ramp → tension bar fill.
- Combat enter sting → fade-to-clash transition.
- Damage sfx → damage vignette + number.
- Synergy chord → brass burst.

No game-relevant info conveyed by sound alone.

### 4.3 Audio adjustments

- Per-bus volume (per audio spec §10).
- Music + ambience independent of SFX so user can mute music without losing combat audio cues.
- Mono output mode (Phase 2): `settings.audio.mono` collapses stereo for hearing-impaired single-ear use.

## 5. Cognitive

### 5.1 Difficulty + clarity

- `settings.gameplay.confirmDestructive` requires double-confirm on permanent actions (resurrection, drop quest item, dismiss character).
- Mission objectives always visible in top ticker.
- Field Notes captures all learned info — no need to remember.
- Per-state UI: e.g., combat shows only combat actions; exploration hides combat-only controls.

### 5.2 Onboarding

Per onboarding spec: contextual nudges, help reference, diegetic teaching. Player-controllable opt-out.

### 5.3 Reading load

- Lore documents are skippable (modal close = mark read).
- Dialogue choices visible all at once (no auto-scroll out of view).
- Long lore split into pages with explicit Next/Prev.
- `settings.accessibility.dyslexicFont` switches body font to OpenDyslexic (bundled).

### 5.4 Cognitive load reduction modes (Phase 2)

`settings.accessibility.simplifyCombatUI`:
- Hide secondary indicators (tension shading, atmospheric overlays).
- Larger action buttons.
- Auto-target lowest-HP enemy when no selection.

`settings.accessibility.showMissionPath`:
- Adds subtle compass-arrow to next mission tile in dungeon (violates "no quest markers" pillar; opt-in only, explicitly).

## 6. Assistive tech

### 6.1 Screen reader

ARIA roles on all UI elements:

- Buttons: `role="button"` + accessible name from label.
- Modals: `role="dialog"` + `aria-labelledby` + focus trap.
- Combat log: `aria-live="polite"`.
- Encounter alerts: `aria-live="assertive"`.
- Toasts: `aria-live="polite"`.
- Status chips: `role="img"` + `aria-label` describing status + duration.

Three.js scene: not navigable by screen reader. Provide text alternative ("Exploration mode — Old Calder Engine, position 17×4, facing East") via off-screen live region updated on state change. Player can hear position + tile facts.

### 6.2 Keyboard-only

All actions reachable via keyboard (per design-system §10):
- Tab order documented per screen.
- Skip-to-content link at top on game start.
- ESC always closes the topmost modal.
- Focus ring visible (2px brass, never removed).

No mouse-only interactions. Drag-drop has tap-tap fallback.

### 6.3 OS integration

- Windows Narrator + JAWS + NVDA tested Phase 2.
- macOS VoiceOver tested Phase 2.
- Linux Orca: target compatibility (depends on Photino webview accessibility tree, may be limited).

## 7. Settings UI

Settings → Accessibility section (per settings-keybinds spec §5):

```
┌─Accessibility──────────────────────────────────────────┐
│ Vision                                                 │
│  ☐ High contrast text                                  │
│  ☐ Large text (1.2× scale)                             │
│  ☐ Dyslexic-friendly font                              │
│  Color blind: [None ▾]                                 │
│                                                        │
│ Motion                                                 │
│  ☐ Reduce motion                                       │
│  ☑ Screen shake                                        │
│  ☑ Damage vignette                                     │
│                                                        │
│ Audio                                                  │
│  ☑ Caption combat events                               │
│  ☐ Mono audio                                          │
│                                                        │
│ Motor                                                  │
│  Long-press duration: [450ms ▾]                        │
│  ☐ Use tap-tap instead of drag                         │
│                                                        │
│ Cognitive                                              │
│  ☑ Confirm destructive actions                         │
│  ☐ Simplify combat UI                                  │
│  ☐ Show contextual hints (one-time per save)           │
│                                                        │
│ [Reset to defaults]                                    │
└────────────────────────────────────────────────────────┘
```

## 8. Validation

### 8.1 Automated

- axe-core run on every screen during Playwright tests.
- Lighthouse accessibility score ≥ 95 on key screens.
- Contrast checker job in CI flags violations.
- ARIA validator in CI catches malformed roles.

### 8.2 Manual

- Pre-release accessibility pass: full keyboard playthrough.
- Screen-reader spot check on critical flows.
- Color-blind simulator on key screens.

### 8.3 Tester pool

Phase 2 onwards: solicit accessibility testers from gaming-accessibility communities (e.g., AbleGamers). Compensate per their guidelines. Findings tracked in dedicated dart board.

## 9. Documentation

`docs/accessibility/` ships with:
- Feature matrix
- Known limitations (e.g., "3D scene navigation not screen-reader-friendly")
- VPAT (Voluntary Product Accessibility Template) Phase 2

## 10. Out of scope

- One-handed gamepad mapping (Phase 3 if controller spec lands).
- Eye tracking input.
- Voice commands.
- Switch-control support.
- BCI / specialized hardware.

These warrant standalone specs if pursued.
