# Exploration Module

## Scope
Player movement, dungeon traversal, fog-of-war tracking, and encounter triggering while inside a dungeon.

## Public API
- `ExplorationService` — handles movement, turning, dungeon entry, and tile exploration
- `ExplorationState` — aggregate holding player, current dungeon, explored tiles, and encounter tracking
- `Player` — position and facing direction
- `BoundedTileSet` — LRU-capped set of explored tile coordinates

## Dependencies
- `Dungeon` (read-only, for collision and tile queries)
- `Combat` (writes, to trigger encounters)
- `Models.Dungeons` (Position, Direction)

## Boundary
Do NOT put combat resolution, town UI logic, or overworld node travel here.
