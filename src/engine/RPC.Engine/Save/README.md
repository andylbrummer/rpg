# Save Module

## Scope
Save/load pipeline: DTOs, serialization, migration, and state restoration.

## Public API
- `SaveSystem` — facade for Save() and Load()
- `SaveData` — versioned DTO representing full game state
- `SaveRestorer` — static methods that hydrate `GameState` from `SaveData`
- `SaveMigrationPipeline` — schema migration chain

## Dependencies
- All feature modules (reads/writes every aggregate to reconstruct state)

## Boundary
This is a cross-cutting persistence layer. No gameplay logic should live here; only serialization and state hydration.
