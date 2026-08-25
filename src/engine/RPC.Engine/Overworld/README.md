# Overworld Module

## Scope
Overworld graph generation, node-to-node travel, turn counting, and travel encounter management.

## Public API
- `OverworldService` — travel, turn increment, and travel encounter resolution
- `OverworldState` — mutable graph state (nodes, routes, current position, turns)
- `OverworldNode` / `OverworldRoute` — graph primitives
- `RouteStatus` / `NodeType` — enum classifications

## Dependencies
- `Campaign` (read-only, for config-based generation and complication application)
- `Party` (read-only, for travel encounter difficulty scaling)

## Boundary
Do NOT put dungeon exploration, combat mechanics, or town vendor logic here.
