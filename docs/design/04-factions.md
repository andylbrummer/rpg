# Factions

Five factions, each with a competing theory about the blooms and the resources to act on it. Reputation with each is tracked independently — helping one often costs standing with another.

Any faction can fill any narrative role (Patron, Threat, Mastermind, Wild Card) depending on the campaign's six rolls. Their personality stays consistent; their position in the story changes.

## The Bureau of Residual Affairs

**Identity:** Institutional, procedural, bureaucratic. The civil service of death management.

**Bloom Theory:** The blooms are a maintenance failure in the old empire's infrastructure. Contain and repair.

**Character:** Risk-averse, process-oriented, genuinely trying to keep people alive. Slow to act, prone to internal politics. The rank-and-file are decent. The higher you go, the murkier it gets.

**As Patron:** Steady contracts, access to Engine facilities, legal authority in city-states. Reliable but restrictive — they want reports filed and protocols followed.

**As Threat:** Authoritarian overreach. Quarantine zones that are really prisons. "Maintenance" operations that are cover for something else.

**As Mastermind:** The institution has been hollowed out and is being used as a tool. The player's own employer is the enemy.

**Exclusive Resources:** Engine maintenance tools, official writs of passage, forensic necromancy equipment.

## The Convocation

**Identity:** Hidden network of academics, idealists, and fanatics. Operates through infiltration and influence.

**Bloom Theory:** The blooms are the infrastructure evolving. Guide the process and it creates a post-scarcity Reach. Fight it and everyone dies anyway.

**Character:** Not cartoonishly evil — their vision has genuine appeal, and some of their science is correct. The cost is what they're willing to sacrifice to get there. True believers and opportunists in uneasy alliance.

**As Patron:** Forbidden knowledge, bloom-adapted equipment, powerful but compromising allies. They ask you to do things the Bureau wouldn't sanction.

**As Threat:** Bloom acceleration experiments in populated areas. Test subjects who didn't volunteer.

**As Mastermind:** Infiltrators in every other faction. The conspiracy the player gradually uncovers. Always three steps ahead.

**Exclusive Resources:** Bloom-adapted gear, mutation-resistant equipment, research notes on Engine modification.

## The Ossuary Compact

**Identity:** Coalition of bone-tithe families and independent necromancers. Decentralized, pragmatic, suspicious of centralized power.

**Bloom Theory:** The blooms are a threat to the existing order — and the existing order, for all its flaws, keeps people fed and warm. Defend what works.

**Character:** Closest thing to a populist faction. They represent ordinary people's relationship with necromancy — it's their dead, their families, their livelihood. Not noble, not villainous. Practical.

**As Patron:** Roster recruits, necromantic crafting, safe houses across the Reach. They know every back road and bone cellar.

**As Threat:** Protectionism turned violent. Attacks on anyone who threatens the tithe system, including reformers. Necromantic guild wars.

**As Mastermind:** The families have been consolidating power quietly. The "populist" movement is a front for an oligarchy of the oldest houses.

**Exclusive Resources:** Rare necromantic components, family-recipe enchantments, a network of safe houses and informants.

### Signature Mechanics

The Compact is the faction of families, inheritance, and the dead as personal property. Their mechanics reflect this:

- **Ancestral Bargaining:** Bonewardens with Compact reputation (25+) can negotiate with tithe-constructs instead of fighting them — turning potential combat encounters into dialogue encounters. The construct's "loyalty" is to a family, not a faction; the right name or bloodline token ends the fight.
- **Bloodline Locks:** Certain dungeon doors, vaults, and items are keyed to specific family bloodlines. Accessible via Compact reputation (40+) or a Compact-recruited character with the matching bloodline. These locks protect the Compact's best loot and deepest secrets.
- **Family Archives:** Compact contacts in towns can reveal family histories relevant to the current dungeon. This unlocks blackmail options against NPCs, safe passage through Compact-held territory, and alternative quest resolutions that avoid combat.

## The Stillness

**Identity:** Militant order dedicated to shutting down the Engines entirely. Ascetic, disciplined, terrifying.

**Bloom Theory:** The empire's infrastructure was always a mistake. The blooms prove it. The only cure is to let the Engines die and rebuild without necromancy.

**Character:** They're willing to let cities collapse to end the cycle. This makes them monsters to some and prophets to others. Internally, absolute discipline. They practice what they preach — no necromancy, no Engine benefits, survival through will and skill.

**As Patron:** Anti-magic capabilities, hardened combat specialists, access to shielded zones where bloom and necromancy don't function. They ask you to destroy, not preserve.

**As Threat:** Sabotage campaigns against Engines. Cities going dark. Civilians freezing because the Stillness decided their Engine was next.

**As Mastermind:** A controlled demolition of civilization, orchestrated to make the world dependent on the Stillness's protection.

**Exclusive Resources:** Anti-magic wards, null-zones, physical combat training, Engine-disruption tools.

## The Cartography

**Identity:** Scholars and explorers mapping the blooms, the ruins, and the connections between them. Information brokers.

**Bloom Theory:** Insufficient data. Map everything, understand everything, then decide. Knowledge before action.

**Character:** Neutral brokers — but information is never neutral. They sell to everyone and keep the best findings for themselves. Charming, helpful, and fundamentally self-serving.

**As Patron:** Maps revealing hidden dungeon areas, bloom forecasts, lore that unlocks quest paths. They want you to explore and report back. Generous with support, hungry for data.

**As Threat:** Information asymmetry weaponized. They know what's coming and position themselves to profit. They've been feeding bad intel to start conflicts.

**As Mastermind:** They already know what the blooms are. They've known for a while. The reason they haven't told anyone is worse than the blooms themselves.

**Exclusive Resources:** Detailed maps, bloom prediction models, imperial archive access, translation services for dead languages.

## Reputation System

- Reputation tracked as a signed integer per faction (-100 to +100)
- Actions, quest completions, and dialogue choices shift reputation
- Thresholds unlock vendor access, exclusive recruits, faction-specific quest lines, and information
- High reputation with one faction often costs reputation with opposed factions
- Reputation affects NPC behavior — high-rep faction members help you, low-rep ones obstruct or attack
- The Wild Card faction's alliance offer only triggers if reputation is above a minimum threshold

### Faction-Gated Branches and Hostile Factions

Some class branches require reputation with a specific faction (e.g., Beastkeeper requires Ossuary Compact rep). If that faction is the campaign's Threat, the player faces a deliberate tension: building rep with the enemy to unlock their character's potential.

This is intentional, not a trap. Design guidelines:
- Faction-gated branches are always available through a secondary path. The Beastkeeper can build Compact rep through side missions that don't directly oppose the party's main objectives — the Compact has rank-and-file members who aren't part of the Threat's operations.
- The reputation cost is real but bounded. Reaching the gate threshold (typically 25-30 rep) while the faction is Threat requires ~3 side missions, costing turns and potentially small rep losses with other factions.
- The player is never locked out of a branch they chose at level 3. If a level-3 branch path leads to a faction-gated level-6 branch, the gate is signaled at level 3 with a clear "this path requires [Faction] contacts" warning.
- If the player ignores the gate, they receive an alternate (weaker) version of the branch at level 6 — still functional, but missing the faction-exclusive abilities. The full version remains accessible if they later hit the rep threshold.
