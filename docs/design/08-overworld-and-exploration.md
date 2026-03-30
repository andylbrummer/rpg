# Overworld & Exploration

## Overworld Map

A node-based map of the Reach — towns, ruins, passes, wilderness zones connected by routes. Not open-world free movement. You choose a destination, time passes during travel, events can interrupt.

## Travel

### Route Properties
- **Distance:** Time cost in campaign turns
- **Danger rating:** Likelihood and difficulty of random encounters
- **Terrain type:** Affects which classes provide travel bonuses
- **Status:** Open, contested, blocked, or bloom-affected (changes during campaign)

### Travel Encounters

Random encounters during travel, drawn from pools based on route danger and campaign state. Each encounter type has defined resolution mechanics — they are gameplay, not flavor text.

| Encounter | Default Resolution | Class Alternative | Outcome |
|---|---|---|---|
| **Faction patrol** | Combat (if hostile) or reputation check | Ashmouth (Broker): diplomacy; Hollow (Liar): bluff past | Hostile: combat loot. Neutral: passage. Friendly: intel or supplies. |
| **Bloom pocket** | Mini-combat (2-3 bloom creatures) | Marcher (Pathfinder): bypass entirely; Cauterist (Scorcher): clear without combat | Combat: XP + bloom samples. Bypass: saves resources. Clear: XP, no loot. |
| **Merchant** | Trade dialogue (buy/sell at route-inflated prices) | Ashmouth (Broker): better prices | Resources exchanged. Rare stock rotates. |
| **Refugees** | Resource decision: give supplies (-supplies, +rep), ignore (no cost, no gain) | Cauterist (Surgeon): heal without supply cost (+rep); Hollow: rob them (+loot, -rep, +heat) | Reputation shifts; occasional recruitable NPC among refugees |
| **Ambush** | Combat with enemy surprise round | Marcher: prevents surprise, party acts first; Hollow (Fader): counter-ambush | Failed ambush: enemy morale penalty. Successful: party takes free damage. |
| **Environmental hazard** | Stat test (highest relevant stat in party) or resource cost to bypass | Marcher (Pathfinder): auto-bypass terrain hazards; Fieldwright: auto-bypass mechanical hazards | Failure: HP damage to party, possible route blocked. Success: passage. |

Encounter frequency: 0-2 per route segment. Danger rating 1-2 routes: 30% chance per segment. Danger rating 3-4: 60%. Danger rating 5: 90%.

### Travel Modifiers
- Marcher (Pathfinder) reduces travel time and ambush chance
- The Cartography faction sells route intel — reveals danger ratings and current status
- The Complication roll directly affects the overworld: closed passes, war zones, spreading blooms
- Faction reputation affects patrol encounters — high rep means safe passage, low rep means hostility

## Towns

2-4 towns per campaign, each with a distinct character based on which Engines they depend on and which factions have presence.

### Town Facilities

**Tavern**
- Recruit new roster members (class and level based on campaign progress)
- Hear rumors — some true, some outdated, some planted by factions
- Ashmouth (Broker) can verify rumor quality before acting on them
- Occasional unique recruits with pre-built specializations

**Market**
- Buy and sell gear, components, supplies
- Faction-specific vendors unlock with reputation thresholds
- Prices fluctuate based on town status and faction control
- Rare items rotate stock between expeditions

**Patron Office**
- Receive missions from your Patron faction (or whoever you're working for)
- Report findings, submit evidence, advance main questline
- Mission briefings may be genuine, misdirected, or traps (player learns to evaluate)
- Reputation affects mission quality and reward

**Faction Contacts**
- NPCs representing each faction present in that town
- Reputation-gated dialogue and services
- Side missions that build rep but may conflict with Patron interests
- Information exchange — factions trade intel for favors

**The Bone Clerk**
- Manage party death and resurrection affairs
- Pay tithe obligations (failure has reputation consequences)
- Resurrection services — costly and with escalating side effects
- Manage bone-tithe inventory for Bonewarden crafting

## Time & World State

### Turn Counter
The campaign runs on a turn counter. Each dungeon expedition and each overworld trip costs turns.

### Faction AI
Factions act on their own schedules between player turns:
- Claim or lose territory
- Complete or fail their own objectives
- React to player actions (both direct and indirect consequences)
- Move toward executing the Scheme (if Mastermind) or countering it (if aware)

### Pacing
- Not real-time pressure — more like "you have roughly X expeditions before the Scheme reaches its next phase"
- Three-act structure: Investigation (turns 1-15), Revelation (15-25), Confrontation (25-35)
- Flexible but the world escalates regardless of player action
- Missing windows closes options — an unrescued settlement stays lost, a faction betrayed doesn't forgive

## Information Economy

You're always working with incomplete information. The game never tells you the full truth — you assemble it from sources of varying reliability.

### Source Reliability

| Source | Reliability | Cost |
|---|---|---|
| **Dungeon documents** | High — first-hand evidence | Exploration risk |
| **Cartography intel** | High — accurate but sold to everyone | Gold (expensive) + Cartography rep |
| **Patron briefings** | Variable — genuine, misdirected, or fabricated (depends on whether Patron is Mastermind) | Free (comes with missions) |
| **Tavern rumors** | Mixed — true, outdated, or faction propaganda | Free, but unverified |
| **NPC faction contacts** | Biased toward their faction's interests | Reputation threshold |
| **Environmental observation** | High — faction markings, market prices, NPC behavior shifts | Attention (no mechanical cost, rewards careful players) |

### Rumor Verification

Rumors acquired in taverns have a hidden tag: **true**, **outdated**, or **planted**. The player doesn't see this tag. Verification resolves it.

**With Ashmouth (Broker):** Spend a downtime action in town to verify one rumor. The Broker contacts faction networks and returns the rumor's status: confirmed, expired, or fabricated + which faction planted it. This is reliable and costs nothing beyond the downtime slot.

**Without Ashmouth:** Spend a downtime action with an Inkblood or Hollow (Rumor) to investigate. This is less reliable — returns "likely true" / "likely false" with ~80% accuracy. Or: travel to the location the rumor references and check firsthand (costs turns, might be a waste).

**No verification:** Act on the rumor blind. True rumors provide good leads. Outdated rumors waste time. Planted rumors lead into traps or serve another faction's agenda.

### Closing Windows

The world moves without you. Opportunities expire. But the game signals closing windows so that missed chances feel like choices, not arbitrary punishment.

**Signal tiers:**

1. **Ambient signals** (always present) — market prices shift when a faction is about to move; NPCs mention "things are getting tense in [location]"; patrol frequency changes on routes
2. **Direct warnings** (reputation-gated) — faction contacts with 30+ rep explicitly warn you: "If you're going to act on [situation], do it before [approximate deadline]." Ashmouth (Broker) gets these warnings at 15+ rep.
3. **Turn counter markers** — certain events are tied to the turn counter. The quest log shows "Situation at [location] is deteriorating" with a rough urgency indicator (not an exact turn count): stable / developing / urgent / critical

**What happens when windows close:**
- A settlement that could have been saved is lost — it becomes a dungeon (Settlement Gone Wrong template) instead of a town
- A faction ally who could have been recruited joins the opposition
- Evidence that could have been recovered is destroyed — the Mastermind covers their tracks
- A dungeon that could have been entered at lower difficulty is now fortified or bloom-consumed

The player always has more opportunities than they can pursue. The strategic question is which windows to prioritize, not whether they can catch everything.
