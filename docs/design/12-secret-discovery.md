# Secret Discovery

## Overview

Secret doors, hidden passages, and concealed loot are core to dungeon exploration. The discovery system must be data-driven, class-aware, and consistent across all dungeon templates.

## Secret Types

| Type | Visual State | Discovery Methods |
|---|---|---|
| **Hidden door** | Looks like a solid wall. Becomes a door when found. | Proximity detection, class ability, explicit search |
| **Breakable wall** | Looks like a damaged/wall section. | Explicit search, area damage, class ability |
| **Concealed compartment** | No visual indicator. | Explicit search, class ability |
| **Illusory floor** | Looks like floor; is a pit/trap. | Marcher (Pathfinder) detection, explicit search |

## Discovery Mechanics

### 1. Passive Proximity Detection (Automatic)

When the party enters a tile within **range 1** of a secret, a detection roll occurs automatically.

```
detection_chance = base_secret_difficulty + party_bonus

party_bonus = highest(perception_stat) + class_bonus + gear_bonus
class_bonus:
  Inkblood (Cartographer): +15%
  Hollow (Filch): +10%
  Marcher (Pathfinder): +10% (also detects at range 2)
```

- Base difficulty varies by secret: `easy (20%)`, `moderate (10%)`, `hard (5%)`, `very_hard (0% — never passive)`.
- Passive detection only reveals the secret's existence, not its exact mechanism (e.g., "something is off about this wall").

### 2. Explicit Search (Player-Initiated)

Player uses the **Search** action (default bound to `Space` when facing a wall, or via UI button).

```
search_chance = base_difficulty + party_bonus + search_attempt_bonus
search_attempt_bonus: +5% per previous failed search on this secret (max +25%)
```

- Searching costs **1 campaign turn** (overworld time doesn't advance, but dungeon exploration time is modeled).
- Searching reveals the exact secret type and how to open it.
- A party can search the same tile multiple times.

### 3. Class Abilities (Guaranteed)

Certain abilities guarantee discovery without a roll:

| Ability | Class/Branch | Effect |
|---|---|---|
| **Cartographer's Eye** | Inkblood (Cartographer) | Reveals all secrets within 2 tiles. Passive, always on. |
| **Tremorsense** | Bonewarden (Animator) | Reveals breakable walls and hidden pits within 1 tile. |
| **Trapfinding** | Hollow (Filch) | Reveals concealed compartments and traps. +25% to all search rolls. |
| **Engine Whispers** | Fieldwright (Artificer) | Reveals hidden doors and Engine-tech secrets in Broken Engine dungeons. |

### 4. Triggered Discovery (Environmental)

Some secrets are found through play, not rolls:
- A document mentions "the east wall of the pump room is false."
- An enemy flees through a hidden door, revealing it.
- An area-of-effect attack accidentally damages a breakable wall.

## Data Model

Secrets are encoded in the room segment JSON:

```json
{
  "secrets": [
    {
      "id": "secret-pump-room-east",
      "type": "hidden_door",
      "position": { "x": 4, "y": 2, "facing": "east" },
      "difficulty": "moderate",
      "revealed": false,
      "discoveryMethods": ["proximity", "search", "document-hint-pump-room-log"],
      "classBypass": ["fieldwright-artificer"],
      "reward": {
        "lootTable": "broken-engine-rare",
        "evidenceId": "doc-maintenance-logs-7"
      }
    }
  ]
}
```

- `revealed` is a per-campaign flag, stored in save state.
- When `revealed: true`, the dungeon grid updates the wall type from `hidden` to `door` (or `open` for breakable walls), and the renderer rebuilds the affected geometry.

## Automap Integration

- Unrevealed secrets do not appear on the automap.
- Once revealed, hidden doors appear as doors. Breakable walls appear with a cracked-wall icon.
- Inkblood (Cartographer) automap reveals secret locations as "?" markers even before the secret is fully discovered.

## Phase Implementation

| Phase | Scope |
|---|---|
| 1 | Hidden doors only. Passive detection + explicit search. Base difficulty system. |
| 1.5 | Add breakable walls. Inkblood Cartographer passive detection. |
| 2 | Add concealed compartments, illusory floors. All class bypass abilities. Document-triggered discovery. |
| 3 | Secret synergies (abilities that reveal secrets as side effects). Environmental storytelling tied to secrets (bloodstains hinting at hidden doors). |
