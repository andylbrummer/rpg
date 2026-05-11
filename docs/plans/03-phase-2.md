# Phase 2: Strategic Depth

**Goal:** Does the strategic bind work? Do party composition choices feel consequential? Does the faction system create tension?

**Prerequisite:** Phase 1.5 complete. Formation, faction basics, and synergies validated.

**Duration estimate:** 10–12 weeks (2 engineers + 2 content authors + 1 QA)

**Phase 2 is the largest engineering and content investment in the project.** It introduces the full class system, all 5 factions, the complete overworld, and the campaign configuration framework. If Phase 2 fails to validate, the project pivots or cuts scope before the LLM and content volume investments of Phase 3.

---

## Group 10: Full Class System

### 52. Add Marcher + Ashmouth
**Layer:** Content  
**Owner:** Content author (lead)

**Subtasks:**
1. **Marcher (Pathfinder branch)** — 6 abilities:
   - Travel bonuses (overworld route danger reduction, ambush prevention)
   - Tracking (reveals enemy weaknesses at combat start)
   - Called Shot (high-damage single-target ranged)
   - Terrain Adaptation (buffs party based on route terrain)
   - Ability costs: standard arrow/ammo (new component type: `quiver`)
2. **Ashmouth (Agitator branch)** — 6 abilities:
   - War Cry (morale damage to all enemies)
   - Rally (combat buff to party)
   - Negotiate (faction soldier encounter alternative — converts combat to dialogue)
   - Intimidate (enemy action delay)
   - Ability costs: none (Ashmouth abilities are social/physical, not component-based)
3. Starting stats: Marcher (back-liner HP ~22, light armor, high speed); Ashmouth (front-liner HP ~26, medium armor).
4. Level-up tables: levels 1–10.

**Content format:** Same JSON schema as existing classes. Add `quiver` component to component table.

**Acceptance criteria:**
- Both classes appear in tavern roster with distinct ability sets.
- Marcher Pathfinder ability reduces route danger rating by 1 in overworld.
- Ashmouth Negotiate ability offers dialogue option when facing faction soldiers.

**Depends on:** Phase 1.5 Group 6 (formation, branch system)

---

### 53. Branch system (levels 3 and 6)
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Extend branch engine from Phase 1.5 (task 35b):
   - Add level 6 branch choice tracking.
   - `branchLevel3: string | null`, `branchLevel6: string | null`.
2. Level 3 branch unlock: 2 options (as in Phase 1.5).
3. Level 6 branch unlock: 2–3 options depending on level-3 choice.
   - Some level-6 branches are gated by faction reputation (e.g., Beastkeeper needs Compact 25+).
   - If gate not met, offer alternate (weaker) version.
4. Branch prerequisite validation: level-6 options are filtered by level-3 choice. Cannot pick Animator at 3 and Beastkeeper at 6.
5. Faction-gated branch fallback: if player doesn't meet rep threshold at level 6, auto-assign alternate branch and notify UI.

**Acceptance criteria:**
- Character with Animator at level 3 sees only Animator-adjacent branches at level 6.
- Character choosing Beastkeeper at level 6 without Compact 25+ gets "Beastkeeper (Untrained)" with reduced abilities.
- Save/load preserves both branch choices.

**Depends on:** Phase 1.5 task 35b

---

### 54. All 8 classes, all branches
**Layer:** Content  
**Owner:** Content authors (both)

**Deliverables:**
- Complete ability trees for all 8 classes × 2–3 branches = ~20 branch definitions.
- Each branch: 6–8 abilities (2 basic, 2 core, 2 advanced, 1–2 ultimate).
- Ability definitions include: name, description, cost (component/HP/memory), targeting, range band, damage/heal formula, status effects, tags.
- Cross-branch balance pass: ensure no branch is strictly superior to others in its class.

**Content volume:**
- ~160 abilities total.
- ~40 abilities per class (across all branches).

**Authoring workflow:**
- Spreadsheet → JSON pipeline for bulk ability stats.
- Hand-authored descriptions and names.
- Build-time validator checks: all ability IDs unique, all component costs reference valid components, all tags are from approved tag list.

**Acceptance criteria:**
- Every ability in every branch appears in combat action menu when branch is unlocked.
- No ability has a null or broken cost reference.
- Snapshot test suite covers at least 1 ability from each branch.

**Depends on:** Tasks 52, 53

---

### 55. Level cap 10
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Extend XP curve from level 5 cap to level 10.
2. Tuning targets (per design doc 05):
   - Level 3 reached by turn 6–10 (first branch choice).
   - Level 6 reached by turn 16–20 (second branch choice).
   - Level 10 reached by turn 31–35 (endgame).
3. XP sources:
   - Combat: base XP per enemy + level difference bonus.
   - Exploration: XP per new room segment discovered (diminishing returns after 50% of dungeon).
   - Quest completion: flat XP scaled to quest difficulty.
4. Level-up UI: show stat gains, new ability unlocks, branch choice prompt (if applicable).

**Acceptance criteria:**
- Playtest campaign reaches level 10 by turn 35 with standard play.
- Level 3 is reachable after 1–2 dungeons.
- Level 6 is reachable at roughly campaign midpoint.

**Depends on:** Task 53

---

### 56. Roster/bench system
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. `RosterState` struct: `active: Character[6]`, `bench: Character[12]`, `dead: Character[]`.
2. Bench rules:
   - Bench characters do not gain XP from combat or exploration.
   - Bench characters can perform downtime actions.
   - Bench characters can be swapped into active party in town.
3. Field promotion: characters below party average level gain 50% bonus XP until within 1 level of average.
4. Roster cap: 12 total (active + bench). Recruiting at cap requires dismissing a character (permanent).
5. Death: dead characters move to `dead` list. Can be resurrected at Bone Clerk (see task 65).

**Acceptance criteria:**
- Swapping bench character into active party updates formation UI.
- Bench character gaining XP with field promotion bonus reaches party average in 2–3 dungeons.
- Roster cap enforced: cannot recruit 13th character without dismissal.

**Depends on:** Phase 1.5 Group 6 (party system)

---

### 57. Roster management UI
**Layer:** Client  
**Owner:** Frontend lead

**Subtasks:**
1. Roster screen: grid of all 12 roster slots.
2. Character cards: portrait, class icon, level, branch (if chosen), HP bar, status icons.
3. Drag-and-drop: swap between active and bench. Active must always have 6 characters.
4. Dismissal: right-click → dismiss → confirmation modal (warns if character is unique/faction-exclusive).
5. Filter/sort: by class, level, HP, faction affinity.

**Acceptance criteria:**
- Roster screen opens from tavern or town menu.
- Dragging a benched character to active party swaps them with the dragged-out active character.
- Dismissing a character frees the slot; the character is permanently removed.

**Depends on:** Tasks 53, 56

---

## Group 11: Full Faction System

### 58. All 5 factions
**Layer:** Content  
**Owner:** Content authors (both)

**Deliverables:**
1. **Stillness** profile:
   - Vendor: anti-magic wards, null-zone generators, Engine-disruption tools.
   - Side missions: "Engine Sabotage" (destroy Engine components), "Recruitment Drive" (rescue civilians from bloom).
   - Contacts: militant ascetics, suspicious of necromancy.
2. **Ossuary Compact** profile:
   - Vendor: rare bone fragments, family-recipe enchantments, bloodline keys.
   - Side missions: "Tithe Collection" (gather tithe tokens), "Family Dispute" (diplomatic resolution).
   - Contacts: family elders, pragmatic necromancers.
   - Signature mechanics content: ancestral bargaining dialogue trees, bloodline lock key items, family archive documents.
3. **Cartography** profile:
   - Vendor: maps, bloom prediction scrolls, translation guides.
   - Side missions: "Survey Mission" (explore unmapped route), "Intel Exchange" (trade documents for maps).
   - Contacts: charming scholars, information brokers.
4. Update Bureau and Convocation from Phase 1.5 with expanded mission sets (4 side missions each).

**Content volume:**
- 5 faction profiles.
- 20+ side missions total.
- 30+ faction-exclusive items.
- 15+ NPC contact dialogue sets.

**Acceptance criteria:**
- All 5 factions appear in reputation UI.
- Each faction has at least 1 unique vendor item not available elsewhere.
- Each faction has at least 2 side missions playable in a single campaign.

**Depends on:** Phase 1.5 Group 7 (reputation system)

---

### 59. Compact signature mechanics
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. **Ancestral Bargaining:**
   - Encounter flag: `negotiable: true` on tithe-construct encounters.
   - Check: party has Bonewarden AND Compact rep ≥ 25.
   - If met, combat start offers "Negotiate" option alongside "Fight" and "Flee".
   - Negotiation: simple dialogue tree (2–3 choices). Success ends encounter peacefully; failure starts combat with initiative penalty.
2. **Bloodline Locks:**
   - Dungeon segment tag: `bloodline_lock: "family-name"`.
   - Check: party has Compact rep ≥ 40 OR a Compact-recruited character with matching bloodline.
   - If unlocked, segment opens revealing loot. If locked, segment is impassable.
3. **Family Archives:**
   - Interactable object in Compact-influenced dungeons.
   - Grants document item with faction intel (counts toward evidence system).

**Acceptance criteria:**
- Tithe-construct encounter with Bonewarden + Compact 25+ shows negotiate option.
- Bloodline-locked door opens for matching character; remains solid for others.
- Family archive document appears in inventory and can be submitted as evidence.

**Depends on:** Task 58

---

### 60. Faction-gated recruits
**Layer:** Engine + Content  
**Owner:** Backend lead + Content author

**Subtasks:**
1. Recruit definition schema: `factionRequirement: { faction: string, threshold: int }`.
2. Tavern roster generation: filter available recruits by reputation thresholds.
3. Exclusive recruits:
   - Beastkeeper-in-training (Compact 25+): Marcher subclass, starts with partial Beastkeeper abilities.
   - Heretic acolyte (Convocation 25+): Inkblood subclass, starts with bloom magic.
   - Bureau informant (Bureau 25+): Hollow subclass, starts with intel-gathering abilities.
   - Stillness defector (Stillness 25+): Stillblade subclass, anti-magic focus.
   - Cartographer scout (Cartography 25+): Marcher subclass, mapping bonuses.
4. Unique named recruits: 4–6 named NPCs with pre-set branches and special abilities. Each requires 40+ rep with a specific faction.

**Acceptance criteria:**
- Tavern shows exclusive recruits only when reputation threshold is met.
- Exclusive recruits have distinct portraits and ability sets.
- Named recruits cannot be replaced if they die.

**Depends on:** Tasks 56, 58

---

### 61. Faction-gated branch fallback
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Branch definition schema extension: `factionGate: { faction: string, threshold: int }` and `fallbackBranch: string`.
2. At level 6, if player chooses a gated branch but doesn't meet threshold:
   - Auto-assign fallback branch.
   - UI notification: "You lack the connections for [Branch]. Learning [Fallback Branch] instead."
   - If player later meets the threshold, offer a one-time respec in town (costs gold + downtime).
3. Gate warning at level 3: when player chooses a branch path that leads to a gated level-6 branch, show warning in branch choice modal.

**Acceptance criteria:**
- Player choosing Animator → Beastkeeper path at level 3 sees warning: "Beastkeeper requires Ossuary Compact contacts."
- Player at level 6 without Compact 25+ gets "Beastkeeper (Untrained)" instead.
- Respec option appears in town UI when threshold is later met.

**Depends on:** Tasks 53, 58

---

### 62. Reputation impact on encounters
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Travel encounter modification: faction patrols check rep.
   - Rep ≥ 25: friendly patrol (offers intel/supplies).
   - Rep 0–24: neutral patrol (allows passage).
   - Rep < 0: hostile patrol (combat).
2. Dungeon encounter modification: faction soldiers check rep.
   - Rep ≥ 25: soldiers may offer negotiation instead of combat.
   - Rep < -25: soldiers are more aggressive (higher damage, no retreat).
3. Town NPC attitude: faction contacts use different dialogue sets based on rep tier.
   - Hostile (-100 to -25), Suspicious (-24 to 0), Neutral (1–24), Friendly (25–49), Allied (50–100).

**Acceptance criteria:**
- Bureau patrol at +30 rep offers dialogue; at -30 rep attacks immediately.
- Faction soldier encounter at +30 rep shows "Parley" option.
- Contact dialogue changes across all 5 attitude tiers.

**Depends on:** Tasks 58, 49 (travel encounters)

---

## Group 12: Expanded Combat

### 63. Full synergy library
**Layer:** Content  
**Owner:** Content author (lead)

**Deliverables:**
1. 15–20 synergies covering all viable class pairs.
2. At least one synergy per branch pair that appears in a typical campaign.
3. Secret synergies started: 2–3 item-required or environmental synergies.
   - Example: "Bone Servant + Overcharge" requires Animator + Artificer + an Engine Charge item.
4. Synergy balancing: no single synergy should out-damage a character's entire turn.

**Content format:** Same JSON schema as Phase 1.5. Each synergy references ability IDs.

**Acceptance criteria:**
- All synergies trigger in snapshot tests.
- No synergy deals more than 150% of a strong ability's damage.
- Secret synergies are not discoverable without the required item/environment.

**Depends on:** Phase 1.5 task 43 (synergy engine), Task 54 (all branches)

---

### 64. Faction soldier AI
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Faction soldier decision tree:
   - Assessment phase: compare total enemy HP vs ally HP. If ratio < 0.5, trigger retreat.
   - Retreat action: shift to long range, then flee on next turn if not blocked.
   - Synergy use: faction soldiers have their own synergy pairs (e.g., two Bureau soldiers using "Flanking Maneuver").
   - Equipment matching: soldier stats modified by faction gear (Bureau soldiers have higher armor; Convocation soldiers have bloom resistance).
2. Ashmouth negotiation check: if party has Ashmouth, combat start offers "Negotiate." Success requires Ashmouth level ≥ enemy leader level + rep check.
3. Negotiation outcomes:
   - Complete success: enemies leave, drop partial loot.
   - Partial success: enemies leave but warn allies (future encounters in dungeon are harder).
   - Failure: combat starts with enemy surprise round.

**Acceptance criteria:**
- Outnumbered faction soldiers attempt retreat.
- Bureau soldier pair triggers "Flanking Maneuver" synergy in combat.
- Ashmouth negotiation success bypasses combat entirely.

**Depends on:** Tasks 52 (Ashmouth), 58 (factions)

---

### 65. Death and resurrection
**Layer:** Engine + Client  
**Owner:** Both leads

**Subtasks:**
1. Death flow:
   - Downed (0 HP) → can be stabilized by Cauterist Surgeon or healing item before combat ends.
   - If combat ends with downed unstabilized character → dead.
   - Dead character removed from active party; moved to `dead` list.
2. Resurrection at Bone Clerk:
   - UI panel: lists dead characters with resurrection cost and consequence.
   - Attempt 1: 500g + 1 Tithe Token, -1 random primary stat.
   - Attempt 2: 1500g + 2 Tithe Tokens, -2 random primary stat, locked out of next branch advancement.
   - Attempt 3: impossible. Character permanently dead.
3. Tithe Token tracking: rare currency earned from Compact missions and bloom containment.
4. Save/load: death state and resurrection attempts persisted.

**Acceptance criteria:**
- Character dying in combat appears in Bone Clerk dead list.
- Resurrection applies permanent stat loss visible in character sheet.
- Third resurrection attempt is disabled in UI.

**Depends on:** Phase 1 Group 4 (combat), Task 56 (roster)

---

### 66. Component inventory
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Per-character inventory: 8 slots. Equipment worn does not count.
2. Component stacking: each component type stacks to its defined limit within one slot.
3. Expedition cache: 12 slots shared by party. Accessible between combats, not during.
4. Fallback casting: Bonewarden without bone fragments can cast using blood (HP) at 2× cost. Other classes have no fallback.
5. Inventory UI warnings: low component count (< 3 uses remaining) shows yellow warning; empty shows red warning.

**Acceptance criteria:**
- Character with 20 bone fragments in one slot shows stack count.
- Expedition cache accessible from dungeon pause menu, not combat action menu.
- Bonewarden at 0 bone fragments can still cast; HP cost is exactly 2× the fragment cost.

**Depends on:** Phase 1 Group 3 (inventory)

---

### 67. Component inventory UI
**Layer:** Client  
**Owner:** Frontend lead

**Subtasks:**
1. Inventory rework: component counts displayed prominently on character cards.
2. Expedition cache panel: 12-slot grid, accessible between combats.
3. Low-stock warnings: colored borders (yellow/orange/red) on component icons.
4. Transfer UI: move components between characters and expedition cache.

**Acceptance criteria:**
- Component count is readable at a glance in inventory.
- Warning colors update in real-time as components are consumed.
- Transfer between characters and cache takes 1 click per stack.

**Depends on:** Tasks 66

---

### 68. Downtime system
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Downtime action allocation: one action per character per town visit.
2. Actions:
   - Rest: recover all HP, clear temporary stat penalties.
   - Train: +25% XP toward next level.
   - Craft: convert raw materials to components/consumables (Fieldwright, Bonewarden only).
   - Network: +5 rep with faction present in town (Ashmouth Broker only).
   - Investigate: verify one rumor (Inkblood, Hollow Rumor only).
   - Lay Low: -30 Hollow heat.
   - Tend Blooms: stabilize one bloom sample (Heretic only).
3. Bench characters can also perform downtime actions.
4. Downtime does not cost campaign turns.

**Acceptance criteria:**
- Each character gets exactly 1 downtime action per town visit.
- Rest action restores full HP.
- Train action applies XP bonus immediately.
- Downtime choices are persisted in save; reloading in town preserves unspent actions.

**Depends on:** Phase 1.5 Group 9 (town system)

---

### 69. Downtime UI
**Layer:** Client  
**Owner:** Frontend lead

**Subtasks:**
1. Town screen: downtime section showing all roster characters (active + bench).
2. Per-character dropdown: select downtime action. Actions filtered by class/branch eligibility.
3. Bulk assign: "Rest all" button for convenience.
4. Confirmation: downtime actions are committed when player clicks "Leave Town." Until then, choices can be changed.

**Acceptance criteria:**
- Dropdown only shows actions the character is eligible for.
- "Rest all" assigns Rest to every character in one click.
- Leaving town commits downtime; returning to town does not allow re-assigning.

**Depends on:** Task 68

---

## Group 13: Full Overworld

### 70. Node-based overworld
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Overworld graph expansion: 2–4 towns, multiple dungeon entrances, 4–8 routes.
2. Route properties:
   - Distance: 1–3 campaign turns.
   - Danger rating: 1–5.
   - Terrain type: plains, mountain, swamp, tunnel.
   - Status: open, contested, blocked, bloom-affected.
3. Node types:
   - Town: has full facilities.
   - Dungeon entrance: links to a dungeon template instance.
   - Pass/ruin: non-town nodes that may have encounters or shortcuts.
4. Campaign generation assigns nodes and routes based on six-roll config.

**Acceptance criteria:**
- Overworld graph is traversable: every node reachable from every other node (unless blocked by status).
- Route status changes affect travel options (blocked routes cannot be traversed).
- Save/load preserves current node, route statuses, and node states.

**Depends on:** Phase 1.5 Group 9 (overworld foundation)

---

### 71. Overworld map renderer
**Layer:** Client  
**Owner:** Frontend lead

**Subtasks:**
1. Full-screen overworld map: node graph with stylized icons.
2. Node visuals: distinct art per town (based on Engine dependence and faction presence).
3. Route visuals: line thickness = danger, color = status (green=open, red=blocked, purple=bloom).
4. Faction territory markers: regions shaded by dominant faction.
5. Player marker: animates along route during travel.

**Acceptance criteria:**
- Map readable at a glance: can identify safe routes vs dangerous routes.
- Faction territory visible as background shading.
- Player marker movement is smooth during travel.

**Depends on:** Task 70

---

### 72. Travel encounter system
**Layer:** Engine + Content  
**Owner:** Backend lead + Content author

**Subtasks:**
1. Full encounter tables per route: each route has a weighted table based on danger and terrain.
2. Resolution mechanics per design doc 08:
   - Combat: full combat with 2–4 enemies.
   - Stat test: highest relevant stat in party vs difficulty DC.
   - Dialogue: class-specific alternatives (Broker, Liar, Pathfinder).
3. Class-specific alternatives implemented:
   - Ashmouth (Broker): diplomacy, better prices, rumor verification.
   - Hollow (Liar): bluff past patrols, rob refugees.
   - Marcher (Pathfinder): bypass bloom pockets, prevent ambushes.
   - Cauterist (Scorcher): clear bloom pockets without combat.
4. Encounter frequency: 0–2 per route segment, scaled by danger rating (per design doc 08 table).

**Acceptance criteria:**
- Each route has a distinct encounter table.
- Class alternatives appear as explicit options in encounter UI.
- Stat test resolution shows roll animation and outcome.

**Depends on:** Tasks 70, 52 (new classes with travel abilities)

---

### 73. Route status changes
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Route status state machine: `open → contested → blocked → bloom-affected`.
2. Transition triggers:
   - Campaign turn milestones (e.g., turn 12: route X becomes contested).
   - Faction actions (e.g., Stillness sabotage blocks route).
   - Complication roll (e.g., Open War makes multiple routes contested at start).
3. Status effects:
   - Contested: +1 danger, faction patrols guaranteed.
   - Blocked: cannot traverse without quest resolution or alternative path.
   - Bloom-affected: bloom pocket encounters guaranteed; necromancy debuff in combat.
4. UI notification when route status changes.

**Acceptance criteria:**
- Blocked route prevents travel; UI shows reason.
- Contested route has increased encounter rate.
- Status changes are saved and persist on reload.

**Depends on:** Task 70

---

### 74. Town facilities (full)
**Layer:** Client + Engine  
**Owner:** Both leads

**Subtasks:**
1. **Tavern:**
   - Recruit roster: 4–6 random characters per town visit, scaled to campaign level.
   - Rumor board: 3–5 rumors per visit, drawn from town's rumor pool.
   - Unique recruit slots: 1 per town visit, rare chance.
2. **Market:**
   - Base inventory: 10–15 items per town.
   - Faction vendors: unlocked by reputation.
   - Price fluctuation: base price × town modifier × faction control modifier. Modifiers range 0.8–1.5.
   - Rare stock rotation: 2 rare items per town, change every 2 town visits.
3. **Patron Office:**
   - Mission board: 2–3 main missions, 1–2 side missions.
   - Evidence submission: submit documents to advance Mastermind discovery.
   - Reputation check: higher rep = better rewards.
4. **Faction Contacts:**
   - One contact per faction present in town.
   - Dialogue sets: 3 per contact (greeting, mission offer, special service).
5. **Bone Clerk:**
   - Resurrection panel (task 65).
   - Tithe payment (design doc 05).
   - Bone fragment storage for Bonewarden crafting.

**Acceptance criteria:**
- Tavern recruits scale to party level ± 1.
- Market prices vary between towns by at least 20%.
- Patron office missions advance the campaign questline.

**Depends on:** Tasks 58 (factions), 65 (resurrection), 68 (downtime)

---

### 75. Rumor system
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Rumor data model: `Rumor { id, text, truthStatus: true | outdated | planted, plantingFaction: string | null, verificationSource: string }`.
2. Rumor distribution:
   - Each town has a rumor pool of 10–20 rumors.
   - Tavern shows 3–5 random rumors per visit.
   - True rumors: 40%. Outdated: 30%. Planted: 30%.
3. Verification:
   - Ashmouth Broker: reliable verification (100% accuracy), costs downtime.
   - Inkblood/Hollow: 80% accurate verification, costs downtime.
   - Firsthand check: travel to location, costs turns, 100% accurate.
4. Rumor effects:
   - True: reveals actual content (dungeon location, faction movement, quest opportunity).
   - Outdated: content no longer exists (wasted turn if pursued).
   - Planted: leads to trap or serves planting faction's agenda.

**Acceptance criteria:**
- Player can verify a rumor and see its status.
- Acting on a true rumor leads to valid content.
- Acting on a planted rumor leads to a harder encounter or faction rep loss.

**Depends on:** Task 74 (tavern)

---

### 76. Closing window signals
**Layer:** Engine + Client  
**Owner:** Both leads

**Subtasks:**
1. **Ambient signals:**
   - Market price shifts when faction is about to move (+20% on faction-controlled goods).
   - NPC dialogue mentions tension ("Things are getting tight in Ashmark").
   - Patrol frequency changes on routes (more patrols = faction mobilizing).
2. **Direct warnings:**
   - Faction contacts with 30+ rep warn of impending events.
   - Ashmouth Broker gets warnings at 15+ rep.
   - Warning format: "If you're going to [act], do it before [approximate deadline]."
3. **Turn counter markers:**
   - Quest log shows urgency indicator: stable → developing → urgent → critical.
   - Indicators tied to specific events (e.g., settlement rescue window).
4. Window closure effects:
   - Settlement lost → becomes Settlement Gone Wrong dungeon.
   - Faction ally joins opposition → exclusive recruit no longer available.
   - Evidence destroyed → evidence counter doesn't increment.

**Acceptance criteria:**
- Player receives at least 2 signals before a window closes.
- Missing a window changes world state visibly (settlement lost, NPC gone).
- Urgency indicators update in quest log without requiring manual refresh.

**Depends on:** Tasks 73 (route status), 75 (rumors)

---

## Group 14: Campaign Configuration

### 77. Six-roll system
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. `CampaignConfig` struct: stores the six rolls and all derived campaign state.
2. Roll logic:
   - Patron: random from 5 factions.
   - Threat: random from remaining 4.
   - Mastermind: random from remaining 3 (can be Patron).
   - Scheme: random from 6.
   - Wild Card: random from remaining uninvolved factions.
   - Complication: random from 6.
3. Coherence validation: ensure no impossible combinations (e.g., Wild Card cannot be Threat).
4. Campaign config file: hand-authored JSON for Phase 2 (no LLM yet).

**Acceptance criteria:**
- 1000 random roll generations produce 0 invalid combinations.
- Each roll is independently selectable for testing.
- Campaign save includes full config for reproduction.

**Depends on:** Nothing (new system)

---

### 78. 3 Schemes + 3 Complications
**Layer:** Content  
**Owner:** Content author (lead)

**Deliverables:**
1. **Schemes (3 of 6):**
   - Bloom Harvest: event chain focusing on bloom acceleration.
   - Engine Seizure: event chain focusing on Engine weaponization.
   - Cascade Failure: event chain focusing on multi-Engine sabotage.
   - Each scheme: 5–7 authored events, finale dungeon setpiece, faction involvement map.
2. **Complications (3 of 6):**
   - Bloom Siege: starting city under bloom threat.
   - Open War: two factions fighting, contested routes.
   - Tithe Collapse: no bone tithe infrastructure in region.
   - Each complication: world-state modifiers, route status changes, faction behavior adjustments.

**Content format:** JSON event chains in `content/campaigns/schemes/` and `content/campaigns/complications/`.

**Acceptance criteria:**
- Each scheme has a distinct finale dungeon feel.
- Each complication visibly changes the overworld at campaign start.
- Two campaigns with different scheme+combination feel different in the first 10 turns.

**Depends on:** Task 77

---

### 79. Evidence system
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. `EvidenceState` struct: `Map<FactionId, int>` — counter per faction.
2. Evidence sources:
   - Documents found in dungeons (see task 81).
   - NPC behavioral shifts (dialogue choices revealing info).
   - Operational anomalies (mission parameters not matching briefing).
3. Threshold effects (per design doc 03):
   - 3 evidence: Inkblood Archivist can flag faction as suspicious.
   - 5 evidence: Ashmouth Broker can confront contacts.
   - 7 evidence: public accusation triggers Act 3 confrontation.
   - 10 evidence: irrefutable proof, final dungeon unlocks.
4. Evidence is hidden from player; effects appear through dialogue options and quest log updates.

**Acceptance criteria:**
- Finding a document increments the correct faction's evidence counter.
- At 3 evidence, Inkblood sees new Field Notes entry.
- At 7 evidence, "Accuse" option appears in Patron Office dialogue.

**Depends on:** Nothing (new system)

---

### 80. Mastermind discovery flow
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Five evidence channels tracked per campaign:
   - Documentary contradictions.
   - NPC behavioral shifts.
   - Operational anomalies.
   - Faction pattern recognition.
   - Direct confrontation.
2. Class checks at thresholds:
   - Inkblood (Archivist) at 3 evidence: identifies suspicious faction.
   - Ashmouth (Broker) at 5 evidence: confronts contacts for confirmation.
3. Act 3 trigger: at 7+ evidence, player can accuse faction publicly.
   - Accusation correct: confrontation path opens, faction reacts.
   - Accusation wrong: -20 rep with accused faction, Mastermind gains time.
4. Final dungeon unlock: at 10 evidence, Mastermind's location is revealed.

**Acceptance criteria:**
- Player can reach Act 3 confrontation with 7 evidence.
- Wrong accusation has real consequences (rep loss, harder endgame).
- Correct accusation at 10 evidence reveals shortest path to finale.

**Depends on:** Task 79

---

### 81. 4 dungeon templates
**Layer:** Content + Client  
**Owner:** Content authors (both) + Frontend lead

**Deliverables:**
1. **Broken Engine** (updated from Phase 1): 15–20 room segments, 6–8 encounters, 1 setpiece.
2. **Bloom Site** (updated from Phase 1.5): 15–20 room segments, 6–8 encounters, 1 setpiece.
3. **Contested Ruin** (new): 15–20 room segments, faction soldiers as primary enemies, negotiation opportunities, rival party evidence.
4. **Underway** (new): 15–20 room segments, connector dungeon, procedural variation per traversal, junction chambers.
5. Visual themes for Contested Ruin and Underway in renderer.
6. Encounter tables per template, scaled to party level and campaign act.

**Content volume:**
- 60–80 room segments total.
- 24–32 encounters.
- 4 setpieces.

**Acceptance criteria:**
- All 4 templates load and assemble without errors.
- Contested Ruin has at least 1 negotiation encounter per run.
- Underway changes layout on second traversal (different segment arrangement).

**Depends on:** Phase 1 Group 2 (dungeon assembler)

---

### 82. Turn counter + world state
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Extend turn counter from 15 (Phase 1.5) to 35 turns.
2. Three-act structure:
   - Act 1 (turns 1–15): investigation, roster building.
   - Act 2 (turns 15–25): revelation, Mastermind apparent.
   - Act 3 (turns 25–35): confrontation, finale.
3. Faction state progression (manual/scripted, not AI yet):
   - Turn 12: factions transition Investigating → Preparing.
   - Turn 22: factions transition Preparing → Executing.
   - Scripted events fire at transitions.
4. World state tracking: which settlements are saved/lost, which dungeons are accessible, faction territory control.

**Acceptance criteria:**
- Campaign reaches turn 35 with standard play.
- Act transitions trigger scripted events.
- World state changes are visible on overworld map.

**Depends on:** Phase 1.5 task 50 (turn counter)

---

### 83. Wild Card trigger
**Layer:** Engine  
**Owner:** Backend lead

**Subtasks:**
1. Wild Card faction determined at campaign generation.
2. Trigger condition: campaign turn 18–24 AND player has 20+ rep with Wild Card faction.
3. Trigger event: alliance offer appears in town.
   - Accept: +20 Wild Card rep, faction provides resources (gear, intel, exclusive recruit).
   - Refuse: no rep change, Wild Card remains neutral.
   - Ignore: offer expires at turn 25.
4. Alliance benefits:
   - Wild Card soldiers assist in combat (random chance).
   - Wild Card vendor prices reduced 25%.
   - Unique questline opens.

**Acceptance criteria:**
- Alliance offer appears only if both conditions are met.
- Accepting alliance changes combat (Wild Card soldiers appear as allies).
- Refusing alliance has no negative consequences.

**Depends on:** Tasks 77, 82

---

### 84. Campaign snapshot tests
**Layer:** Tests  
**Owner:** QA / Backend lead

**Subtasks:**
1. 3 hand-authored campaign configs with different six-roll combinations.
2. Test assertions:
   - Dungeon sequence is valid (all dungeons traversable, critical path exists).
   - Evidence count placed ≥ 10 (achievable threshold).
   - Faction states transition at correct turns.
   - Wild Card trigger conditions are possible to meet.
3. Automated test: load config → simulate 35 turns → assert end state.

**Acceptance criteria:**
- All 3 configs pass validation.
- Evidence count is always ≥ 10.
- Faction transitions happen at turns 12 and 22 ± 1.

**Depends on:** Tasks 77, 79, 82

---

## Group 14.5: Spec Coverage Gaps

### 84a. Hollow heat meter
**Layer:** Engine + Client
**Owner:** Backend lead + Frontend lead

**Subtasks:**
1. `HeatState` on campaign save: hidden int 0–100.
2. Heat sources per design doc 05 (rob refugees +25, market theft +20, pickpocket +15, assassinate +30, plant rumor +10, threaten +5).
3. Heat thresholds per design doc 05 (0–20 none; 21–40 price + recruit penalty; 41–60 Bureau patrols + random search; 61–80 contacts refuse + Bone Clerk reports; 81–100 lockdown).
4. Heat reduction: `Lay Low` downtime (-30), Broker passive (-10 per town visit), Bribe UI (-20 for 300g, repeatable), natural decay -5 per turn.
5. UI: heat is hidden; surface via NPC dialogue cues, market price flag, patrol presence indicator. Optional Inkblood/Archivist reveal at 5 evidence equivalent (design lead final call before implementation).
6. Lockdown encounter: town guard combat encounter at 81+; bribe path always available.

**Acceptance criteria:**
- Robbing refugees adds 25 heat; entering town at 45 heat triggers Bureau patrol presence + random search outcome.
- `Lay Low` reduces heat by exactly 30.
- Heat persists across dungeons; decays -5 per campaign turn.

**Depends on:** Tasks 52 (Hollow + Ashmouth), 68 (downtime), Phase 1.5 task 38 (reputation, for opposed effects)

---

### 84b. Tithe obligations
**Layer:** Engine + Client
**Owner:** Backend lead

**Subtasks:**
1. Tithe scheduler: due on entering any town if current turn ≥ next tithe turn (acts 1/2/3 trigger: turns 1, 15, 25).
2. Tithe cost: `ceil(activePartySize / 3)` Tithe Tokens; debt accumulates if unpaid.
3. Bone Clerk UI prompt: list tithes due, debt total, pay/defer buttons. Auto-open on town entry when debt exists.
4. Non-payment penalties: -10 Compact rep per unpaid token, Bonewarden component cost +50%, Compact contacts refuse interaction.
5. Late payment: full back-payment + 25% gold penalty.
6. Tithe tokens earned via Compact missions (task 58), bloom containment (Convocation cross-grant), and rare dungeon loot (1–2 per dungeon roll).

**Acceptance criteria:**
- Party of 6 at turn 15 owes 2 tokens on next town entry.
- Skipping tithe applies all listed penalties immediately.
- Paying overdue tithe at 1000g cost shows 25% surcharge line item.

**Depends on:** Task 74 (Bone Clerk facility), Task 58 (Compact content)

---

### 84c. Secret discovery — full system
**Layer:** Engine + Client + Content
**Owner:** Backend lead + Content author

**Subtasks:**
1. Add secret types per design doc 12: `concealed_compartment` (no visual indicator) and `illusory_floor` (looks like floor, becomes pit).
2. Class bypass abilities:
   - Cartographer's Eye (Inkblood Cartographer): already in Phase 1.5; extend to all dungeon templates.
   - Tremorsense (Bonewarden Animator): reveals breakable walls + hidden pits within 1 tile.
   - Trapfinding (Hollow Filch): reveals concealed compartments and traps; +25% search rolls.
   - Engine Whispers (Fieldwright Artificer): reveals hidden doors and Engine-tech secrets in Broken Engine.
3. Document-triggered discovery: documents tagged `revealSecretId: "<id>"` auto-reveal on read.
4. Enemy-triggered discovery: enemy flee paths flagged to traverse hidden doors and auto-reveal.
5. Illusory floor implementation: tile flagged `illusory_floor`; party stepping in falls to encounter or sub-level segment; Marcher Pathfinder detects passively at range 2.

**Acceptance criteria:**
- Hollow Filch in party reveals concealed compartments within search range.
- Document "Maintenance Logs #7" read in inventory auto-reveals the linked hidden door.
- Marcher detects illusory floor before party steps in.

**Depends on:** Phase 1 task 25a, Phase 1.5 task 51b, Tasks 52, 53, 54

---

### 84d. Display + accessibility settings
**Layer:** Client
**Owner:** Frontend lead

**Subtasks:**
1. Display section per design doc 10: Resolution, Fullscreen mode, V-Sync, FOV (60°–90° step 5), camera bob, camera shake.
2. Resolution change: Photino window resize + Three.js renderer resize on apply.
3. Accessibility section: colorblind presets (Deuter/Protan/Tritan), text size (clamp font-size root), motion reduction (disables bob + replaces synergy flash with solid overlay), high contrast outlines, subtitles.
4. Subtitle system: engine audio events carry `subtitle_id`; client overlay displays text 2–3s.
5. Settings applied before first frame on startup.

**Acceptance criteria:**
- FOV slider live-updates camera without restart.
- Motion reduction disables camera bob and converts synergy flash to overlay.
- Subtitles display for synergy and Unaccounted-equivalent audio (Unaccounted ships Phase 3; gate Phase 2 subtitles to existing audio events).

**Depends on:** Phase 1 task 9a (settings v1), Phase 1.5 task 51a (rebindable input)

---

### 84e. Template epilogue
**Layer:** Engine + Client
**Owner:** Backend lead + Frontend lead

**Subtasks:**
1. Template strings per design doc 11 with variable slots: `{mastermind_faction}`, `{succeeded|failed}`, `{scheme_name}`, `{town_name}`, `{saved|lost|abandoned}`, `{survived|suffered N losses|wiped}`, `{wildcard_faction}`, `{remained silent|offered alliance|became a lasting ally}`.
2. Variable resolution from action log: derive final faction states, settlement fates, death count, wildcard outcome.
3. Epilogue screen UI: title, 2–4 sentence template output, "Continue" / "View action log" buttons.
4. Always-available offline path (no LLM dependency).

**Acceptance criteria:**
- Campaign ending at turn 35 displays template epilogue within 1 second.
- Two different campaign outcomes produce visibly different epilogue text.
- Epilogue resolves 100% of variables; no `{placeholder}` survives to UI.

**Depends on:** Phase 1 task 31a (action log), Task 82 (turn counter + world state), Task 80 (mastermind discovery)

---

### 84f. Action log expansion (Phase 2 categories)
**Layer:** Engine
**Owner:** Backend lead

**Subtasks:**
1. Add categories per design doc 11: `roster` (recruited, branch_chosen, resurrected, benched), `narrative` (mastermind_accused, scheme_exposed, wild_card_alliance_accepted/refused), `downtime` (rest, train, craft, network, investigate, lay_low, tend_blooms).
2. Emit events from: roster ops (task 56/57), branch system (task 53), death/resurrection (task 65), wildcard trigger (task 83), evidence/mastermind (tasks 79/80), downtime (task 68).
3. Final event count budget per campaign: ~500 events / ~100KB.

**Acceptance criteria:**
- Recruiting a tavern character emits `character_recruited`.
- Resurrecting at Bone Clerk emits `character_resurrected` with stat-loss payload.
- Action log entries map 1:1 to template epilogue variables (no missing data).

**Depends on:** Phase 1.5 task 51e, Tasks 53, 56, 65, 68, 80, 83

---

## Content Volume & Pipeline

### Content Types

| Content Type | Count | Authoring Method | Effort |
|---|---|---|---|
| Class/branch abilities | ~160 | Spreadsheet → JSON | 3 weeks |
| Room segments | 60–80 | Hand-authored JSON | 4 weeks |
| Encounters | 30–40 | Spreadsheet → JSON | 2 weeks |
| Items | 50–60 | Spreadsheet → JSON | 2 weeks |
| NPCs (contacts + recruits) | 20–25 | Hand-authored JSON | 2 weeks |
| Synergies | 15–20 | Hand-authored JSON | 1 week |
| Rumors | 20–30 | Spreadsheet → JSON | 1 week |
| Documents (evidence) | 15–20 | Hand-authored JSON | 1 week |
| Schemes | 3 | Hand-authored JSON | 2 weeks |
| Complications | 3 | Hand-authored JSON | 1 week |

**Total content effort:** ~6 weeks with 2 authors (parallelized).

### Pipeline

```
Author writes JSON → Schema validator → Content pack compiler → Binary pack → Integration test
```

- Schema validator catches: missing fields, invalid ability IDs, dangling references.
- Content pack compiler produces `.rpk` files per content type.
- Integration test: load all packs → assert no deserialization errors → assert all referenced IDs resolve.

---

## Testing Strategy

| Level | Target | Tool | Count |
|---|---|---|---|
| Unit | Reputation math, evidence thresholds, turn counter, world state transitions | xUnit | 30 tests |
| Snapshot | Combat with all 8 classes, branch abilities, faction soldier AI, synergies | xUnit | 20 tests |
| Integration | Content pipeline: all JSON → binary → loaded state | xUnit | 5 tests |
| UI Smoke | Roster management, branch choice, town facilities, overworld travel, evidence submission | Playwright | 10 tests |
| Campaign | Full 35-turn campaign with 3 different six-roll configs | xUnit + manual | 3 automated + 2 manual |

---

## Dependency Graph

```
Phase 1.5 (complete)
  ├─► Group 10 (Classes)
  │     ├─► 52 (Marcher + Ashmouth content)
  │     ├─► 53 (Branch system engine)
  │     ├─► 54 (All branches content)
  │     │     └─► depends on 53
  │     ├─► 55 (Level cap 10)
  │     │     └─► depends on 53, 54
  │     ├─► 56 (Roster/bench)
  │     └─► 57 (Roster UI)
  │           └─► depends on 56
  ├─► Group 11 (Factions)
  │     ├─► 58 (All 5 factions content)
  │     ├─► 59 (Compact mechanics)
  │     │     └─► depends on 58
  │     ├─► 60 (Gated recruits)
  │     │     └─► depends on 58, 56
  │     ├─► 61 (Branch fallback)
  │     │     └─► depends on 53, 58
  │     └─► 62 (Rep impact)
  │           └─► depends on 58
  ├─► Group 12 (Combat)
  │     ├─► 63 (Synergy library)
  │     │     └─► depends on 54
  │     ├─► 64 (Faction soldier AI)
  │     │     └─► depends on 58, 52
  │     ├─► 65 (Death/resurrection)
  │     │     └─► depends on 56
  │     ├─► 66 (Component inventory)
  │     ├─► 67 (Component UI)
  │     │     └─► depends on 66
  │     ├─► 68 (Downtime)
  │     └─► 69 (Downtime UI)
  │           └─► depends on 68
  ├─► Group 13 (Overworld)
  │     ├─► 70 (Node graph)
  │     ├─► 71 (Map renderer)
  │     │     └─► depends on 70
  │     ├─► 72 (Travel encounters)
  │     │     └─► depends on 70, 52
  │     ├─► 73 (Route status)
  │     │     └─► depends on 70
  │     ├─► 74 (Town facilities)
  │     │     └─► depends on 58, 65, 68
  │     ├─► 75 (Rumor system)
  │     │     └─► depends on 74
  │     └─► 76 (Closing windows)
  │           └─► depends on 73, 75
  └─► Group 14 (Campaign)
        ├─► 77 (Six-roll system)
        ├─► 78 (Schemes + Complications)
        │     └─► depends on 77
        ├─► 79 (Evidence system)
        ├─► 80 (Mastermind discovery)
        │     └─► depends on 79
        ├─► 81 (4 dungeon templates)
        │     └─► depends on 78
        ├─► 82 (Turn counter + world state)
        │     └─► depends on 77
        ├─► 83 (Wild Card)
        │     └─► depends on 77, 82
        └─► 84 (Campaign tests)
              └─► depends on 77, 79, 82

Group 14.5 (Coverage)
  ├─► 84a (Hollow heat) depends on 52, 68, P1.5 38
  ├─► 84b (Tithe obligations) depends on 74, 58
  ├─► 84c (Secrets — full) depends on P1 25a, P1.5 51b, 52, 53, 54
  ├─► 84d (Display + a11y settings) depends on P1 9a, P1.5 51a
  ├─► 84e (Template epilogue) depends on P1 31a, 80, 82
  └─► 84f (Action log Phase 2) depends on P1.5 51e, 53, 56, 65, 68, 80, 83
```

**Parallelization:**
- Groups 10, 11, and 13 can start immediately after Phase 1.5.
- Group 12 needs Group 10 (branches for synergies) and Group 11 (factions for soldier AI).
- Group 14 integrates everything; starts only when Groups 10–13 are feature-complete.
- Content authoring for Groups 10, 11, 13, and 14 can happen in parallel with engineering.

---

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Content volume overwhelms pipeline | High | Critical | Spreadsheet pipeline for bulk content; hand-author only narrative content. Weekly validation passes catch drift early. |
| Branch system creates analysis paralysis | Medium | High | Limit to 2 choices at level 3, 2–3 at level 6. Provide ability previews. User test with 5 players. |
| 35-turn campaign feels too long | Medium | High | Internal playtest at week 8; if pacing is off, compress to 30 turns or reduce dungeon count. |
| Faction system feels like a spreadsheet | Medium | Medium | Expose rep changes with cause/effect UI. Ensure vendors sell genuinely distinct gear. |
| Evidence trail is too obscure | Medium | High | Inkblood Archivist explicitly flags suspicious factions at 3 evidence. Ashmouth Broker can confront at 5. |
| Roster management UI is clunky | Low | Medium | Iterate UI in week 9–10 based on playtest feedback. |

---

## Milestones

| Week | Milestone | Definition of Done |
|---|---|---|
| 3 | All classes playable | 8 classes in tavern, branch choices work, all abilities trigger |
| 5 | Factions matter | All 5 factions have vendors, missions, and rep consequences |
| 7 | Overworld alive | 4 towns, travel encounters, route status changes, rumor system |
| 9 | Strategic bind validated | Playtesters report agonizing over branch choices and faction alignment |
| 11 | Campaign complete | 35-turn campaign playable end-to-end with 3 different configs |
| 12 | Phase 2 lock | All snapshot tests pass, content validated, no P0 bugs |

---

## Appendix A — Campaign Config Schema (hand-authored Phase 2 example)

Schema becomes LLM output target in Phase 3 (task 94). Phase 2 ships 5–8 hand-authored configs covering all six-roll permutations needed for testing.

```json
{
  "campaignId": "p2-test-bureau-mastermind-bloom-harvest",
  "schemaVersion": 2,
  "rolls": {
    "patron": "bureau",
    "threat": "convocation",
    "mastermind": "bureau",
    "scheme": "bloom-harvest",
    "wildCard": "compact",
    "complication": "open-war"
  },
  "dungeonSequence": [
    { "templateId": "broken-engine", "instanceId": "act1-d1", "act": 1 },
    { "templateId": "bloom-site",   "instanceId": "act1-d2", "act": 1 },
    { "templateId": "underway",     "instanceId": "act2-conn", "act": 2 },
    { "templateId": "contested-ruin","instanceId": "act2-d1", "act": 2 },
    { "templateId": "bloom-site",   "instanceId": "act3-finale", "act": 3 }
  ],
  "dungeonAssignments": {
    "act1-d1": {
      "factionPresence": ["bureau"],
      "evidenceSlots": ["doc-bureau-inspection-7", "doc-bureau-roster-12"],
      "npcCasting": { "patron-contact": "npc-bureau-warden-marie" },
      "encounterEscalation": "act1"
    }
  },
  "townConfigurations": {
    "ashmark": { "factions": ["bureau","stillness"], "engineType": "broken", "specialVendor": null, "rumorPool": "ashmark-act1" }
  },
  "factionTimelines": {
    "bureau":      { "investigating": 1, "preparing": 12, "executing": 22, "scriptedEvents": ["bureau-roundup-t14"] },
    "convocation": { "investigating": 1, "preparing": 14, "executing": 24, "scriptedEvents": ["bloom-leak-t18"] }
  },
  "wildcardTrigger": { "factionId": "compact", "turnWindow": [18, 24], "repThreshold": 20 },
  "evidenceChain": [
    "doc-bureau-inspection-7", "doc-bureau-roster-12", "doc-bureau-finance-3",
    "doc-bureau-tithe-mismatch-1", "doc-bureau-bloom-purchase-5",
    "doc-bureau-cover-up-2", "doc-bureau-internal-memo-9",
    "doc-bureau-witness-statement-4", "doc-bureau-shipping-manifest-1",
    "doc-bureau-confession-1"
  ]
}
```

**Validation rules (per task 95 preview):**
- `evidenceChain.length >= 10`.
- No NPC appears in two `npcCasting` values.
- `factionTimelines[X].preparing < executing`.
- `wildcardTrigger.factionId` ∉ {`threat`, `mastermind`}.
- Every `evidenceSlots` doc exists in content index.

---

## Appendix B — Evidence Chain Authoring

Per design doc 03. Each chain places 10–15 documents leading to the Mastermind.

**Chain composition for Bureau-Mastermind / Bloom-Harvest example:**
| # | Doc ID | Act | Source dungeon | Evidence weight | Reveals |
|---|---|---|---|---|---|
| 1 | doc-bureau-inspection-7 | 1 | Broken Engine | 1 | Bureau presence at unauthorized site |
| 2 | doc-bureau-roster-12 | 1 | Broken Engine | 1 | Bureau personnel on bloom payroll |
| 3 | doc-bureau-finance-3 | 1 | Bloom Site | 1 | Bureau funding bloom containment failures |
| 4 | doc-bureau-tithe-mismatch-1 | 2 | Underway | 1 | Bone tithe diverted from Compact |
| 5 | doc-bureau-bloom-purchase-5 | 2 | Contested Ruin | 1 | Bureau buying bloom samples |
| 6 | doc-bureau-cover-up-2 | 2 | Contested Ruin | 2 | Internal Bureau memo destroying evidence |
| 7 | doc-bureau-internal-memo-9 | 2 | Bloom Site | 1 | Mastermind's name appears in BCC |
| 8 | doc-bureau-witness-statement-4 | 3 | Boneyard | 2 | First-hand survivor of Bureau hit |
| 9 | doc-bureau-shipping-manifest-1 | 3 | Sealed Vault | 1 | Bloom samples shipped to Bureau labs |
| 10 | doc-bureau-confession-1 | 3 | Finale | 3 | Direct admission |

**Threshold gates (per design doc 03 + task 79):**
- 3 evidence → Inkblood Archivist flags Bureau in Field Notes.
- 5 evidence → Ashmouth Broker dialogue: "Confront Bureau contact about [pattern]."
- 7 evidence → Patron Office "Accuse Bureau" option appears.
- 10 evidence → Finale dungeon unlocks; correct accusation path.

**Authoring rule:** documents 1..N may only reference events/places mentioned in documents 1..N-1. Build-time validator scans cross-references.

---

## Appendix C — Hollow Heat Gating

Heat exposure to player is indirect. Per task 84a.

| Heat | NPC dialogue change | Market change | Patrols | Bone Clerk |
|---|---|---|---|---|
| 0–20 | Normal | Normal | None | Normal |
| 21–40 | "You look tense, friend." | +10% prices | None | Normal |
| 41–60 | "Keep your head down here." | +15% prices, fewer rare items | Bureau random-search 25% chance | Normal |
| 61–80 | Contacts refuse meeting | +20% prices, no rare items | Bureau patrol on every visit | Reports player to authorities |
| 81–100 | Town hostile | Locked out | Lockdown gate | Hostile, refuses service |

**Heat reveal classes:** Inkblood Archivist (downtime Investigate) and Hollow Rumor branch can surface explicit heat reading. Otherwise hidden.

**Lockdown encounter (81+):**
- Bribe path: 500g → heat -30, leave normally.
- Combat path: 4-encounter combat vs town guard (faction soldier stat block), winning leaves town but flags `town_hostile: true` permanent for that town.

---

## Appendix D — Action Log Categories (Phase 2)

Adds to Phase 1.5 Appendix D. Schema unchanged.

| Category | New types |
|---|---|
| `roster` | `character_recruited`, `branch_chosen`, `character_resurrected`, `character_benched`, `character_dismissed` |
| `narrative` | `mastermind_accused`, `mastermind_accusation_outcome`, `scheme_exposed`, `wild_card_alliance_accepted`, `wild_card_alliance_refused`, `betrayal_committed` |
| `downtime` | `rest`, `train`, `craft`, `network`, `investigate`, `lay_low`, `tend_blooms` |
| `dungeon` | `settlement_fate_chosen` (now live), `evidence_found` (extended with chainStep) |
| `combat` | `enemy_negotiated` (Ashmouth path), `character_died` (extended with cause) |

**Epilogue-critical variables resolved from log:**
- `{mastermind_faction}` ← latest `mastermind_accusation_outcome.factionId`
- `{succeeded|failed}` ← `scheme_exposed` outcome
- `{town_name}` ← latest `settlement_fate_chosen.townId`
- `{saved|lost|abandoned}` ← `settlement_fate_chosen.outcome`
- `{N losses}` ← count of `character_died` where `payload.permanent = true`
- `{wildcard_faction}` ← `rolls.wildCard`
- Wildcard line ← presence/absence of `wild_card_alliance_accepted`

---

## Appendix E — Tithe Schedule

Per design doc 05 + task 84b.

| Act | Tithe due turn | Tokens owed (party of N) |
|---|---|---|
| 1 | turn 1 (or next town entry after) | ceil(N/3) |
| 2 | turn 15 | ceil(N/3) |
| 3 | turn 25 | ceil(N/3) |

**Source budget:**
- Compact missions: 1 token per completion (max 4 missions / campaign).
- Bloom containment: 1 token per containment site cleared (3–5 sites / campaign).
- Rare dungeon loot: 1 token per Compact-influenced dungeon (≈ 30% drop rate).
- Total expected: 6–10 tokens / campaign. Sufficient for party of 6 (6 tokens needed across 3 acts).

**Edge cases:**
- Bench characters do not count toward tithe size.
- Recruiting mid-act does not retroactively bill — next act's tithe sizes up.
- Resurrected characters count from resurrection forward.
