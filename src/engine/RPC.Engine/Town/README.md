# Town Module

## Scope
Town hub logic: vendors, contacts, tavern recruitment, missions, downtime actions, rumors, and inn resting.

## Public API
- `TownService` — primary orchestrator for town actions
- `TownState` — aggregate holding vendors, contacts, roster, missions, rumors, quest log
- `MissionService` — mission acceptance, completion, failure, abandonment
- `TavernRecruit` / `FactionVendor` / `FactionContact` / `ActiveMission` / `TownRumor` — town data types

## Dependencies
- `Party` (read/write, for recruitment and roster changes)
- `Campaign` (read-only, for reputation-based vendor thresholds)
- `Character` (for resurrection and stat modification)

## Boundary
Do NOT put dungeon exploration, combat resolution, or overworld travel here.
