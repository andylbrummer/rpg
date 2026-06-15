# Save Module

## Scope
Save/load pipeline: DTOs, serialization, migration, and state restoration.

## Public API
- `SaveSystem` — thin facade for Save() and Load(); delegates build to `SaveBuilder`, restore to `SaveRestorer`
- `SaveData` — versioned root DTO; feature DTOs live in `SaveData.<Feature>.cs` (Party, Town, Overworld, Campaign, Journal, Heat, ActionLog)
- `SaveBuilder` — static `Build<Feature>(GameState)` helpers that serialize `GameState` into `SaveData`
- `SaveRestorer` — static `Restore<Feature>(GameState, SaveData)` helpers that hydrate `GameState` from `SaveData`
- `SaveMetadata` / `SaveCompatibility` — minimal version+content-hash contract and load-time compatibility warnings
- `SaveMigrationPipeline` — schema migration chain

## Layout
DTOs are grouped by feature so save behaviour is discoverable by feature name and path:
`SaveData.cs` (root) plus `SaveData.Party.cs`, `SaveData.Town.cs`, `SaveData.Overworld.cs`,
`SaveData.Campaign.cs`, `SaveData.Journal.cs`, `SaveData.Heat.cs`, `SaveData.ActionLog.cs`.
The build (`SaveBuilder`) and restore (`SaveRestorer`) halves mirror each other per feature.

## Dependencies
- All feature modules (reads/writes every aggregate to reconstruct state)

## Boundary
This is a cross-cutting persistence layer. No gameplay logic should live here; only serialization and state hydration.
