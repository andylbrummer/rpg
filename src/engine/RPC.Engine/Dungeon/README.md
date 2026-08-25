# Dungeon Module

## Scope
Procedural dungeon generation, room segment loading, and dungeon template definitions.

## Public API
- `DungeonBuilder` — procedural dungeon generator (accepts seed for determinism)
- `IDungeonGenerator` — contract for dungeon creation
- `DungeonTemplate` — content definition for a dungeon type
- `SegmentLoader` — loads room segment JSON into segment data
- `RoomSegment` — individual room geometry and encounter tags

## Dependencies
- `Models.Dungeons` (Dungeon, Tile, Position types)
- `Content` (for template lookup)

## Boundary
Do NOT put exploration logic (movement, fog-of-war) or combat logic here. This module only builds the static dungeon geometry.
