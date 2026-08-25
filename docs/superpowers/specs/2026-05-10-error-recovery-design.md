# Error Handling & Crash Recovery — Design Spec
Date: 2026-05-10
Status: design — Phase 2 deliverable; Phase 1 has basic try/catch in WS handler
Scope: server panic isolation, client error boundary, save-on-crash, auto-recover stale combat/dialog, observability. No silent failures.

## 1. Failure classes

| Class | Source | Strategy |
|---|---|---|
| **Recoverable action error** | bad input, rule violation | reject + `event:error`, continue session |
| **Engine exception** | bug in combat/dungeon/etc | isolate to current operation, save state, surface to user, keep session alive |
| **Content load error** | malformed RPK / missing id | crash early at boot with clear error; never start with broken content |
| **WebSocket disconnect** | network / client crash | server marks session paused; resume window 120 s (per protocol spec) |
| **Host process crash** | unhandled .NET exception | auto-restart child process; client reconnects; resume from last autosave |
| **Webview crash** | JS error escape, OOM | Photino host restarts webview; reconnect; reload UI |
| **Save corruption** | disk error, interrupted write | back up bad save; load previous autosave |
| **Out-of-disk** | full disk on save | reject save action with clear error; prompt user; never overwrite |

## 2. Server layers

```
WS handler
  └─ tries action dispatch
       └─ engine operation
            └─ pure domain functions
```

Boundaries:

- **WS handler** wraps every action dispatch in try/catch. Engine exceptions caught, logged with action id + payload + stack, emit `event:error code:server_error`. Session continues.
- **Engine operations** that mutate `GameState` are wrapped in a transaction-style snapshot:
  ```csharp
  var snapshot = state.Clone();
  try { state.ApplyAction(action); }
  catch (Exception ex) {
      state = snapshot;
      logger.LogError(ex, "Action {act} failed; reverted", action.Type);
      throw new EngineException(ex);
  }
  ```
- **Pure domain** (combat resolution, dungeon assembly) should not throw under normal input. If they do, it's a bug — log and rethrow up.

State revert avoids half-applied moves. Action becomes a no-op from the user's perspective + error toast.

## 3. Engine exception toast

`event:error` payload `code:server_error` with sanitized message + correlation id. Client toasts:

```
"Something went wrong — your last action didn't apply. Error ID: a1b3c2"
```

Correlation id maps to server-side log entry for support. Do NOT include stack traces in payload (PII / leak risk).

## 4. Auto-save on crash

`RPC.Host` registers `AppDomain.CurrentDomain.UnhandledException` + `TaskScheduler.UnobservedTaskException`:

```csharp
AppDomain.CurrentDomain.UnhandledException += (s, e) => {
    try { SaveSystem.Save(GameState, GetCrashSavePath()); }
    catch { /* best effort */ }
    Console.Error.WriteLine($"FATAL: {e.ExceptionObject}");
    Environment.Exit(1);
};
```

Crash save path: `saves/_crash/{utc-ts}.rpcsave`. Kept last 5; older deleted.

On next launch, host detects any `_crash` saves and offers to restore via UI banner in town.

## 5. Host process supervisor

Photino host can run with a supervisor wrapper (`RPC.Host.Supervisor`) Phase 2:

- Launches `RPC.Host` as child.
- On child crash (non-zero exit): restart up to 3 times within 5 min.
- Beyond limit: surface non-recoverable error window with crash log path.

Phase 1 ships single-process; supervisor is opt-in via launcher script.

## 6. Client error boundary

Svelte 5 doesn't have React-style error boundaries built in. Use a top-level error catcher:

```ts
window.addEventListener('error', (ev) => {
  Telemetry.report('window_error', ev.error);
  ErrorOverlay.show('UI error caught — reloading');
  setTimeout(() => location.reload(), 2000);
});
window.addEventListener('unhandledrejection', (ev) => {
  Telemetry.report('unhandled_rejection', ev.reason);
  ErrorOverlay.show('Background task failed — reloading');
  setTimeout(() => location.reload(), 2000);
});
```

Plus per-layer `try`/`catch` around render paths in critical components (CombatOverlay, DungeonRenderer). Components catch own errors and render a fallback card instead of taking down the whole UI.

`ErrorOverlay.svelte`:

```
┌──────────────────────────────────┐
│ Something went wrong.            │
│ Your progress is saved.          │
│ Reloading in 2 seconds…          │
│ [Reload now] [Copy error]        │
└──────────────────────────────────┘
```

## 7. Stale state recovery

If client receives `state_update` while in unexpected mode (e.g., client thinks Exploration, server says Menu after a crash recovery):
- Discard local state.
- Apply server state authoritatively.
- Toast: "Session resumed from save."

If a `CombatState` references a combatant id not in the current `Party`:
- Server detects on next tick.
- Combat aborted, mode → Exploration, party HP preserved as-is.
- Log warning + emit `event:error code:combat_stale` for telemetry.

## 8. Save corruption recovery

`SaveSystem.Load` already catches exceptions (post-2026-05-07 fix). Extend:

1. Try load primary save.
2. On parse failure: rename corrupt file to `{path}.corrupt.{utc-ts}`.
3. Try `_autosave/latest.rpcsave`.
4. If still failing: try second-latest autosave.
5. If all fail: prompt user with options [Start new game] [Restore from crash save] [Browse saves folder].

Never load partially. Always validate Version field + clamp ranges (existing logic).

## 9. Autosave policy

`settings.gameplay.autoSave`:

| Setting | Behavior |
|---|---|
| `town` (default) | save on town entry |
| `checkpoint` (Phase 2) | save on town entry + dungeon checkpoints (defined as setpiece room entry) |
| `off` | manual only |

Always: save before risky operations (dungeon entry, combat enter — saved to `_autosave/pre_combat.rpcsave`).

Rotation: keep last 5 autosaves in `_autosave/`. Manual saves separate.

## 10. Disk-full handling

Save attempts space-check before writing:

```csharp
var diskFree = new DriveInfo(Path.GetPathRoot(path)).AvailableFreeSpace;
if (diskFree < 50 * 1024 * 1024) {
    return SaveResult.InsufficientSpace;
}
```

User toast: "Cannot save — disk full. Free at least 50 MB." Save action rejected; game continues.

## 11. Logging

`RPC.Host`:
- Structured logging via `Microsoft.Extensions.Logging`.
- Console + rolling file `logs/host-{date}.log` (7 days retention).
- Levels: Trace (verbose action dispatch), Debug (state changes), Info (mode transitions), Warn (recoverable errors), Error (engine exceptions, save failures), Critical (crash, fatal).

Client:
- Same levels via `console.{trace|debug|info|warn|error}`.
- Captured to in-memory ring buffer (last 200 entries) for crash report attachment.

No logs include save content, item ids beyond debug level, or PII. Player name allowed (it's their own data).

## 12. Crash report

Phase 2 optional feature: "Send crash report" button on Error Overlay assembles:
- Game version + build hash
- Content RPK hash
- Last 50 log entries (client + server tail)
- Stack trace
- Action log tail (10 entries)
- Settings (sanitized — no debug.invincible etc.)

Saved to `crash-reports/{utc-ts}.zip`. Phase 2 ships local-only (file). Phase 3 may add opt-in upload to a sink.

## 13. Tests

- xUnit: action that throws → state reverted + error event emitted.
- xUnit: save with simulated IO exception → backup created + previous restored.
- xUnit: load with corrupted file → fallback to autosave.
- Integration: kill host mid-action → restart → state restored from last autosave.
- Playwright: induce client JS error → ErrorOverlay appears → reload preserves game.

## 14. Anti-patterns to avoid

- **Silent catch-all in domain code** — pure logic should throw on bugs, not return defaults.
- **Catch + console.log + continue** — hides bugs. Either handle properly or rethrow.
- **--no-verify in saves** — never skip hash/clamp checks.
- **Partial writes** — save is atomic (write to tmp, fsync, rename).
- **Background timer that swallows errors** — every interval has explicit error path.

## 15. Out of scope

- Distributed tracing / OpenTelemetry (Phase 3 if needed).
- Multi-user crash isolation.
- Hardware fault recovery.
- Bug bounty / reproducer system.
