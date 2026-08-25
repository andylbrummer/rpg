# Developer Experience — Design Spec
Date: 2026-05-10
Status: design — Phase 2 deliverable; Phase 1 baseline covered
Depends on: content-pipeline (hot reload), websocket-protocol (debug actions), determinism-replay (replay tool), settings-keybinds (debug flags)
Scope: dev mode flag, debug overlay, REPL, scenario launcher, time controls, deterministic seeds, content live-edit, profiler hooks. Optimize iteration speed for designers + engineers.

## 1. Dev mode activation

`DOTNET_ENVIRONMENT=Development` AND/OR `RPC_DEV=1` env var enables dev features. Default off in release builds (`dotnet publish -c Release` strips dev code via `#if DEBUG`).

Client equivalent: `?dev=1` URL param OR running against Vite dev server (not built bundle).

Both routes converge to a single `IsDevMode` flag exposed by `GameState`.

## 2. Debug overlay

Toggleable client overlay, top-right corner, transparent black:

```
┌─Debug (F12 toggle)────────────────┐
│ FPS:        60 (avg 58)           │
│ State seq:  142                   │
│ Mode:       Exploration           │
│ Pos:        (17, 4) E             │
│ Turn:       7                     │
│ Seed:       12345                 │
│ State hash: ab12cd34              │
│ WS RTT:     8ms                   │
│ Memory:     124 MB                │
│ Last fx:    damage_number         │
│ Pacing:     5 steps since enc     │
└───────────────────────────────────┘
```

`settings.debug` keys (per settings-keybinds spec §2) toggle individual lines. `F12` toggles overlay visibility.

Click any value → copies to clipboard.

## 3. Dev command palette

`Ctrl+Shift+P` opens a fuzzy command palette:

```
> 

  give_item bone_spear 1 kael
  give_gold 1000
  set_flag engine_repaired_old_calder
  force_encounter goblin-scouts
  level_up kael
  resurrect kael
  toggle_invincible
  teleport 32 32
  set_worldturn 22
  load_scenario combat_synergy_test
  reload_content
  trace_combat_ai true
```

Each command is a server action wired only in dev mode. Maps to `action:debug.<cmd>` with payload. Server validates dev flag; rejects in production builds.

## 4. Scenario launcher

`content/scenarios/<id>.json` defines a saved state to jump into:

```json
{
  "id": "combat_synergy_test",
  "name": "Test: Tithe Link + Pyre synergy",
  "description": "Party of 4 at L3, combat against 2 bone archers + 1 rat.",
  "savedState": "scenarios/saves/combat_synergy_test.rpcsave"
}
```

Dev menu shows list. Picking loads the .rpcsave directly + sets active.

Phase 1 minimum: 10 scenarios covering critical-path combat, dungeon, town, error states.

Phase 2 expansion: each subsystem owns its scenarios (faction phase transitions, mission outcomes, level-up flows).

## 5. Time controls

In dev mode, top-bar gains debug time controls:

```
[⏪] [▶ Play] [⏸ Pause] [⏭ Step] [⏩] | Turn 7 | [+ Turn] [- Turn]
```

- `⏸ Pause`: stops world-clock ticks (faction phase advancement, rumor expiry). Combat unaffected.
- `⏭ Step`: advances one world turn manually.
- `+/- Turn`: jumps turns; faction phases recompute on jump.

Purely dev affordance — release builds don't expose.

## 6. Content live-edit

Per content-pipeline spec §5:
- `FileSystemWatcher` on `content/` triggers re-validate + push.
- Client receives `event:content_updated`, refreshes affected modules.
- Active in-game state preserved unless structurally incompatible.

Developer workflow: edit a class json → save → see new ability immediately in next combat selection.

## 7. State inspector

Dev panel (right rail, toggle with `F11` — separate from fullscreen, since dev only):

```
┌─State Inspector──────────────┐
│ ▾ player                     │
│   x: 17, y: 4                │
│   facing: "E"                │
│ ▾ party                      │
│   ▾ [0] Kael                 │
│     hp: 17/17                │
│     statuses: []             │
│   ▸ [1] Sera                 │
│ ▸ dungeon                    │
│ ▸ missions                   │
│ ▸ factions                   │
└──────────────────────────────┘
```

JSON tree, expandable. Selecting a value lets developer edit (server validates + applies).

Click a missionId → opens its content def in a sub-panel. Click an enemyId → enemy stat block.

## 8. Combat trace mode

`settings.debug.traceCombatAi`: when on, every enemy turn logs:

```
[Combat trace] Bone Archer #2 turn:
  Situation: { selfHpPct: 0.6, enemyMeleeCount: 2, band: 2 }
  Stance: engage
  Action scores:
    longbow: 8.2 (damage_potential 5.4 + prefer_back 2.8)
    hold_ground: 2.1
    retreat: 1.0
  Picked: longbow → target Sera (back row, 0.4 hp)
```

Outputs to console + writes to `logs/combat-trace.log`. Used to debug AI behavior + tune heuristics.

## 9. Performance HUD

Toggle with `Ctrl+F12`:

```
┌─Perf HUD──────────────────────────┐
│ Frame:  16.4 ms (60fps)           │
│ Render: 8.2 ms                    │
│ Logic:  2.1 ms                    │
│ Idle:   6.1 ms                    │
│                                   │
│ Mem JS:    98 MB                  │
│ Mem GPU:   42 MB                  │
│                                   │
│ Server tick: 1.2 ms p50, 3.4 ms p99 │
│ Combat:    0.6 ms / turn         │
│ AI:        0.2 ms / decision     │
│ Diff:      0.4 ms / state push   │
└───────────────────────────────────┘
```

Captures performance percentiles over a rolling 5s window.

## 10. Logging

`logs/` folder (per error-recovery spec §11):
- `host-{date}.log` — server log
- `client-{date}.log` — client-pushed log (Phase 2 client → server via `action:debug_log`)
- `combat-trace.log` — combat AI trace
- `content-watch.log` — content reloads

Level configurable per logger:

```
LOG_LEVEL=Trace dotnet run
```

## 11. REPL (Phase 2)

Optional embedded C# REPL via `Microsoft.CodeAnalysis.CSharp.Scripting`:

```
> gameState.Party.Members[0].CurrentHp
17
> gameState.Party.SetMember(0, gameState.Party.Members[0] with { CurrentHp = 1 })
> 
```

Access through dev panel; exposes `gameState`, `registry`, `services`. Sandboxed-ish (still C# — devs should not run on production saves).

## 12. Save inspector tool

`tools/save-inspect/`:
- CLI: `rpc-save inspect <file>` prints structured GameState.
- CLI: `rpc-save migrate <file>` runs migration chain through CurrentVersion.
- CLI: `rpc-save diff <a> <b>` structural diff between two saves.

Used for support + bug repro.

## 13. Network inspector

Dev mode adds a Network tab to dev panel:

```
┌─WebSocket frames────────────────┐
│ → action:move_forward (id:abc)  │
│ ← event:state_update (full)     │
│ ← event:fx ×3                   │
│ → action:turn_left              │
│ ...                             │
└─────────────────────────────────┘
```

Latency + size per frame. Click → frame payload viewer.

## 14. Component / token explorer

Per design-system spec §11 step 8: `/app/dev/styleguide` route shows every primitive in every state.

Phase 2 extends with token playground — live edit a CSS variable, see all primitives update.

## 15. Recording / playback

Per determinism-replay spec: `rpcreplay` artifact playable in tooling.

Dev tools: Record button in dev panel → captures actions to `recording.rpcreplay`; Play button replays in real-time or accelerated.

## 16. Tests

- Manual: every dev tool active when `RPC_DEV=1`, absent in release.
- xUnit: command palette commands fail gracefully when feature flag off.
- Manual: scenario launcher loads each authored scenario without errors.

## 17. Out of scope

- Hot-reload of C# engine code (use `dotnet watch` for that; not custom).
- Networked debug (multi-instance state sharing).
- Visual scripting editor.
- Built-in profiler GUI (use dotnet-trace + browser devtools).
