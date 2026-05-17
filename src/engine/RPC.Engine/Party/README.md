# Party Module

## Scope
Party roster management: living members, dead character storage, row swapping, and expedition cache.

## Public API
- `PartyState` — aggregate with 6 member slots, dead characters, and expedition cache
- `CharacterState` — full character data (stats, equipment, abilities, modifiers)
- `BaseStats` / `Equipment` / `ComponentStack` — character primitives

## Dependencies
- `Character` (namespace overlap; types are colocated here)

## Boundary
Do NOT put combat AI, town recruitment logic, or exploration movement here. This module only models the party roster data.
