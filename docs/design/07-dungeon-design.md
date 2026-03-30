# Dungeon Design

## Structure

Each campaign contains 5-8 dungeons, drawn from a library of authored dungeon templates. Each template is a set of rooms, encounters, and setpieces that get skinned and connected based on the campaign's six rolls.

## Navigation

- First-person low-poly 3D, grid-based movement (step/turn)
- Cardinal directions: forward, back, strafe left/right, turn left/right
- Automap fills in as you explore
- Secret doors, hidden passages, environmental puzzles
- The Cartography faction's maps can reveal sections in advance
- Marcher (Pathfinder) class detects hidden features at greater range

## Dungeon Templates

### The Broken Engine
An imperial facility, partially functioning. Hazardous machinery, areas of intense necromantic energy, something still running that shouldn't be.

- **Environment:** Industrial architecture, pipes, conduits, chambers that hum with energy
- **Hazards:** Energy discharges, unstable floors, rooms that shift when machinery cycles
- **Key encounters:** Engine constructs (guardians and malfunctions), Fieldwright-advantage puzzles
- **Setpieces:** A control room where players can interact with (or sabotage) the Engine
- **Loot focus:** Engine-tech components, Fieldwright upgrades, faction documents about maintenance history

### The Bloom Site
A Vandermeer-style zone of wrongness. Organic geometry, creatures that used to be something else, the environment itself is hostile.

- **Environment:** Organic growth over architecture, impossible geometry, color shifts, air that feels thick
- **Hazards:** Bloom exposure (cumulative stat debuffs), mutating terrain, areas where necromancy backfires
- **Key encounters:** Bloom creatures in their home territory, stronger and stranger than elsewhere
- **Setpieces:** The bloom's "heart" — a source node that can be destroyed, contained, or harvested (faction implications for each choice)
- **Loot focus:** Bloom samples (valuable to Convocation and Cartography), mutated gear with high risk/reward stats

### The Boneyard
A tithe processing facility. Industrial necromancy at scale. Political secrets in the records.

- **Environment:** Vast bone-sorting halls, animation chambers, filing rooms, tithe archives
- **Hazards:** Automated tithe-processing that doesn't distinguish living from dead, residual animations
- **Key encounters:** Rogue tithe-constructs, Compact guardians, bureaucratic undead still filing paperwork
- **Setpieces:** The archive — searchable records that reveal faction secrets, tithe fraud, or family histories
- **Loot focus:** Necromantic crafting materials, Bonewarden upgrades, political leverage documents

### The Sealed Vault
Old empire storage. Traps that still work. Warnings in dead languages. The thing inside was locked away on purpose.

- **Environment:** Imperial architecture at its most imposing, layers of wards, Inkblood-readable inscriptions
- **Hazards:** Active traps (mechanical and necromantic), wards that trigger on specific class abilities
- **Key encounters:** Guardian constructs designed to stop exactly what the players are trying to do
- **Setpieces:** The vault itself — what's inside depends on the campaign's Scheme. Could be a weapon, knowledge, an entity, or something worse
- **Loot focus:** Imperial-era equipment (powerful but conspicuous), lore documents, the vault's contents

### The Contested Ruin
A dungeon another faction is already in. Rival parties, evidence of recent activity, potential for diplomacy or ambush.

- **Environment:** Standard ruin but with signs of recent habitation — camps, barriers, faction markings
- **Hazards:** Traps set by the rival faction, patrol routes, alarm systems
- **Key encounters:** Faction soldiers with their own objectives, potential negotiation encounters
- **Setpieces:** A confrontation with the rival faction's leader — fight, negotiate, or cooperate
- **Loot focus:** Faction intel, stolen goods, equipment from defeated rivals

### The Underway
Imperial transit tunnels connecting locations. Used as a recurring connector dungeon between major sites. Changes each traversal to prevent memorization.

- **Environment:** Uniform imperial tunnel architecture that slowly deteriorates the deeper you go
- **Hazards:** Collapses, flooded sections, bloom encroachment, things living in the dark
- **Key encounters:** Random mix — whatever is using the tunnels right now (faction patrols, bloom creatures, refugees, smugglers)
- **Setpieces:** Junction chambers where players choose routes — shorter but more dangerous vs longer but safer
- **Loot focus:** Traveler's supplies, cached goods, occasional imperial-era finds in collapsed sections

**Procedural variation between traversals:**
- Room segment layout is re-assembled each traversal (same segment pool, different arrangement). The critical path changes; previously memorized routes don't apply.
- Encounter tables re-roll based on current campaign state. Early-game Underway has smugglers and refugees. Late-game has bloom creatures and Unaccounted.
- One persistent element: the junction chambers remain in fixed positions, preserving the route choice mechanic. Everything between junctions shuffles.
- Environmental state degrades over the campaign. First traversal: intact tunnels with minor flooding. Third traversal: bloom encroachment, collapsed sections, makeshift faction barricades.

### The Settlement Gone Wrong
A town or outpost overtaken by bloom, faction, or disaster. Urban exploration, survivors, moral choices.

- **Environment:** Recognizable town structures — homes, shops, civic buildings — in various states of ruin or transformation
- **Hazards:** Structural collapse, bloom pockets, hostile survivors, faction occupation
- **Key encounters:** Survivors who need help (at a cost), occupying forces, bloom creatures in domestic settings (unsettling)
- **Setpieces:** A choice about the settlement's fate — evacuate, reclaim, or sacrifice it
- **Loot focus:** Civilian supplies, personal effects with lore value, faction evidence

### The Ossuary
A family bone-tithe repository. Personal and political. Ghosts that are more memory than threat.

- **Environment:** Intimate architecture — family vaults, memorial halls, private chambers
- **Hazards:** Memory-ghosts that project emotional states (confusion, grief, rage), tithe-magic traps keyed to bloodlines
- **Key encounters:** Animated ancestors who think they're still alive, Compact guardians, family politics made literal
- **Setpieces:** A family secret — every Ossuary holds one, and it's always relevant to the current campaign
- **Loot focus:** Family heirlooms (unique equipment), necromantic recipes, political blackmail material

## Procedural Assembly

### How Templates Become Dungeons
1. Campaign generation selects 5-8 templates based on the six rolls
2. LLM assigns faction presence, evidence placement, and NPC casting
3. Room layouts are procedurally assembled from authored room segments (modular tiles with hand-designed geometry)
4. Loot tables and encounter difficulty scale to party level and campaign progress

### Room Segments
- Each template has 20-30 authored room segments: corridors, chambers, dead ends, puzzle rooms, encounter arenas, treasure rooms
- Segments have connection points (north/south/east/west/up/down) and size categories
- Assembly algorithm ensures connectivity, pacing (combat rooms spaced by exploration), and a critical path to the setpiece
- Secret rooms are placed off the critical path with authored connection triggers (hidden switches, breakable walls, Inkblood-readable clues)

### Room Segment Data Format

Each room segment is a JSON file consumed by the dungeon assembler and (in Phase 3) addressed by the LLM during campaign generation. JSON is used for content data (not KDL) because the LLM campaign generation pipeline outputs JSON, the validation layer uses JSON Schema, and the content library needs efficient batch loading across 160-240+ entries.

```json
{
  "id": "broken-engine-control-room",
  "template": "broken-engine",
  "size": "large",
  "category": "setpiece",
  "connections": {
    "north": "open",
    "south": "open",
    "east": "hidden"
  },
  "geometry": {
    "grid": [6, 8],
    "elevation": 0,
    "features": ["console", "pipes", "grating"]
  },
  "encounters": {
    "primary": "engine-construct-guardian",
    "optional": {
      "id": "malfunctioning-turret",
      "condition": "!party.has_class('fieldwright')"
    }
  },
  "loot": {
    "fixed": ["engine-tech-manual"],
    "table": "broken-engine-rare"
  },
  "interactables": {
    "console": {
      "classInteraction": { "class": "fieldwright", "action": "repair-or-sabotage" },
      "defaultInteraction": "examine"
    }
  },
  "tags": ["engine", "industrial", "setpiece", "faction-evidence-slot"]
}
```

The `tags` field is the LLM's primary addressing mechanism. During campaign generation, the LLM selects segments by tag to place faction evidence, NPC encounters, and quest-relevant content. The `faction-evidence-slot` tag marks segments where documentary evidence can be placed without breaking the segment's authored design.
