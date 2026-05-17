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

| Module | Folder | Key Types | Responsibility |
|---|---|---|---|
| **Combat** | `Combat/` | `CombatEngine`, `CombatState`, `Combatant`, `RangeBands` | Turn resolution, initiative, damage |
| **Dungeon** | `Dungeon/` | `DungeonBuilder`, `DungeonTemplate`, `RoomSegment` | Procedural assembly |
| **Character** | `Character/` | `CharacterState`, `ClassRegistry`, `LevelingSystem` | Party members, stats, progression |
| **Town** | `Town/` | `TownState`, `DowntimeSystem`, `RumorRepository` | Hub logic, vendors, missions |
| **Overworld** | `Overworld/` | `OverworldState`, `RouteStatusSystem` | Node travel, faction presence |
| **Campaign** | `Campaign/` | `CampaignConfig`, `EvidenceState`, `HeatState` | Narrative scaffolding |
| **Save** | `Save/` | `SaveSystem`, `SaveData`, `SaveMigrationPipeline` | Persistence, versioning |
| **Commands** | `Commands/` | `CommandDispatcher`, `ICommand`, `PlayerAction` | Input parsing |
| **Content** | `Content/` | `IContentCatalog`, `ItemRegistry`, `FileSystemCatalog` | Asset loading |
| **Protocol** | `Host/Web/` (temp) | `ProtocolEnvelope`, `GameServer` | Transport, framing |

### Upcoming Moves (Phase 1.5 Retrofit)

- `ProtocolEnvelope` → `RPC.Engine.Protocol/` ✅
- `GameServer` transport split → `RPC.Host/Web/{Protocol,ClientRegistry,Broadcaster}` ✅
- `SaveSystem` DTOs → `RPC.Engine.Save/SaveData.cs` ✅
- `SaveSystem` restore → `RPC.Engine.Save/SaveRestorer.cs` ✅
- `DungeonBuilder` contract → `IDungeonGenerator` ✅
- `GameState` decomposition → feature state aggregates
- Protocol schema pipeline → `tools/protocol-gen/` ✅

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

*Last updated: 2026-05-16*
