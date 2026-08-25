# Codebase Fixes — Design Spec
Date: 2026-05-07

## Scope
20 issues from Phase 1 review. All 20 addressed. Grouped into 4 commits.

## Commit 1: security/correctness

### WS frame accumulation (GameServer.cs)
Buffer fixed at 4096. `ReceiveAsync` returns `EndOfMessage=false` for large messages — current code processes the partial buffer. Fix: accumulate frames in a `MemoryStream` until `result.EndOfMessage`, then deserialize.

### SaveSystem: log + clamp (SaveSystem.cs)
- catch block: log exception to `Console.Error` before returning false
- After deserialization, clamp each party member: `Level = Math.Max(1, s.Level)`, `CurrentHp = Math.Clamp(s.CurrentHp, 0, 9999)`, `Xp = Math.Max(0, s.Xp)`, `Row = Math.Clamp(s.Row, 0, 1)`
- Add Version check: if `data.Version != "1"`, log warning and return false

### .gitignore
Add: `.agnt`, `.agnt/`, `static-server.cjs`

## Commit 2: quality

### Remove debug Console.WriteLine (GameServer.cs)
Remove lines 305-307 (`ENTER_COMBAT triggered`, `ENTER_COMBAT result`). These log on every combat trigger, expose game state, pollute prod output.

### Fix generate_dungeon action (GameServer.cs)
The `generate_dungeon` case hardcodes `"broken_engine"`, ignoring any dungeonType. Since `enter_dungeon` already handles typed entry correctly, remove the `generate_dungeon` case entirely from the switch (it's superseded).

### Delete Class1.cs
Remove `src/engine/RPC.Engine/Class1.cs` (empty placeholder).

### TownMenu NaN guard (TownMenu.svelte)
`(member.hp / member.maxHp) * 100` → `(member.hp / (member.maxHp || 1)) * 100`

## Commit 3: perf

### DungeonRenderer shared texture leak fix (DungeonRenderer.ts)
`clearTiles()` calls `(mat as any).map.dispose()` on each tile material. Since `wallTexture`, `floorTexture`, `doorTexture` are shared class fields referenced by all tile materials, this disposes the shared texture on first clear — all subsequent tile renders use disposed textures.

Fix: remove the `map.dispose()` call from `clearTiles()`. Shared textures are owned by the class instance and only disposed in `dispose()`.

### ExploredTiles unbounded cap (GameState.cs)
`ExploredTiles: HashSet<string>` grows without bound. Cap at 4096: before `ExploredTiles.Add(...)`, if size >= 4096, clear oldest 512 entries. Simple implementation: track insertion order via a parallel `Queue<string>`.

### GameClient exponential backoff (GameClient.ts)
`setTimeout(() => this.connect(), 1000 * this.reconnectAttempts)` → `Math.min(Math.pow(2, this.reconnectAttempts) * 1000, 30000)` with no change to maxReconnectAttempts (5).

## Commit 4: nits

### DungeonRenderer console.log removal (DungeonRenderer.ts)
Remove 9 console.log calls: constructor init/complete (lines 34, 62, 90), updateState (205), renderDefaultScene (224, 251), renderTiles (268, 299), handleResize (445). Keep no logs.

### GameClient console.log cleanup (GameClient.ts)
Remove: constructor port log (15), connect/disconnect logs (20, 26, 33, 45, 60), per-action send log (74), warn on closed (78). Keep: `console.error` on parse failure (39) and max-reconnect failure (63).

### Rename viewRadius in server (GameServer.cs)
`var viewRadius = 8` at line 500 → `var sendRadius = 8`. Eliminates confusion with `GameState.viewRadius = 3` (exploration reveal). These serve different purposes.

### Remove dead fetchContent (GameClient.ts)
`fetchContent<T>` method (lines 93-99) has no call sites. Remove.

## What was NOT fixed
- Dungeon gen always produces same 4-segment layout regardless of type — Phase 2 content work.
- Lock broadcast pattern (already correct — snapshot outside lock, send outside lock).
- Path traversal in ContentPackReader — not a real issue; RPK paths are dict keys, no fs access.
- SPA fallback masking API errors — static file handler only applies to `/app` and `/assets` paths; unknown `/api/*` returns 404 correctly.
