# Photino Window Lifecycle — Design Spec
Date: 2026-05-10
Status: design — Phase 2 deliverable; Phase 1 single-window, default behavior
Depends on: settings spec, error-recovery spec
Scope: window state (size, position, fullscreen, focus, minimize, close), persistence, multi-window (deferred), keyboard focus, OS integration.

## 1. Phase 1 baseline

Current `RPC.Host/Program.cs` launches a single Photino window with default size. No persistence. No close-confirm. No state save on close.

This spec adds the structured lifecycle.

## 2. Window state model

```csharp
public record WindowState(
    int Width,
    int Height,
    int X,            // top-left
    int Y,
    bool Maximized,
    bool Fullscreen,
    string MonitorId  // for multi-monitor recovery
);
```

Persisted at `{appData}/rpc/window.kdl`:

```kdl
window {
    width 1280
    height 800
    x 200
    y 120
    maximized false
    fullscreen false
    monitor "DP-1"
}
```

Defaults: 1280×800 centered on primary monitor. Loaded synchronously at startup before window creation.

## 3. Lifecycle events

Phases:

```
Launch
  ├─► loadSettings → loadWindowState
  ├─► createPhotinoWindow(size, pos, restoreState)
  ├─► startServer (WS + REST) on random free port
  ├─► loadInitialScene (Vite dev / built bundle)
  └─► [Running]
        ├─ Focus event:
        │     • client.notify(focused)
        │     • server resumes audio (already-handled per audio spec)
        ├─ Blur event:
        │     • client.notify(blurred)
        │     • settings.audio.muteOnFocusLoss ⇒ master gain 0
        │     • pause music/ambience optional
        ├─ Minimize:
        │     • reduce render frame rate to 1 fps (skip three.js render)
        │     • pause sfx
        ├─ Restore: resume
        ├─ Resize: persist debounced 500ms
        ├─ Move: persist debounced 500ms
        ├─ Close-request:
        │     • dirtyCheck: any unsaved progress?
        │     • show confirm if dirty + settings.gameplay.confirmDestructive
        │     • else: autosave + close
        └─ Quit (after close): persist window state, flush logs
```

## 4. Implementation

`RPC.Host/Program.cs` skeleton:

```csharp
var settings = SettingsStore.Load();
var windowState = WindowStateStore.Load();
var window = new PhotinoWindow()
    .SetSize(windowState.Width, windowState.Height)
    .SetLocation(new System.Drawing.Point(windowState.X, windowState.Y))
    .SetMaximized(windowState.Maximized)
    .SetFullScreen(windowState.Fullscreen)
    .SetTitle("The Reach")
    .RegisterWindowFocusInHandler((s,e) => OnFocus())
    .RegisterWindowFocusOutHandler((s,e) => OnBlur())
    .RegisterWindowMinimizedHandler((s,e) => OnMinimize())
    .RegisterWindowRestoredHandler((s,e) => OnRestore())
    .RegisterSizeChangedHandler((s,e) => DebouncePersist())
    .RegisterLocationChangedHandler((s,e) => DebouncePersist())
    .RegisterWindowClosingHandler(OnCloseRequest);

await StartServer();
window.Load($"http://localhost:{port}/app");
window.WaitForClose();
PersistWindowState();
```

Phase 1 may stub focus/blur handlers if Photino bindings lack them on a platform; fall back to client-side `window.onfocus / onblur`.

### 4.1 Close confirm

`OnCloseRequest(args)`:

```csharp
if (state.IsDirty && settings.Gameplay.ConfirmDestructive) {
    args.Cancel = true;          // prevent immediate close
    SendEventToClient(new { type = "close_request" });
    // client shows modal; on confirm, client sends action quit_confirmed
}
```

Client receives `event:close_request`, opens a modal:

```
┌──────────────────────────────────────────┐
│ Quit The Reach?                          │
│ You have unsaved progress.               │
│                                          │
│ [Save & Quit]  [Quit without saving]     │
│ [Cancel]                                 │
└──────────────────────────────────────────┘
```

On Save & Quit: `action:save_game` then `action:quit_confirmed`. Server invokes `window.Close()`.
On Quit w/o saving: confirm sub-modal (double-confirm). Then `quit_confirmed`.
On Cancel: dismiss modal.

If `state.IsDirty == false`: close immediately, no modal.

### 4.2 Dirty check

`IsDirty = WorldStateHash != LastSavedStateHash`. Updated on every action / every save. Reused from determinism spec §4.

## 5. Multi-monitor & DPI

On startup, validate `WindowState.MonitorId` exists in current monitor list. If not (monitor disconnected), fall back to centering on primary.

DPI awareness: Photino on Windows respects manifest setting `Per-Monitor V2`. Three.js renderer uses `window.devicePixelRatio` already (capped at 2 per `DungeonRenderer.ts:51`). Resize handler updates renderer:

```ts
window.addEventListener('resize', () => {
  renderer.handleResize(container);
});
```

`DungeonRenderer.handleResize` exists. Already correct.

## 6. Fullscreen / borderless

Fullscreen toggle: F11 keybind (settings spec bindings table) or settings option.

Photino: `SetFullScreen(true)`. Save into `WindowState.Fullscreen` on toggle.

Exit fullscreen: F11 again or ESC twice (single ESC handled by Pause modal).

Borderless windowed (Phase 2 if requested): chrome-less window at monitor resolution; user-toggleable.

## 7. Multiple windows

Phase 1/2: single window. Spec'd here to forbid until needed.

Why deferred:
- Game state is single-player single-instance.
- Photino supports child windows but adds complexity for save/lifecycle.
- Tooling windows (level editor) Phase 3 may use child windows.

If needed Phase 3: each window is a fresh Photino instance with its own webview; main window remains authoritative for save/state.

## 8. Hotkeys reserved at OS level

These keys handled BEFORE client JS sees them:
- `Alt+F4` (Win) / `Cmd+Q` (Mac): triggers OS close → close request flow.
- `F11`: fullscreen.
- `Ctrl+Shift+I` (dev only): open Photino devtools.

Disabled in release: devtools off unless `RPC_DEV=1` env var set.

## 9. Single instance

Prevent double-launch. On startup:

```csharp
var mutex = new Mutex(true, "rpc-singleton-{userSid}", out var createdNew);
if (!createdNew) {
    // existing instance: focus its window via IPC (named pipe)
    PipeClient.Send("focus");
    return;
}
```

Existing instance listens on named pipe `\\.\pipe\rpc-{userSid}`. On message, calls `window.RestoreWindow()` + brings to front.

Allow override with `--allow-multiple` CLI flag for testing.

## 10. URL-scheme launching (Phase 2)

Register OS handler for `rpc://` URLs (for replay sharing, deep links to mission catalog):

```
rpc://replay/load?file=/path/to/file.rpcreplay
rpc://campaign/new?seed=12345
```

Single-instance pipe receives URL on second-launch attempt, dispatches to active window.

## 11. Tray / background mode

Out of scope. Game runs in foreground only.

## 12. OS notifications

Phase 2: when minimized, key events (mission completed, faction tick) trigger OS notification:

```csharp
window.SendNotification("Mission Completed", "Foothold at the Broken Engine");
```

Settings toggle `gameplay.osNotifications` (default off) gates this.

## 13. Suspend / sleep

When OS suspends (laptop close, sleep), `window.RegisterWindowSuspendHandler` (if available on platform) → save state + pause server timers. On resume, recompute time deltas (none, since turn-based).

Workaround if handler unavailable: server detects long gap in heartbeats from client (ping spec §8) and treats as suspend.

## 14. Tests

- Manual: launch → resize → close → relaunch → window restores to last size/pos.
- Manual: launch on secondary monitor → disconnect monitor → relaunch → window centered on primary.
- Manual: launch twice → second exits cleanly, first focuses.
- Playwright: simulate window blur → confirm audio mutes; focus → audio restores.
- xUnit: WindowStateStore parses/serializes/round-trips.

## 15. Out of scope

- Custom title bar / window chrome (Phase 3 if visual identity demands).
- Picture-in-picture mode.
- Touch / pen input window manipulation.
- Background music while window unfocused (intentional — focus loss mutes).
