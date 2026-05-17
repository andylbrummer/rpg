# Commands Module

## Scope
Command parsing, dispatch, and the top-level game command handler that bridges player actions to feature services.

## Public API
- `GameCommandHandler` — translates `PlayerAction` messages into `GameState` mutations
- `ICommand` — marker interface for command types
- `CommandDispatcher` — routes command types to handlers
- `PlayerAction` — union of all possible client actions
- `CommandResult` — `(StateChanged, ClearCombatResult)` tuple

## Dependencies
- All feature modules (Combat, Exploration, Town, Overworld, Campaign)
- `Dungeon` (for `IDungeonGenerator` contract)

## Boundary
This is the cross-cutting orchestration layer. Keep feature-specific business rules out; delegate to feature services.
