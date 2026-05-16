# Engine Service Boundaries

## Overview

`GameState` is the root state container / snapshot for the entire game. It holds all mutable state (party, town, overworld, combat, reputation, etc.) but contains minimal orchestration behavior. Domain commands are delegated to dedicated service classes in `RPC.Engine.Services`.

## Rule of Thumb

> If a method mutates state across multiple sub-systems or implements a full game-mode command, it belongs in a service. If it is a simple getter, setter, or state initialization, it can stay on `GameState`.

## Service Map

| Service | Domain | Key Commands |
|---------|--------|-------------|
| `CombatService` | Encounters & combat | `TriggerEncounter`, `ResolveParley`, `SubmitCombatAction`, `FleeCombat` |
| `ExplorationService` | Dungeon exploration | `EnterDungeon`, `ExploreAroundPlayer`, `TryMoveForward/Back/StrafeLeft/StrafeRight`, `TurnLeft/Right` |
| `TownService` | Town hub & roster | `RestAtInn`, `PerformDowntimeAction`, `ReturnToTown`, `RecruitFromTavern`, `ResurrectCharacter`, `PurchaseVendorItem` |
| `OverworldService` | Travel & turns | `GenerateOverworld`, `Travel`, `ResolveTravelEncounter`, `IncrementTurns` |
| `CampaignService` | Story & progression | `ApplyReputationDelta`, `AddEvidence`, `AccuseFaction`, `UnlockFinalDungeon`, `ChooseBranch`, `CheckWildCardTrigger`, `Accept/Refuse/IgnoreWildCardAlliance` |
| `MissionService` | Mission lifecycle | `AcceptMission`, `CompleteMission`, `FailMission`, `AbandonMission` |

## Service Pattern

Services are instantiated by `GameState` in its constructor and held as private fields. Each service receives the registries and RNG it needs via constructor injection.

```csharp
public class CombatService
{
    private readonly EncounterTableRegistry? _encounterTables;
    private readonly ClassRegistry? _classRegistry;
    private readonly GameRandom _encounterRng;

    public CombatService(EncounterTableRegistry? encounterTables, ClassRegistry? classRegistry, GameRandom encounterRng)
    {
        _encounterTables = encounterTables;
        _classRegistry = classRegistry;
        _encounterRng = encounterRng;
    }

    public void TriggerEncounter(GameState state, EncounterDef? encounter = null)
    {
        // ... operate on state.* properties ...
    }
}
```

`GameState` exposes the same public API it always had, but each command method is now a thin delegator:

```csharp
public void TriggerEncounter(EncounterDef? encounter = null)
{
    _combatService.TriggerEncounter(this, encounter);
}
```

## Cross-Domain Calls

When a service needs to trigger a command in another domain (e.g., `ExplorationService.ExecuteMove` calls `state.TriggerEncounter`), it goes through `GameState`'s public delegator. This keeps the dependency graph flat and avoids service-to-service references.

## State That Stays on GameState

The following remain on `GameState` because they are either simple state containers or have tight coupling to save/load:

- All properties (`Party`, `Town`, `Overworld`, `Combat`, `Reputation`, `Evidence`, `Journal`, `WorldState`, etc.)
- `SaveGame` / `LoadGame` (pass-through to `SaveSystem`)
- `Reset` (global reset orchestration)
- `RestoreActionLog` / `RestoreDowntimeState` (save restoration helpers)
- `EmitActionLog` (internal, used by all services)
- `ClearTaggedEncounterTile` (internal, used by combat)
- `IncrementTurns` (public delegator to `OverworldService`)
- Simple setters used by `SaveSystem`: `SetAccusedFaction`, `SetMastermindAdvantage`, `SetFinalDungeonUnlocked`

## Adding a New Service

1. Create `src/engine/RPC.Engine/Services/MyDomainService.cs`
2. Add constructor dependencies (registries, RNG)
3. Move command methods from `GameState` into the service
4. Add a private field to `GameState` and instantiate it in the constructor
5. Replace the original `GameState` method body with a delegator
6. Make any private fields on `GameState` accessible to services by changing them to `internal`
7. Ensure all 608 tests still pass

## Host Boundary

The transport layer (`RPC.Host.Web.GameServer`) is intentionally thin. It owns WebSocket/HTTP lifecycle, protocol envelope parsing, and client broadcasting. All game logic lives in `RPC.Engine`.

| Component | Namespace | Responsibility |
|-----------|-----------|---------------|
| `CommandDispatcher` | `RPC.Engine.Commands` | Maps raw `PlayerAction` string bag → typed `ICommand` record |
| `GameCommandHandler` | `RPC.Engine.Services` | Receives `ICommand`, executes domain mutations on `GameState`, returns `CommandResult` |
| `StatePresenter` | `RPC.Host.Web` | Pure serializer: `GameState` → client payload. No side effects. |

**Flow:**
```
Client WS message → ProtocolEnvelope parse → CommandDispatcher.Parse → GameCommandHandler.Execute → StatePresenter.CreateStateMessage → BroadcastState
```

**Key rules:**
- `GameServer` never mutates `GameState` directly (except `LoadGame` during startup).
- `StatePresenter` is pure: calling it twice with the same `GameState` yields identical output.
- `CommandResult` carries flags (`StateChanged`, `ClearCombatResult`) so `GameServer` knows what to do after execution without inspecting state.

## History

- **2026-05-13**: Extracted six services from 1,831-line `GameState`. Reduced `GameState` to ~460 lines of state + delegation.
- **2026-05-13**: Extracted `StatePresenter` and `GameCommandHandler` from `GameServer`. Reduced `GameServer` from 1,344 to ~737 lines.
