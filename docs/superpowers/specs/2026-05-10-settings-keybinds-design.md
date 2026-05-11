# Settings & Keybinds — Design Spec
Date: 2026-05-10
Status: design — not yet implemented
Depends on: CLAUDE.md mandate (KDL for prefs/keybinds, JSON for content/API)
Scope: schema, storage location, runtime sync, rebind UX, defaults, migration. Phase 1 = client-side KDL with localStorage mirror for synchronous boot; Phase 2 = host-side persistent KDL on disk.

## 1. Storage

### 1.1 Phase 1 — client-only

- Canonical: KDL string stored in `localStorage` under key `rpc.settings.v1.kdl`.
- Loaded synchronously at app start (before Svelte mount) so first paint already respects theme/motion/keys.
- Written on any change with 300ms debounce.

### 1.2 Phase 2 — host-side

- File: `{appData}/rpc/settings.kdl`. On Linux `${XDG_CONFIG_HOME:-~/.config}/rpc/settings.kdl`; macOS `~/Library/Application Support/rpc/settings.kdl`; Windows `%APPDATA%\rpc\settings.kdl`.
- Client still keeps localStorage mirror for boot speed.
- On socket `hello`, server sends current authoritative settings; client diff-merges into local mirror.
- Server writes file atomically (write-tmp-then-rename) on change.

## 2. KDL schema

```kdl
// rpc settings — KDL v1
version "1"

display {
    theme "dark"          // "dark" | "light" (Phase 2)
    scale 1.0             // ui scale, 0.85..1.5
    showFps false
}

motion {
    reduce false          // true overrides --dur-* to 0 (per design-system spec)
    screenShake true
    transitions "full"    // "full" | "fast" | "instant"
}

atmosphere {
    vignette true
    dustMotes true
    torchFlicker true
    damageVignette true
    combatTension true
}

audio {
    master 0.8            // 0..1
    music 0.7
    sfx 0.9
    ambience 0.6
    muteOnFocusLoss true
}

gameplay {
    autoSave "town"       // "town" | "checkpoint" | "off"
    confirmDestructive true
    ironman false         // Phase 3
    combatSpeed "normal"  // "slow" | "normal" | "fast"
    autoAdvanceAi true    // skip enemy turn animations after first frame
    showDamageNumbers true
}

accessibility {
    highContrastText false
    largeText false       // bumps ui scale 1.2 plus increases gutter
    captionCombatEvents true   // mirrors fx into log for screen reader
    colorBlind "none"     // "none" | "deuter" | "prot" | "trit"
}

keybinds {
    bindings {
        moveForward "KeyW" "ArrowUp"
        moveBackward "KeyS" "ArrowDown"
        turnLeft "KeyA" "ArrowLeft"
        turnRight "KeyD" "ArrowRight"
        turnAround "KeyX"
        strafeLeft "KeyQ"
        strafeRight "KeyE"
        interact "KeyF" "Enter"
        inventory "KeyI"
        partyManagement "KeyP"
        characterSheet "KeyC"
        missionJournal "KeyJ"
        map "KeyM"
        save "F5"
        load "F9"
        pause "Escape"
        cycleParty "Tab"
        combatActionAttack "Digit1"
        combatActionDefend "Digit2"
        combatActionCast "Digit3"
        combatActionItem "Digit4"
        combatActionFlee "Digit5"
        combatActionWait "Digit6"
        combatTargetNext "Tab"
        combatTargetPrev "ShiftLeft+Tab"
        combatConfirm "Enter" "Space"
        combatCancel "Escape"
    }
}

debug {
    showCoords false
    showSeed false
    forceEncounter false
    invincible false
}
```

Bindings accept 1–3 `KeyboardEvent.code` values. Modifiers expressed as `Shift+`, `Ctrl+`, `Alt+`, `Meta+` prefixes joined by `+`. Examples: `ShiftLeft+Tab`, `Ctrl+KeyS`.

### 2.1 Schema constraints

- `version` literal `"1"`. Bump for breaking change.
- Unknown nodes preserved on load + re-emitted on save (forward-compat for newer-app reading older file).
- Unknown keys within known nodes preserved.
- Out-of-range numerics clamped on load with `console.warn`.

## 3. Defaults

Defaults file: `src/client/src/settings/defaults.kdl` (literal KDL string baked into bundle). Loaded when no localStorage entry exists OR migration fails.

When loaded settings missing a node, defaults fill in. Saved file always has all nodes after one save cycle (canonicalization).

## 4. Client architecture

```
src/client/src/settings/
  defaults.kdl              // literal kdl
  parser.ts                 // minimal kdl reader (whitelisted)
  serializer.ts             // emit kdl
  schema.ts                 // typed Settings interface, clamps
  store.ts                  // Svelte store + persist debounce
  migrate.ts                // v0 (none) → v1, v1 → v2 stubs
```

KDL library: use `kdljs` (small, browser-friendly) or implement subset reader (~80 LOC for the predictable schema above). Subset suffices Phase 1 — only nodes-with-string/number/bool args, no slashdash, no annotations.

### 4.1 Svelte store

```ts
import { writable, derived } from 'svelte/store';
export const settings = writable<Settings>(loadSettings());
export const keybinds = derived(settings, $s => $s.keybinds);
export const motion = derived(settings, $s => $s.motion);
```

Components subscribe to slices. CSS variables for motion/atmosphere wired by a top-level effect that writes to `document.documentElement.style` whenever settings change.

### 4.2 Keybind dispatcher

`src/client/src/input/KeybindDispatcher.ts`:

```ts
type Action = 'moveForward' | 'turnLeft' | ... ;
type Handler = (action: Action, ev: KeyboardEvent) => boolean;

class KeybindDispatcher {
  private handlers: Handler[] = [];
  private context: 'global' | 'exploration' | 'combat' | 'menu' = 'global';
  setContext(c: typeof this.context) { this.context = c; }
  registerHandler(h: Handler) { this.handlers.push(h); return () => this.handlers.splice(this.handlers.indexOf(h),1); }
  onKeyDown(ev: KeyboardEvent) {
    const action = matchBinding(ev, currentKeybinds, this.context);
    if (!action) return;
    for (let i = this.handlers.length - 1; i >= 0; i--) {
      if (this.handlers[i](action, ev)) { ev.preventDefault(); return; }
    }
  }
}
```

Single global `keydown` listener feeds the dispatcher. Components register/unregister handlers on mount/unmount in stack order (last-registered first-hit). Replaces the ad-hoc handler in current `App.svelte`.

Context-aware: combat-only actions ignored outside combat mode. Set context from `gameState.mode` reactive.

### 4.3 Conflict detection

When user rebinds an action, the dispatcher checks existing assignments. Conflict = same `KeyboardEvent.code` + same modifier mask + overlapping context.

UI shows conflict warning before commit. User options: Swap (the other action loses the binding), Override (other action keeps but binding becomes shared — may misfire), Cancel.

## 5. Settings UI

Modal accessed from Pause menu or TopBar settings icon. Sections (tabs on ≥1024, accordion on tablet portrait):

1. **Display** — theme, scale, fps toggle, full-screen toggle (Phase 2 host).
2. **Motion** — reduce motion master toggle, individual atmosphere toggles, transitions speed.
3. **Audio** — master/music/sfx/ambience sliders, mute on focus loss.
4. **Gameplay** — autosave, confirm prompts, combat speed, AI animation skip.
5. **Accessibility** — high contrast, large text, captioning, colorblind palette.
6. **Keybinds** — searchable list of actions with current binding(s), rebind button per row.
7. **Debug** (only if `?debug=1` URL flag or env): coords overlay, seed display, force-encounter, invincible.

### 5.1 Rebind UX

Click rebind button → modal "Press a key…". Captures next `keydown`, normalizes to KDL form, validates (no Meta-only on Windows because of OS hotkey collisions; show warning), checks conflicts, applies.

ESC cancels rebind capture. The Escape binding itself can be rebound only after explicit confirm "Are you sure? You'll lose the pause hotkey unless you remap pause first."

Per-action up to 3 bindings (primary, secondary, tertiary). UI shows all three in row.

Reset to defaults: per-action and per-section reset buttons. Master reset = re-load defaults wholesale.

### 5.2 Live preview

- Motion toggles apply immediately (no save/apply step).
- Theme switch transitions over `--dur-3`.
- Audio sliders preview a click sample on release.
- Keybind rebind takes effect immediately.

## 6. Migration

`migrate.ts` exports `migrate(parsed: KdlDoc) → Settings`:

- `version` missing or `"0"` → treat as no settings, return defaults.
- `version "1"` → current.
- Future `version "2"` → translate down to v1 fields lossy + log warning if user opens older build with v2 settings.

Migrations chained: `v0 → v1 → v2 → ...`. Each migration pure function.

## 7. Photino host (Phase 2)

`src/engine/RPC.Host/Settings/SettingsStore.cs`:

- Reads/writes `settings.kdl` to OS appdata path.
- Watches file with `FileSystemWatcher` for external edits (text editor) → emits `event:settings_updated` to client.
- Exposes server actions:
  - `settings_get` → returns current settings.
  - `settings_set` → atomic write.

Client uses host store when present; falls back to localStorage when running web-only or before host responds.

## 8. Tests

- Unit (Vitest): parser round-trip for sample KDLs; default merge; unknown-node preservation; conflict detection; migration v0→v1.
- Playwright: open Settings → rebind moveForward to KeyK → close Settings → press K → player moves. Reset to defaults → press K → no movement. Confirm conflict modal when binding clashes.
- xUnit (Phase 2): `SettingsStore.Cs` atomic write under crash simulation; watcher emits events.

## 9. Validation

Settings file invalid (parse error or schema violation):
- Log error to `console.error` / `Console.Error`.
- Back up corrupt file to `settings.kdl.bak.{timestamp}`.
- Reset to defaults, persist clean defaults.
- Toast user: "Settings file was invalid; reset to defaults. Backup at {path}."

Never silent.

## 10. Out of scope

- Profile/multi-user settings.
- Cloud sync.
- Mod settings (Phase 2+ — mods get their own KDL files under `mods/<id>/settings.kdl`).
- Controller / gamepad mapping (Phase 2). Schema reserves a `gamepad {}` node for future.
- Touch gesture customization beyond toggle on/off.
