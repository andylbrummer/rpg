# Field Notes Journal — Design Spec
Date: 2026-05-10
Status: design — Phase 1.5 deliverable
Depends on: `docs/design/06-combat-system.md` §The Combination System, quest-mission spec, design-system spec
Scope: synergy discovery + journaling UI, lore catalog, rumor verification log, replayable mechanic. Companion to Phase 1.5 synergy engine.

## 1. Concept

Field Notes is the player's diegetic notebook. Records:
- Synergies discovered (mechanical pair + cryptic hint).
- Lore documents read.
- NPCs met.
- Rumors heard with truth status (Phase 2).
- Bestiary entries unlocked.

Read-anytime, modal accessed via `J` key (per settings-keybinds spec). Diegetic skin: parchment pages, ink notation, marginalia.

## 2. Entry types

### 2.1 Synergy

```
{
  id: "tithe_link_pyre",
  kind: "synergy",
  pair: { abilityA: "bone_link", abilityB: "pyre" },
  hint: "Bone carries fire well",
  effectDescription: "Linked enemies all take fire damage when one is burned.",
  firstSeen: ISO-8601,
  timesObserved: 4,
  replayBufferId: "uuid"          // for replay
}
```

### 2.2 Lore

```
{
  id: "doc-cascade-memo-1",
  kind: "lore",
  title: "Cascade Maintenance Memo",
  body: "...full text...",
  source: "Broken Engine — control room",
  tags: ["faction-bureau","engine"],
  firstRead: ISO-8601
}
```

### 2.3 Bestiary

```
{
  id: "bone_archer",
  kind: "bestiary",
  name: "Bone Archer",
  category: "Constructed",
  firstEncountered: ISO-8601,
  killsByParty: 5,
  knownAbilities: ["longbow","retreat"],  // reveal incrementally
  resistances: ["necrotic"],              // discovered through play
  weaknesses: [],                         // hidden until proven
  threatScore: "moderate"
}
```

Phase 1 Bestiary auto-fills with name+icon+kill count. Phase 1.5 reveals ability names on second observation. Phase 2 reveals resistances/weaknesses after threshold (e.g., dealing fire damage 3x reveals resistance).

### 2.4 NPC

```
{
  id: "npc-bureau-inspector",
  kind: "npc",
  name: "Inspector Harrow",
  faction: "Bureau",
  firstMet: ISO-8601,
  encounters: ["meeting-at-old-calder","interrogation-at-archives"],
  reputation: 0,
  dispositionNotes: ["Suspicious of party","Offered mission xyz"]
}
```

### 2.5 Rumor (Phase 2)

```
{
  id: "rumor-stillness-scouts",
  kind: "rumor",
  text: "The Stillness sent scouts to Whitepeak last week.",
  source: "tavern-keeper-old-calder",
  truth: "unknown" | "true" | "false" | "planted",
  verifiedAt: ISO-8601 | null
}
```

## 3. Data model

`src/engine/RPC.Engine/Journal/`:

```
Journal/
  JournalEntry.cs          // sealed record per kind
  JournalState.cs          // dictionaries keyed by id, save-persisted
  JournalRegistry.cs       // loads static entries from content (lore, bestiary base)
  JournalService.cs        // record observation events, advance reveal thresholds
```

`GameState`:

```csharp
public JournalState Journal { get; } = new();
```

Journal subscribes to events from `MissionEngine`, `CombatEngine`, `WorldEvents`:

```csharp
public void RecordSynergy(SynergyTrigger t);
public void RecordLore(string docId, string sourceContext);
public void RecordKill(string enemyId, int byMember);
public void RecordEncounter(string enemyId);
public void RecordAbilityObserved(string enemyId, string abilityId);
public void RecordResistance(string enemyId, string damageType);
public void RecordNpcMet(string npcId, string context);
public void RecordRumor(string rumorId, string sourceNpcId);
```

Each record is idempotent — calling with same id updates `lastSeen` and counters, doesn't duplicate.

## 4. Synergy engine hook

Phase 1.5 introduces synergy detection. Sketch (lives in `src/engine/RPC.Engine/Combat/Synergies/`):

- Authored synergy table: `content/synergies/*.json`:

```json
{
  "id": "tithe_link_pyre",
  "pair": ["bone_link","pyre"],
  "ordering": "any",   // "any" | "AB_only" (A must precede B)
  "scope": "round",    // both must resolve in same round
  "effect": {
    "kind": "spread_status",
    "status": "burning",
    "targets": "linked_by_a"
  },
  "hint": "Bone carries fire well"
}
```

- `SynergyEngine.AfterAbilityResolved(combatState, ability)` checks recent-round-resolved abilities, matches against table.
- On match, applies effect AND emits `event:fx synergy_triggered` AND calls `Journal.RecordSynergy(...)`.

Discovery flow (per doc 06):
1. Immediate — flash + sound on synergy trigger.
2. Field Notes — auto-recorded entry with cryptic hint.
3. Replayable — entry includes `replayBufferId` referencing a stored snippet of combat state changes around the synergy.

## 5. Replay buffer

Combat log is already structured (`CombatLogEntry[]`). For synergy replay:
- On trigger, capture last 3 rounds of log entries + current snapshot.
- Compressed JSON stored under `replays/{uuid}.json` (size budget 10 KB each).
- Up to 100 replay buffers per save; FIFO eviction beyond.

Replay viewer in journal: plays back the log entries in sequence with damage numbers + indicators reconstructed from `FxEvent` history.

## 6. UI

### 6.1 Layout (≥1024)

```
┌──────────────────────────────────────────────────────────────────────────┐
│ Field Notes                                                       [ × ] │
├────────────────────┬─────────────────────────────────────────────────────┤
│ Tabs               │ Detail                                              │
│ ────               │ ────                                                │
│ Synergies (4)      │ Bone carries fire well                              │
│ Lore     (12)      │ ─── tithe_link_pyre ───                             │
│ Bestiary (6)       │                                                     │
│ NPCs    (8)        │ When Bonewarden's Bone Link and Cauterist's         │
│ Rumors  (3)        │ Pyre resolve within the same round, every enemy     │
│                    │ tethered by Bone Link takes the same fire damage    │
│ Search [______]    │ as the primary Pyre target.                         │
│ Filter ▾           │                                                     │
│                    │ First observed: Day 4, Broken Engine, control room  │
│ ▣ Bone carries...  │ Times observed: 4                                   │
│ ▣ Strike from..    │                                                     │
│ ▢ ??? (hint only)  │ [ ▶ Replay first observation ]                      │
│                    │                                                     │
│ ▢ ??? (unseen)     │                                                     │
└────────────────────┴─────────────────────────────────────────────────────┘
```

Left rail = tab + entry list with status indicators:
- `▣ name` — discovered and named.
- `▣ "hint"` — discovered, hint shown but not labeled.
- `▢ ???` — heard rumor or NPC mention but unseen.
- `▢ (unseen)` — completely unknown — only shown for entries player has acquired a clue toward.

Right pane = entry detail.

### 6.2 Tablet portrait

Single column. Tabs at top, list below, tapping entry pushes detail sheet from bottom.

### 6.3 Hint linking

When player reads a lore document or hears a rumor that hints at an unknown synergy, that synergy's entry surfaces in Field Notes as `▢ ???` with the hint text shown. After discovery, the entry transitions to `▣` with full description.

Linking authored in synergy JSON:

```json
"hints": [
  { "source": "lore", "id": "doc-bone-tithe-fragment-3" },
  { "source": "rumor", "id": "rumor-bonefire-tales" },
  { "source": "npc", "id": "npc-old-cauterist", "node": "tales-of-the-war" }
]
```

When player reads doc-bone-tithe-fragment-3, Field Notes auto-creates the `▢ ???` for `tithe_link_pyre` and stores the hint.

### 6.4 Bestiary entry

Layout:

```
┌─Bone Archer──────────────────────────────────────────────┐
│ [portrait]                                               │
│ Category: Constructed                                    │
│ Threat: Moderate                                         │
│                                                          │
│ First encountered: Day 2, Broken Engine                  │
│ Defeated: 5 times                                        │
│                                                          │
│ Known abilities:                                         │
│  • Longbow (long range physical)                         │
│  • Retreat (repositioning)                               │
│                                                          │
│ Resistances:    [discover by play]                       │
│ Weaknesses:     [discover by play]                       │
│                                                          │
│ Notes:                                                   │
│  "Reanimated archers from the imperial pacification      │
│   campaigns. The bones remember the form, not the        │
│   reason."                                               │
└──────────────────────────────────────────────────────────┘
```

Resistance/Weakness slots reveal as observed (e.g., "Resistant to Necrotic — observed 3 times").

### 6.5 Indicators

- New entry badge on `J` key icon in TopBar.
- Section tab count badge (e.g., "Synergies (4)").
- Recent (last 24h play time) entries have a brass dot.
- Locked entries (▢ ???) show ink-dim styling.

### 6.6 Flash alerts

- New synergy discovered → centered modal (toast layer) shows entry title + hint + Continue. Distinct from generic toast — pauses game briefly.
- New lore added → corner toast "Added to Field Notes: {title}".
- Bestiary milestone (third kill of a type) → small inline toast "{enemy} resistance noted".

## 7. Save / load

`SaveData` adds `JournalStateData`:

```csharp
public Dictionary<string,SynergyEntryData> Synergies { get; set; }
public Dictionary<string,LoreEntryData> Lore { get; set; }
public Dictionary<string,BestiaryEntryData> Bestiary { get; set; }
public Dictionary<string,NpcEntryData> Npcs { get; set; }
public Dictionary<string,RumorEntryData> Rumors { get; set; }
public List<string> ReplayBufferIds { get; set; }
```

Replay buffers stored separately in `replays/` folder (atomic file writes, garbage-collected to 100 most recent).

Save version → `"4"` (after combat-state, inventory, missions).

## 8. Phase 1 vs 1.5 vs 2

Phase 1:
- Bestiary auto-fill on kill (name + count).
- Lore on document pickup (UI placeholder; documents authored Phase 1.5).
- No synergy engine yet — left empty.

Phase 1.5:
- Synergy engine + table of 5 synergies + Field Notes synergy tab functional.
- Replay buffers.
- Lore documents seeded in Broken Engine.

Phase 2:
- Rumors with truth states.
- Bestiary resistance/weakness reveal mechanics.
- Hint cross-linking (lore reveals synergy clues).
- NPC dispositions.

## 9. Tests

- xUnit: record observations idempotently; bestiary reveal threshold; replay buffer FIFO eviction.
- xUnit (Phase 1.5): synergy detection given ability resolution order; both orderings hit (any-order pairs).
- Playwright: trigger known synergy in combat → modal shows → open Field Notes → entry present → replay plays back.

## 10. Out of scope

- Sharing journal between saves.
- Exporting journal as standalone artifact.
- Player-authored notes (Phase 3 if pursued).
- Automatic tactic-suggestion based on bestiary weaknesses (Phase 3 — would undermine discovery).
