# Build Plan — The Reach

Implementation plan for a first-person party-based dungeon crawler. Desktop app (Photino) with a .NET backend and browser-based frontend.

## Technology Stack

| Layer | Choice | Rationale |
|---|---|---|
| Rendering | Three.js | Mature WebGL, good for low-poly. Grid-based dungeon view doesn't need a full engine. |
| UI Framework | Svelte | Compiled reactivity, no virtual DOM competing with Three.js render loop. Small output. |
| Frontend Language | TypeScript | Type safety, good Three.js typings, standard for non-trivial browser projects. |
| Backend Language | C# / .NET | Zero-alloc combat resolution, dungeon assembly, binary content packs. Full local compute. |
| Desktop Shell | Photino | .NET host + webview. No network latency, full local resource access. |
| Build (frontend) | Vite | Native TS/Svelte support, fast HMR for game feel iteration. |
| Build (backend) | dotnet CLI | Standard .NET build pipeline. |
| Transport | WebSocket + REST | WebSocket for game simulation (stateful, event-driven). REST for static content (cacheable). |
| Content Authoring | JSON | Source of truth. Matches LLM pipeline output (Phase 3). Human-editable. |
| Content Runtime | Binary packs | JSON compiled to binary at build time. Zero-alloc reads in .NET. |
| Client Cache | IndexedDB | Content chunks cached locally. REST ETags for invalidation. |
| Settings | KDL | Player preferences, keybindings. Synchronous reads at startup via localStorage mirror. |
| Testing | xUnit + snapshots + Playwright | Logic unit tests, combat replay snapshots, headless UI smoke tests. |

## Architecture

```
┌─────────────────────────────────────┐
│           Photino Shell             │
├──────────────┬──────────────────────┤
│  .NET Host   │   Webview (Browser)  │
│              │                      │
│  Game Engine │   Three.js Renderer  │
│  Combat Res. │   Svelte UI Panels   │
│  Dungeon Asm │   IndexedDB Cache    │
│  Faction AI  │                      │
│  Save/Load   │                      │
│  Content Srv │                      │
├──────────────┴──────────────────────┤
│  WebSocket (state)  REST (content)  │
└─────────────────────────────────────┘
```

The .NET host owns all game state. The browser is a renderer and input handler. No game logic runs client-side. The frontend sends player actions over WebSocket, receives state updates, and renders them.

### Rendering State Boundary

The server computes **visibility** (line-of-sight and explored state) and sends it as part of the dungeon grid. The client does not run its own LOS algorithm. At 40×40 grids this is trivial server-side; if dungeons grow larger than 60×60, visibility computation can be moved to the client without protocol changes (the `visible` flag becomes client-authoritative).

## Project Structure

```
rpc/
├── docs/
│   ├── design/              # game design spec (01-09)
│   └── plans/               # build plans (this folder)
├── src/
│   ├── engine/              # .NET backend
│   │   ├── RPC.Engine/      # game logic library (combat, dungeon, factions)
│   │   ├── RPC.Content/     # content loading, binary pack reader
│   │   ├── RPC.Host/        # Photino shell, WebSocket/REST server
│   │   └── RPC.Tests/       # xUnit + snapshot tests
│   └── client/              # TypeScript/Svelte frontend
│       ├── renderer/        # Three.js dungeon view, camera, grid movement
│       ├── ui/              # Svelte components (combat, inventory, map, menus)
│       ├── net/             # WebSocket client, REST content fetcher
│       ├── cache/           # IndexedDB content cache manager
│       └── types/           # shared type definitions (mirroring .NET models)
├── content/                 # JSON source files
│   ├── segments/            # room segment definitions
│   ├── encounters/          # encounter tables
│   ├── items/               # items and equipment
│   └── npcs/                # NPC definitions
├── tools/                   # build-time tooling
│   └── content-pack/        # JSON → binary content pack compiler
```

### Key Boundaries

- **RPC.Engine** is a pure library with no I/O. Takes state in, returns state out. This is what snapshot tests target.
- **RPC.Host** handles Photino, networking, and file I/O. Thin layer over the engine.
- **content/** is separate from src/ — data, not code. The content pack compiler is a build tool.
- **types/** in the client mirrors .NET models. Manual maintenance in Phase 1, codegen from C# in Phase 2+.

## Testing Strategy

| Level | Target | Tool | What It Catches |
|---|---|---|---|
| Unit | Combat resolution, dungeon assembly, faction state machines | xUnit | Logic bugs, balance regressions |
| Snapshot | Combat replays (action sequence → final state) | xUnit + custom harness | Regression in combat outcomes after tuning changes |
| Integration | Content pipeline (JSON → binary → loaded state) | xUnit | Serialization bugs, content format drift |
| UI Smoke | Critical flows: navigate, fight, inventory, save/load | Playwright | Broken UI after refactors, WebSocket protocol drift |
| Manual | Game feel, pacing, visual quality | Playtesting | Everything automated tests can't measure |

## Documents

1. [Overview](00-overview.md) — this document
2. [Phase 1: Core Loop](01-phase-1.md) — dungeon navigation + combat
3. [Phase 1.5: Minimum Viable Strategy](02-phase-1.5.md) — formation, factions, synergies
4. [Phase 2: Strategic Depth](03-phase-2.md) — full roster, overworld, campaign system
5. [Phase 3: Full Vision](04-phase-3.md) — LLM generation, faction AI, full content
6. [Complications](05-complications.md) — spec/implementation conflicts and pre-build decisions
