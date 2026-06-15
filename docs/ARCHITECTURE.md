# The Reach — Engine Architecture & Naming Conventions

> Deep modules, shallow interfaces. Every file should be findable in ≤3 keystrokes.

---

## Principles

1. **Feature modules own their state and behavior.** No feature reaches into another feature's internals.
2. **Interfaces are small.** A service interface should expose ≤5 public methods.
3. **Files are named after the primary noun they define.** Searching the noun finds the file.
4. **Namespaces mirror folders.** If the folder is `RPC.Engine.Combat`, the namespace is `RPC.Engine.Combat`.
5. **Build the snapshot under the lock, broadcast outside it.** (see `GameServer` command handler)

---

## Folder Conventions

```
src/engine/RPC.Engine/
  <Feature>/           # One folder per feature domain
    README.md          # Public API index (required)
    IFeature*.cs       # Contracts / interfaces
    FeatureNoun.cs     # State / data models
    FeatureService.cs  # Behavior / business logic
    FeatureAdapter.cs  # Presentation / serialization (optional)
  Save/
    Migrations/        # Versioned migration rules
  Models/              # Cross-cutting primitive types (Position, Direction, etc.)
```

### Feature README.md Template

Every feature module must contain a `README.md` with:

```markdown
# FeatureName

## Public API
- `IFeatureService` — primary operations
- `FeatureState` — mutable state aggregate

## Dependencies
- `OtherFeature` — why

## Naming
- Files: `FeatureNoun.cs`
- Tests: `FeatureTests.cs`
```

---

## File Naming Patterns

| What it is | Pattern | Example |
|---|---|---|
| Interface | `I<Noun>.cs` | `IDungeonGenerator.cs` |
| State / data | `<Noun>State.cs` or `<Noun>.cs` | `CombatState.cs`, `Dungeon.cs` |
| Service / logic | `<Noun>Service.cs` | `CombatService.cs` |
| Registry / lookup | `<Noun>Registry.cs` | `EncounterTableRegistry.cs` |
| DTO (save/protocol) | `<Noun>Dto.cs` or `Save<Noun>.cs` | `SavePartyMember.cs` |
| Migration | `<Version><Description>Migration.cs` | `V1ToV2IdentityMigration.cs` |
| Tests | `<Noun>Tests.cs` | `CombatEngineTests.cs` |
| Snapshot tests | `<Noun>SnapshotTests.cs` | `CombatSnapshotTests.cs` |

---

## Namespace Rules

1. **Always match folder path.** `src/engine/RPC.Engine/Combat/CombatEngine.cs` → `namespace RPC.Engine.Combat;`
2. **No global usings for feature namespaces.** Explicit `using RPC.Engine.Combat;` at the top of consumer files makes dependencies visible.
3. **Models shared across features live in `RPC.Engine.Models.<Subdomain>`**. Example: `RPC.Engine.Models.Dungeons` for `Tile`, `Dungeon`, `Position`.

---

## Searchability Guidelines

### grep / ripgrep

Find a service by capability:
```bash
rg "class \w+Service" src/engine/RPC.Engine/
```

Find where a feature is consumed:
```bash
rg "using RPC.Engine\.Combat" src/engine/
```

Find tests for a feature:
```bash
rg "class Combat\w+Tests" src/engine/RPC.Tests/
```

### Code Search Heuristics

- **State changed?** Look for `*State.cs` in the feature folder.
- **New behavior added?** Look for `*Service.cs`.
- **Protocol changed?** Look in `RPC.Host/Web/` or `RPC.Engine.Protocol/` (upcoming).
- **Save schema changed?** Look in `RPC.Engine.Save/` and bump `SchemaVersion`.

---

## Module Index (As-Built)

### Engine — `src/engine/RPC.Engine/`

| Module | Folder | Key Types | Responsibility |
|---|---|---|---|
| **Combat** | `Combat/` | `CombatEngine`, `CombatState`, `CombatSessionState`, `Combatant`, `RangeBands`, `EncounterTable`, `EnemyDef` | Turn resolution, initiative, damage, encounter rolls |
| **Dungeon** | `Dungeon/` | `IDungeonGenerator`, `DungeonGenerator`, `DungeonGenerationContracts`, `DungeonBuilder`, `SegmentStitcher`, `DungeonPacer`, `DungeonPathClassifier`, `DungeonLootPlacer`, `DungeonLootTable`, `RoomSegment`, `DungeonTemplate`, `DungeonConnectivityValidator` | Generation contracts + identity, segment assembly, encounter pacing, loot placement |
| **Character** | `Character/` | `CharacterState`, `ClassRegistry`, `LevelingSystem`, `ClassDef`/`BranchDef` | Party members, stats, level/branch progression |
| **Party** | `Party/` | `PartyState`, `EconomyState` | Roster, expedition cache, gold/tithe/inventory |
| **Inventory** | `Inventory/` | `ComponentInventorySystem`, `ComponentStack` | Component stacks, expedition cache transfers |
| **Exploration** | `Exploration/` | `ExplorationState`, `ExplorationService`, `BoundedTileSet` | Dungeon traversal, explored/collected tiles, encounter triggering |
| **Town** | `Town/` | `TownState`, `TownService`, `DowntimeSystem`, `MissionService`, `RumorRepository` | Hub logic, vendors, missions, downtime, rumors |
| **Overworld** | `Overworld/` | `OverworldState`, `OverworldService`, `RouteStatusSystem` | Node graph, route travel, faction presence |
| **Travel** | `Travel/` | `TravelEncounterState`, travel-encounter resolution | Overworld travel encounters |
| **Campaign** | `Campaign/` | `CampaignState`, `CampaignService`, `CampaignConfig`, `EvidenceState`, `FactionInteractionService` | Six-roll narrative scaffolding, evidence, accusations |
| **Reputation** | `Reputation/` | `ReputationState` | Per-faction reputation |
| **Save** | `Save/` | `SaveSystem`, `SaveBuilder`, `SaveRestorer`, `SaveData.*` (per-feature DTOs), `SaveMetadata`, `Migrations/`, `SessionMetaState`, `MetaProgression` | Persistence: build/restore split, versioned migration, save identity + compat |
| **Commands** | `Commands/` | `CommandDispatcher` (`KnownActions`), `ICommand`, `PlayerAction`, `GameCommandHandler` | Action-string → command parsing + execution coordination |
| **Protocol** | `Protocol/` | `ProtocolEnvelope` | Wire envelope/framing contract |
| **Content** | `Content/` | `IContentCatalog`, `FileSystemCatalog`, `RpkCatalog`, `ItemRegistry` | Content pack + asset loading |
| **Core / Models** | `Core/`, `Models/` | `GameRandom`, `Position`, `Direction`, `Tile`, `Dungeon` | Cross-cutting primitives + RNG |
| **Analytics / LLM** | `Analytics/`, `LLM/` | `AnalyticsTracker` | Telemetry; LLM hooks |

`GameState` (`GameState.cs`) is the composition root: it owns feature aggregates (`Exploration`, `Campaign`, `Party`, `Economy`, `Town`, `Overworld`, `CombatSession`, `SessionMeta`) and exposes thin delegating facades — not loose fields.

### Host — `src/engine/RPC.Host/Web/`

| Module | Key Types | Responsibility |
|---|---|---|
| Composition root | `GameServer` | Build collaborators, wire them, run the HttpListener accept loop |
| Content bootstrap | `ContentBootstrap` → `HostContent` | Load all registries from the content catalog |
| HTTP | `HttpRequestRouter` | Routing, static files, debug JSON endpoints |
| WebSocket | `WebSocketConnectionHandler` | WS accept/hello/receive loop + heartbeat |
| Protocol | `Protocol/ProtocolMessageHandler` | Envelope parse/validate, action dispatch under the game-state lock |
| Clients | `ClientRegistry`, `ClientConnection` | Connection tracking + per-client send |
| Broadcast | `StateBroadcaster`, `StatePresenter`, `Presenters/` | State snapshot projection + push |

### Client — `src/client/src/`

| Area | Folder | Responsibility |
|---|---|---|
| Shell | `app/` | `App.svelte` lifecycle, input buffer, store wiring |
| Features | `features/<area>/` | UI + adapters per gameplay area (combat, exploration, town, overworld, party, settings, field-notes, analytics, title); each has an `index.ts` barrel |
| Shared | `shared/{net,stores,types,data}/` | `net/` protocol+client (GameClient, testHarness), `stores/` UI state (gameStore), generated `types/protocol.gen.ts` |
| Renderer | `renderer/` | Three.js dungeon renderer, audio, subtitles |
| Config | `config/` | Keybindings, display + accessibility settings |

See `src/client/src/README.md` for the client taxonomy + barrel conventions.

---

## Search Glossary — "where do I change X?"

| Gameplay term | Code path |
|---|---|
| Dungeon generation / layout | `RPC.Engine/Dungeon/DungeonGenerator.cs` (+ `IDungeonGenerator`, `DungeonGenerationContracts`) |
| Dungeon segment assembly / stitching | `RPC.Engine/Dungeon/DungeonBuilder.cs`, `SegmentStitcher.cs` |
| Encounter pacing / difficulty curve | `RPC.Engine/Dungeon/DungeonPacer.cs` |
| Critical path / room roles | `RPC.Engine/Dungeon/DungeonPathClassifier.cs` |
| Loot placement / loot tables | `RPC.Engine/Dungeon/DungeonLootPlacer.cs`, `DungeonLootTable.cs`, `content/loot/*.json` |
| Determinism / seeds | `DungeonGenerator.StableHash`, `GameRandom`, `Dungeon.Seed` |
| Combat turn resolution | `RPC.Engine/Combat/CombatEngine.cs` |
| Encounter tables / enemy spawns | `RPC.Engine/Combat/EncounterTable.cs`, `content/encounters/*.json`, `content/enemies/*.json` |
| Class abilities / branches | `content/classes/*.json`, `RPC.Engine/Character/LevelingSystem.cs` |
| Overworld routes / travel | `RPC.Engine/Overworld/OverworldState.cs`, `RouteStatusSystem.cs` |
| Travel encounters | `RPC.Engine/Travel/`, `Overworld/OverworldService.cs` |
| Town / vendors / missions / rumors | `RPC.Engine/Town/` |
| Save format / migration | `RPC.Engine/Save/SaveData.*.cs`, `SaveBuilder.cs`, `SaveRestorer.cs`, `Save/Migrations/` |
| Protocol actions (server) | `RPC.Engine/Commands/CommandDispatcher.cs` (`KnownActions`) |
| Protocol action types (client) | `tools/protocol-gen/schema.json` → `src/client/src/shared/types/protocol.gen.ts` |
| State projection to client | `RPC.Host/Web/StatePresenter.cs`, `Presenters/*Presenter.cs` |
| HTTP / WebSocket transport | `RPC.Host/Web/HttpRequestRouter.cs`, `WebSocketConnectionHandler.cs` |
| Content loading / registries | `RPC.Host/Web/ContentBootstrap.cs`, `RPC.Engine/Content/` |

## Retrofit Status (Phase 1.5 — complete)

- `ProtocolEnvelope` → `RPC.Engine.Protocol/` ✅
- `GameServer` transport/protocol/content split → `RPC.Host/Web/{ContentBootstrap,HttpRequestRouter,WebSocketConnectionHandler,Protocol/ProtocolMessageHandler}` ✅ (795 → ~150 lines)
- `SaveSystem` DTOs → per-feature `Save/SaveData.*.cs`; build side → `SaveBuilder`; restore → `SaveRestorer`; metadata → `SaveMetadata` ✅
- `DungeonBuilder` → `IDungeonGenerator` + `DungeonGenerationContracts` (request/identity/result), identity-based save/load ✅
- `GameState` decomposition → feature aggregates (`CombatSessionState`, `EconomyState`, `SessionMeta`, plus existing Exploration/Campaign/Party/Town/Overworld) ✅
- Protocol schema pipeline + drift check (`ProtocolActionSyncTests`) → `tools/protocol-gen/` ✅
- Client feature-module barrels + layout doc ✅

---

## Determinism Rules

1. **Seeded RNG only.** `GameRandom` for all game-affecting randomness. No `System.Random` in features.
2. **Stable hashes for content-derived seeds.** `StableHash(string)` (FNV-1a) instead of `string.GetHashCode()`.
3. **Dungeon identity = seed.** Every `Dungeon` stores its `Seed`; save/load round-trips regenerate identically.

---

## Protocol Schema Pipeline

Source of truth: `tools/protocol-gen/schema.json` (JSON Schema Draft-07).

### Generating Types

```bash
cd tools/protocol-gen
npm install
npm run generate
```

This produces `src/client/src/types/protocol.gen.ts` from the schema.

### When to Regenerate

- New action type added to protocol
- Envelope shape changed
- Payload fields added/removed

### C# Sync

C# types in `RPC.Engine.Protocol/` are maintained manually. When the schema changes, update the corresponding C# classes and add a test in `ProtocolSchemaTests` to enforce field parity.

---

*Last updated: 2026-06-15*
