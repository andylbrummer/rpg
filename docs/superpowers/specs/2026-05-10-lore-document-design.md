# Lore Documents — Design Spec
Date: 2026-05-10
Status: design — Phase 1.5 deliverable; Phase 1 stub via Field Notes
Depends on: field-notes-journal, content-pipeline, dialogue-system, dungeon-assembly (placement)
Scope: lore document schema, in-fiction presentation, discovery mechanics, evidence chain integration, citation, persistence.

## 1. Concept

Lore documents = in-fiction artifacts (memos, journals, ledgers, letters, inscriptions, faction directives, fragments). Players read to:

- Learn world context (`docs/design/02`).
- Discover synergy hints (per `docs/design/06`).
- Build evidence chain (Phase 3 LLM-arranged campaigns, `docs/design/09`).
- Earn XP (per progression spec discovery grant).

Diegetic: appears as a physical object the player picks up. Never modal "Game Hint" — always presented as the artifact itself.

## 2. Schema

`content/documents/<id>.json`:

```json
{
  "id": "doc-cascade-memo-1",
  "title": "Cascade Maintenance Memo, Old Calder Office",
  "author": "Inspector Harrow",
  "kind": "memo",
  "factionTag": "bureau",
  "tags": ["faction-evidence-slot","scheme-cascade-failure"],
  "redactions": [
    { "from":42, "to":58, "replacedWith":"████████████████" }
  ],
  "body": "...full text with optional [[link:another-doc-id]] inline references...",
  "footerLines": ["Filed: 4.21.Y14","Cross-ref: 9-A"],
  "evidenceWeight": 3,
  "synergyHint": null,
  "xpReward": 25,
  "rarity": "uncommon"
}
```

### 2.1 Kinds

| Kind | Visual presentation |
|---|---|
| `memo` | typewritten parchment, blocky font |
| `journal` | handwritten cursive on stained paper |
| `letter` | folded paper with seal/wax |
| `inscription` | stone/metal etched, blocky uppercase |
| `ledger` | tabular, ruled lines |
| `fragment` | torn page, edges ragged, partial text |
| `directive` | official seal at top, formal language |
| `notice` | poster-style, large headline |

UI renders different fonts + textures per kind.

### 2.2 Redactions

Some documents have redacted sections. `redactions[]` specifies character ranges replaced. Phase 2: certain class abilities (Inkblood Archivist) can unredact — server emits unredacted version.

### 2.3 Inline references

`[[link:doc-id]]` syntax renders as clickable text in viewer. Clicking jumps to that document if known; if unknown, marks as "referenced — find this document".

`[[npc:npc-id]]`, `[[mission:mission-id]]`, `[[faction:id]]` also supported. Click → opens corresponding journal entry.

### 2.4 Evidence weight

Per `docs/design/09` LLM spec: evidence threshold for Mastermind reveal = 10. Each evidence-tagged document has a weight contributing to total. Phase 2: evidence chain tracked.

## 3. Discovery

Documents placed in dungeons via segment data:

```json
{
  "id": "segment-broken-engine-records-A",
  "loot": {
    "fixed": [ { "kind":"document", "documentId":"doc-cascade-memo-1" } ]
  }
}
```

Or in encounter loot tables:

```json
{
  "id": "encounter-stillness-scholar",
  "loot": [
    { "kind":"document", "documentId":"doc-stillness-research-3", "chance":0.5 }
  ]
}
```

Or via dialogue:

```json
{ "type":"give_item", "itemId":"doc-bone-tithe-fragment-3" }
```

(Documents are special items per inventory spec — `ItemKind.Quest`, locked, take up backpack slot.)

## 4. Reading flow

On pickup: toast "Document found: {title}" (toast layer). Document enters character's backpack.

In Inventory → Quest tab, document items show with kind icon. Click → opens viewer modal.

Reading viewer:

```
┌─Cascade Maintenance Memo, Old Calder Office────────────────────┐
│                                                                │
│       From the desk of Inspector Harrow                        │
│                                                                │
│       Engine 7-A flickered three times this week.              │
│       The maintenance cycle says we've another                 │
│       hundred years. The maintenance cycle is                  │
│       ████████████████ written by people who did               │
│       not live through the last cascade.                       │
│                                                                │
│       Cross-ref: 9-A. See also [[doc:bureau-orders-3]].        │
│                                                                │
│                              Filed: 4.21.Y14                   │
│                                                                │
│  [Close]                            [Add to Journal]           │
└────────────────────────────────────────────────────────────────┘
```

Style by kind. Font swap, parchment texture, redaction blocks rendered as solid bars.

"Add to Journal" auto-fires on first read; button later opens Field Notes entry directly.

## 5. Effects on read

First time reading document:
- `Journal.RecordLore(documentId, context)` (per field-notes-journal spec §3).
- Award `xpReward` to currently selected character.
- Trigger `JournalSynergyHintAdded` if `synergyHint` field references a synergy with `▢ ???` state.
- Apply `worldEffects[]` if present (e.g., set flag, advance mission stage).
- Adjust faction rep if `factionRepDelta` present.

Subsequent reads: no XP, but always viewable from Journal.

## 6. Evidence chain

For `evidenceWeight > 0` documents:

`MissionState` tracks:

```csharp
public Dictionary<string,int> EvidenceCollected { get; }   // chainId → accumulated weight
public Dictionary<string,List<string>> EvidenceDocsByChain { get; }
```

Documents tagged with a chain (Phase 2 via `evidenceChain` field) contribute weight when read. When threshold met:
- Mastermind revealed (faction-system spec): triggers mission outcome.
- Journal shows "Conclusion reached: {Mastermind}".

Phase 1: stub — `evidenceWeight` recorded but no thresholds reached.

## 7. UI

### 7.1 Document viewer

Modal at modal-layer (per design-system spec). Per-kind styling:

| Kind | Background | Font | Texture |
|---|---|---|---|
| memo | parchment-warm | typewriter | aged paper |
| journal | parchment-cream | italic body | stained edges |
| letter | folded paper | mixed | wax seal corner |
| inscription | dark stone | uppercase blocky | engraved depth |
| ledger | ruled paper | mono numbers | columns |
| fragment | torn paper | partial | ragged borders, gaps |
| directive | white parchment | formal serif | gold-leaf header |
| notice | poster paper | bold display | dramatic |

CSS theme tokens already in design-system spec. Per-kind override classes apply.

### 7.2 Field Notes — Lore tab

Per journal spec §6.1. List of read documents grouped by tag. Selection opens viewer modal in read-only "from journal" mode (no XP grant, no first-read effects).

### 7.3 Indicators

- Pickup: brass burst around inventory slot.
- Cross-reference: linked documents in viewer underline brass; unread ones with `▢` icon.
- Redaction toggle: small lock icon next to redactions; if unlockable via class ability, brass tooltip.

### 7.4 Flash alerts

- New document → toast: "Document found: {title} ({xpReward} XP)".
- Evidence threshold reached → centered modal: "Pieces fit together — {Mastermind} revealed."
- Synergy hint linked → toast: "A connection comes to mind…" + Field Notes badge.

## 8. Engine

`src/engine/RPC.Engine/Lore/`:

```
Lore/
  DocumentDef.cs            // from content
  DocumentRegistry.cs
  DocumentReader.cs         // on-read effects: journal, xp, hints, evidence
```

Wired to inventory: when an `inv_use` action targets a Document item, opens read flow instead of consuming.

## 9. Persistence

Read state in `JournalState.Lore` (per journal spec). Per-document: first-read turn, source context, evidence-chain progress.

Save version: covered by journal v4 + missions v3.

## 10. Phase rollout

Phase 1: 6 hand-authored lore documents per dungeon (3 dungeons × 6 = 18 total). Basic viewer styling. No evidence chain.

Phase 1.5: synergy hints linked. Class-specific unredact (Inkblood Archivist).

Phase 2: evidence chains, Mastermind reveal mechanic, faction-tagged redactions.

Phase 3: LLM arrangement places documents in dungeons based on campaign rolls.

## 11. Tests

- xUnit: first-read fires effects; subsequent reads do not.
- xUnit: redaction rendering with character range correct.
- xUnit: cross-reference link resolution.
- xUnit: evidence weight accumulates; threshold fires once.
- Playwright: pick up document → open inventory → read → close → Field Notes shows entry.

## 12. Out of scope

- Player-authored documents.
- Document forgery / falsification mechanics (Hollow Rumor branch may add Phase 3).
- OCR / image-based documents.
- Voice narration.
