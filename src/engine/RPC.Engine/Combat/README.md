# Combat Module

## Scope
Turn-based combat system: encounter initiation, action submission, enemy AI, parley resolution, and combat result generation.

## Public API
- `CombatService` — primary orchestrator for combat flow
- `CombatState` — mutable combat snapshot (initiative, actors, log)
- `CombatAction` — player-submitted action payload
- `CombatResult` — outcome of a completed combat
- `CombatEngine` — core combat resolution (damage, status effects, death)

## Dependencies
- `Character` (read/write, for party and enemy state)
- `Exploration` (read-only, for dungeon context)
- `Campaign` (read-only, for reputation/heat checks)

## Boundary
Do NOT put exploration movement, town logic, or overworld travel here.
