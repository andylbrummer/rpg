# The Reach

A first-person party-based dungeon crawler for the browser. Grim fantasy, factional intrigue, procedural narrative.

## Technology Stack

| Layer | Choice |
|-------|--------|
| Rendering | Three.js |
| UI Framework | Svelte |
| Frontend | TypeScript + Vite |
| Backend | C# / .NET |
| Desktop Shell | Photino |
| Transport | WebSocket + REST |

## Project Structure

```
rpc/
├── docs/               # Game design documents
│   ├── design/         # Design specs (01-09)
│   └── plans/          # Build plans
├── src/
│   ├── engine/         # .NET backend
│   │   ├── RPC.Engine/     # Game logic
│   │   ├── RPC.Content/    # Content loading
│   │   ├── RPC.Host/       # Photino shell
│   │   └── RPC.Tests/      # Unit tests
│   └── client/         # TypeScript/Svelte frontend
│       ├── src/
│       │   ├── renderer/   # Three.js dungeon view
│       │   ├── ui/         # Svelte components
│       │   ├── net/        # WebSocket client
│       │   └── types/      # Type definitions
│       └── dist/           # Built frontend
├── content/            # JSON content files
│   ├── segments/       # Room segment definitions
│   ├── encounters/     # Encounter tables
│   ├── items/          # Items and equipment
│   └── npcs/           # NPC definitions
└── tools/              # Build-time tooling
```

## Building

```bash
./build.sh
```

## Running

### Development Mode (Browser)

Terminal 1 - Frontend dev server:
```bash
cd src/client
npm run dev
```

Terminal 2 - Backend with dev flag:
```bash
cd src/engine/RPC.Host
dotnet run -- --dev
```

### Desktop App

```bash
cd src/engine/RPC.Host
dotnet run
```

## Controls

- **W / ↑** - Move forward
- **A / ←** - Turn left
- **D / →** - Turn right

## Current Status

Phase 1 (Core Loop) - In Progress

- ✅ Project structure
- ✅ .NET backend with WebSocket server
- ✅ Three.js first-person renderer
- ✅ Grid-based dungeon navigation
- ✅ Basic room segment system
- 🔄 Combat system (next)
