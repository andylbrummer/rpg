# Combat System

## Structure

Turn-based, initiative-ordered. Party (up to 6) vs enemy groups. Inspired by Wizardry: Crusaders of the Dark Savant.

### Combat Space

Combat takes place in an abstract space separate from the dungeon grid. When combat initiates, the game transitions from grid navigation to the combat view. Positions in the combat space do not map back to dungeon tiles.

The combat space has **range bands**, not a grid:

| Band | Distance | Who's Here |
|---|---|---|
| **Melee** | Adjacent | Party front row, melee enemy groups |
| **Short** | Nearby | Ranged enemy groups, some casters |
| **Long** | Distant | Artillery enemies, fleeing groups |

Range bands determine targeting: melee attacks only reach melee band, ranged attacks reach short and long, some abilities span all bands. Enemy groups can shift between bands (advance to melee, retreat to long) as a combat action.

### Formation
- Party arranged in front row (3) and back row (3)
- Front row occupies the melee band and can melee attack enemies at melee range
- Back row uses ranged/magic, protected from melee attacks unless front row is eliminated or enemies cross rows
- Enemies appear in 1-4 groups, each placed at a range band based on encounter type

### Initiative
- Each round, all combatants roll initiative: base speed + class modifier + gear + random factor
- Displayed as a visible turn order bar so players can plan around it
- Some abilities manipulate initiative — delaying enemies, pushing allies earlier in the order
- Surprise rounds: if one side has stealth/ambush advantage, they get a free round

### Actions
- One action per turn + one quick action (swap item, drink potion, shout)
- Some branches grant bonus actions or reactions (counterattack, interrupt, guard)
- Actions: Attack, Defend, Cast, Use Item, Flee, Wait (delay turn in initiative order)
- Targeting: individual enemies or enemy groups depending on ability

## The Combination System

Certain ability pairs trigger bonus effects when used in coordination. These synergies are not documented in-game — players discover them by experimenting.

### Synergy Trigger Timing

A synergy triggers when both abilities in the pair are used within the **same combat round**, regardless of initiative order. The second ability to resolve checks whether its pair was already used this round; if so, the synergy activates on the second ability's resolution.

This means synergies are plannable: if you know the pair, you can queue both abilities at the start of the round and the synergy will fire when the second one resolves. Initiative manipulation (pushing an ally earlier) doesn't affect whether a synergy triggers — only whether the bonus effect applies before or after enemy actions.

**Synergies do not chain across rounds.** Using ability A in round 3 and ability B in round 4 does not trigger the synergy.

### Discovery Flow

Three-tier feedback ensures synergies aren't missed:

1. **Immediate** — visual flash and distinct sound cue when the synergy resolves. Can be missed if the player isn't watching.
2. **Field Notes entry** — auto-recorded with a cryptic hint describing the effect and the ability pair. Always captured, never lost.
3. **Replayable** — from the Field Notes journal, the player can re-watch the synergy animation and review the mechanical effect. Lore hints found in dungeons or from NPCs also link to relevant Field Notes entries, letting players connect hints to synergies they've already seen (or haven't yet discovered).

NPCs and lore documents occasionally hint at undiscovered combinations. Some synergies only work against specific faction threats.

### Example Synergies

| Ability A | Ability B | Effect | Hint Text |
|---|---|---|---|
| Tithebinder's Bone Link | Scorcher's Pyre | Linked enemies all take fire damage | "Bone carries fire well" |
| Fader's Backstep | Stalker's Called Shot | Guaranteed critical hit | "Strike from the shadow's edge" |
| Animator's Bone Servant | Fieldwright's Overcharge | Skeleton detonates as area bomb | "Even the dead can be overloaded" |
| Agitator's War Cry | Hollow's Cheap Shot | Enemy group's morale breaks, back rank flees | "Panic opens every guard" |
| Surgeon's Purify | Heretic's Bloom Touch | Controlled mutation — target gains temp buff and bloom resistance | "Clean corruption is still corruption" |
| Warden's Shield Wall | Cauterist's Flashfire | Fire damage in arc, warden takes none | "Stand behind the fire line" |
| Pathfinder's Mark Prey | Oathless's Frenzy | Frenzy damage doubled against marked target | "The hunt narrows" |
| Archivist's Expose Weakness | Sapper's Precision Charge | Charge ignores armor, hits structural weakness | "Knowledge is a kind of violence" |

### Synergy Design Targets
- 40-50 total synergies across all class/branch combinations
- 10-15 relevant per run depending on party composition and threat type
- Some synergies require specific branch combinations, rewarding diverse parties
- A few "secret" synergies require items or environmental conditions

## Favoritism With Friction

Players can run their favorite party shape every run. A melee-heavy Stillblade/Hollow/Marcher core works. But each campaign's threat has 2-3 encounters specifically designed to punish that composition — forcing bench-swaps or creative synergy use to compensate.

You can brute-force it, but you'll miss the elegant solution.

## Enemy Design

### Bloom Creatures
- Unpredictable, might mutate mid-fight
- Environmental hazards — the arena itself is hostile
- Resist categorization, each one slightly different
- Weak to fire and purification, resistant to necromancy

### Faction Soldiers
- Tactical, use their own synergies
- Retreat when outmatched — you can't always get a clean kill
- Equipment and abilities match their faction identity
- Can be negotiated with if you have the right Ashmouth

### Engine Constructs
- Mechanical, immune to certain damage types
- Puzzle-like weak points — brute force works but costs resources
- Fieldwrights can interact with them in unique ways
- Found near functioning Engines, sometimes as guardians, sometimes as malfunctions

### The Unaccounted
The dead that nobody raised. The bloom's own "faction." They appear in late-game dungeons (Act 3) and as escalation encounters when bloom timers expire.

No faction controls them. No faction understands them. This is the thing everyone is afraid of.

**Design principle:** The Unaccounted teach through violation. By Act 3, the player has internalized the combat rules. The Unaccounted break those rules deliberately:

| Rule Players Learned | How The Unaccounted Break It |
|---|---|
| Initiative determines turn order | Unaccounted **interrupt** — they act between other turns, ignoring initiative |
| Enemies stay in their range band until they act | Unaccounted **phase** — they appear at any range band without moving |
| Dead enemies stay dead | Unaccounted **reassemble** — fallen Unaccounted recombine into new forms after 2 rounds |
| Front row protects back row | Unaccounted **reach through** — some attacks target back row directly |
| Status effects have defined durations | Unaccounted inflict **dread** — a unique status that doesn't tick down, only clears when the source is killed |

The Unaccounted are not unfair — each rule-break has a counter:
- Interrupt: Warden's Shield Wall still blocks regardless of timing
- Phase: Marcher (Stalker) can target any range band
- Reassemble: Cauterist fire permanently prevents reassembly
- Reach through: Bonewarden (Animator) summons absorb back-row targeting
- Dread: Ashmouth (Agitator) war cry dispels dread

The counter to each violation comes from a different class, rewarding diverse parties.

## Resource Management

- No mana bars. Spell costs are physical: bone fragments, blood (HP), memory (temp stat loss), Engine charge, carried components
- Healing is limited — Cauterists have finite supplies per expedition, resting in dungeons is risky
- Combat attrition matters. The question isn't "can we win this fight" but "can we win it and still handle the next three"
- Fleeing is always an option but costs: enemies may reposition, block paths, or alert others

## Balance Targets

Approximate ratios for Phase 1 tuning. These are starting points, not final numbers.

### Health & Damage

| Stat | Level 1 | Level 5 | Level 10 |
|---|---|---|---|
| Front-liner HP (Stillblade, Warden) | 30 | 55 | 85 |
| Back-liner HP (Inkblood, Cauterist) | 18 | 35 | 55 |
| Basic melee attack | 5-8 | 10-15 | 18-25 |
| Basic ranged attack | 4-6 | 8-12 | 14-20 |
| Strong ability | 10-15 | 20-30 | 35-50 |

### Attrition Budget

A dungeon expedition should contain 4-6 combat encounters before the setpiece. The party should be able to handle them if they manage resources well, arriving at the setpiece at roughly 40-60% of their starting resources.

- Cauterist healing per expedition: enough to restore ~150% of one front-liner's max HP total (spread across the party)
- Bone fragment supply: enough for ~8-10 Bonewarden casts per expedition (carry limit, not unlimited)
- A well-played expedition uses 70-80% of carried resources
- A poorly-played expedition runs dry by encounter 4, forcing retreat or basic-attack-only boss attempts

### Combat Duration

- Trash encounter: 3-4 rounds
- Standard encounter: 5-7 rounds
- Setpiece/boss: 8-12 rounds
- Player turn decision time target: 10-15 seconds (UI should present clear options, not require menu diving)

## Appendix — Wandering Encounter Probability

The per-step wandering encounter chance is a linear escalation model:

```
chance = baseChance + perStepBonus * stepsSinceEncounter
```

Current balance values (Phase 1.5):

| Constant | Value | Description |
|---|---|---|
| `baseChance` | 0.05 | Initial chance on the first step after an encounter |
| `perStepBonus` | 0.08 | Added for each step without an encounter |
| `maxChance` | 1.0 | Implicit cap (roll is 0..99, so chance > 1.0 guarantees trigger) |

**Example pacing:**
- Step 1: 13%
- Step 5: 45%
- Step 12: 101% (guaranteed)

Tagged tiles (setpiece / boss) bypass this formula entirely. They trigger on entry with 100% probability and are cleared on victory. Fleeing leaves the tag intact so the encounter fires again on re-entry.

**Tuning guidance:** Raising `perStepBonus` reduces the maximum "dry" stretch. Lowering it increases tension but risks long boring walks. Keep `baseChance` low so the first few steps after combat feel safe.
