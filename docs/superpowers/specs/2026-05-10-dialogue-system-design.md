# NPC Dialogue System — Design Spec
Date: 2026-05-10
Status: design — Phase 2 deliverable; Phase 1.5 minimal subset (linear shopkeep text only)
Depends on: quest-mission spec; field-notes-journal spec; faction system (Phase 2 separate)
Scope: dialogue tree grammar, condition language, action effects, content authoring, runtime engine, UI. Authored content; no LLM generation (per `docs/design/09` design principle).

## 1. Grammar

Each NPC has a dialogue file `content/dialogue/<npc-id>.json`. Tree of nodes; each node is one beat of conversation.

```json
{
  "npcId": "npc-bureau-inspector",
  "name": "Inspector Harrow",
  "portrait": "harrow.webp",
  "startNode": "greet",
  "nodes": {
    "greet": {
      "kind": "say",
      "speaker": "npc",
      "text": "You're the lot the Patron sent. Sit down before you say something binding.",
      "conditions": [],
      "actions": [
        { "type": "set_flag", "flag": "met_harrow" },
        { "type": "journal_record", "kind": "npc", "id": "npc-bureau-inspector" }
      ],
      "choices": [
        { "label": "What do you need?", "next": "ask_brief", "conditions": [] },
        { "label": "We're listening.", "next": "ask_brief", "conditions": [] },
        { "label": "[Threaten] Make it quick.", "next": "threat_response",
          "conditions": [ { "type":"party_has_class", "class":"hollow" } ],
          "actions": [ { "type":"faction_rep_delta", "faction":"bureau", "delta":-3 } ] },
        { "label": "[Lie] We've been working the case.", "next": "lie_check",
          "conditions": [ { "type":"party_has_class", "class":"ashmouth", "branch":"liar" } ] }
      ]
    },
    "ask_brief": {
      "kind": "say", "speaker": "npc",
      "text": "Engine in Old Calder went dark three nights past. Find out why. Discreetly.",
      "actions": [ { "type":"start_mission", "missionId":"foothold-broken-engine" } ],
      "choices": [
        { "label": "Agreed.", "next": "end_friendly" },
        { "label": "What's the pay?", "next": "ask_pay" }
      ]
    },
    "ask_pay": {
      "kind": "say", "speaker": "npc",
      "text": "Three hundred crowns and a Bureau letter of safe passage. More if you don't make a mess.",
      "choices": [ { "label": "Agreed.", "next": "end_friendly" } ]
    },
    "threat_response": {
      "kind": "say", "speaker": "npc",
      "text": "I see. Walk carefully in this city — the Bureau remembers.",
      "choices": [ { "label": "(leave)", "next": "end" } ]
    },
    "lie_check": {
      "kind": "skill_check",
      "stat": "WIL",
      "dc": 14,
      "success": "lie_success",
      "failure": "lie_fail"
    },
    "lie_success": {
      "kind": "say", "speaker": "npc",
      "text": "Mm. Good. Then you won't mind a smaller bonus.",
      "actions": [ { "type":"reward", "gold":150 } ],
      "choices": [ { "label": "We'll take it.", "next": "ask_brief" } ]
    },
    "lie_fail": {
      "kind": "say", "speaker": "npc",
      "text": "You haven't. Don't insult me.",
      "actions": [ { "type":"faction_rep_delta", "faction":"bureau", "delta":-5 } ],
      "choices": [ { "label": "(leave)", "next": "end" } ]
    },
    "end_friendly": { "kind":"end", "actions":[ { "type":"faction_rep_delta", "faction":"bureau", "delta":2 } ] },
    "end": { "kind":"end" }
  }
}
```

### 1.1 Node kinds

| Kind | Meaning |
|---|---|
| `say` | speaker line + optional choices |
| `skill_check` | branches on stat roll |
| `branch` | branches on condition without dialogue (silent router) |
| `give` | gives item / gold without text (rare; usually part of `actions`) |
| `combat` | triggers combat with specified encounter; resolution branches |
| `end` | closes dialogue, applies actions |

### 1.2 Speaker

`npc` (default), `player` (first-person from selected party speaker), `narrator` (italic descriptive), or specific `member:<characterId>` to attribute to a party member.

### 1.3 Choices

Each choice:

```json
{
  "label": "string (player-facing)",
  "next": "nodeId",
  "conditions": [ /* shown only if all true */ ],
  "lockedConditions": [ /* shown but disabled if any false */ ],
  "actions": [ /* applied on selection */ ],
  "tags": ["intimidate","lie","persuade"]
}
```

`tags` purely for content categorization + UI iconography (intimidate = ⚔, lie = 🎭, persuade = ✋).

## 2. Conditions

Declarative predicates. Engine evaluates against current `GameState` + party.

| Type | Params | True when |
|---|---|---|
| `flag_set` | `flag` | `WorldFlags[flag]` set |
| `flag_not_set` | `flag` | not set |
| `mission_status` | `missionId, status` | mission in status |
| `mission_outcome` | `missionId, outcome` | mission ended with outcome |
| `party_has_class` | `class, branch?` | any party member matches |
| `party_avg_level` | `op, n` | comparator |
| `party_member_alive` | `characterId` | named member alive |
| `gold` | `op, n` | gold compare |
| `has_item` | `itemId, qty?` | aggregated across zones |
| `faction_rep` | `faction, op, n` | rep compare |
| `world_turn` | `op, n` | turn compare |
| `random` | `chance` | rng roll |
| `npc_disposition` | `npcId, op, n` | per-npc track |
| `and` | `conditions[]` | all true |
| `or` | `conditions[]` | any true |
| `not` | `condition` | invert |

Phase 1.5 subset: `flag_set`, `flag_not_set`, `mission_status`, `gold`, `has_item`. Phase 2: rest.

## 3. Actions

Side effects applied when entering a node or selecting a choice.

| Type | Params | Effect |
|---|---|---|
| `set_flag` | `flag, value?` | WorldFlags update |
| `clear_flag` | `flag` | |
| `start_mission` | `missionId` | accepts mission |
| `complete_mission` | `missionId, outcome` | force-complete |
| `reward` | `gold?, xp?, items[]?` | |
| `faction_rep_delta` | `faction, delta` | |
| `npc_disposition_delta` | `npcId, delta` | |
| `journal_record` | `kind, id` | adds Field Notes entry |
| `give_item` | `itemId, qty` | into Cache then Backpack overflow |
| `take_item` | `itemId, qty` | from any zone; fails if not present (node won't run) |
| `take_gold` | `amount` | |
| `trigger_combat` | `encounterId, onWin, onLose` | leaves dialogue, returns to specified nodes |
| `play_audio` | `cueKey` | one-shot |
| `set_music` | `cueKey` | crossfade |
| `screen_effect` | `effect` | one of `flash`,`shake`,`vignette_red` |
| `set_npc_var` | `npcId, key, value` | per-NPC scratchpad |
| `goto` | `nodeId` | force jump (used for end fallthrough) |

Actions on a node run BEFORE its text renders. Actions on a choice run AFTER the player selects, BEFORE moving to `next`.

## 4. Skill check

```json
{ "kind":"skill_check", "stat":"WIL", "dc":14,
  "speaker":"narrator",
  "text":"You weigh your words. (WIL vs DC 14)",
  "success":"node_id", "failure":"node_id",
  "modifiers":[
    { "when":{"type":"party_has_class","class":"ashmouth","branch":"liar"}, "delta":+5 },
    { "when":{"type":"faction_rep","faction":"bureau","op":">=","n":20}, "delta":+2 }
  ]
}
```

Roll: `d20 + (selectedMember.Stats[stat] - 10)/2 + Σ modifiers`. Result vs DC.

Member selection: dialogue chooses based on highest stat (auto), or player picks from `choices` of type `pick_speaker` (Phase 2).

## 5. Engine

`src/engine/RPC.Engine/Dialogue/`:

```
Dialogue/
  DialogueDef.cs          // immutable, from content
  DialogueState.cs        // current node, history, npc vars
  DialogueRegistry.cs     // all NPC dialogues
  DialogueEngine.cs       // step + select choice + evaluate conditions/actions
  ConditionEvaluator.cs   // generic predicate eval
  ActionApplier.cs        // dispatcher
```

`GameState`:

```csharp
public DialogueState? ActiveDialogue { get; private set; }
public Dictionary<string,Dictionary<string,object>> NpcVars { get; } = new();
public Dictionary<string,int> NpcDispositions { get; } = new();

public void StartDialogue(string npcId);
public void SelectChoice(int choiceIndex);
public void EndDialogue();
```

`Mode.Dialog` already in enum.

## 6. Server actions

| Action | Payload | Effect |
|---|---|---|
| `dialog_start` | `{npcId}` | enter dialog mode at NPC start node |
| `dialog_choice` | `{choiceIndex}` | select choice |
| `dialog_close` | `{}` | force-exit (some nodes disallow — sticky encounters) |

## 7. UI

Modal at overlay layer (z=200, not modal — replaces world view in Dialog mode).

```
┌──────────────────────────────────────────────────────────────────────────┐
│ Inspector Harrow                                  [Bureau]               │
│ ───────────────────────────────                                          │
│ [portrait 256×256]    "You're the lot the Patron sent. Sit down          │
│                       before you say something binding."                  │
│                                                                          │
│                                                                          │
│ ── Choices ──                                                            │
│ 1. What do you need?                                                     │
│ 2. We're listening.                                                      │
│ 3. ⚔ [Threaten] Make it quick.        (Hollow only)                      │
│ 4. 🎭 [Lie] We've been working the case. (Liar branch)                   │
│ 5. (leave)                                                               │
└──────────────────────────────────────────────────────────────────────────┘
```

Layout:
- Top: NPC name + faction badge.
- Center: portrait left, text right (justified body font, italic for narrator).
- Bottom: numbered choices, hover/focus = brass underline.
- Hotkeys: 1–9 select choice; ESC = "leave" if available.
- Tablet portrait: portrait shrinks, text wraps full-width, choices stack.

Choice icons by tag (intimidate ⚔, lie 🎭, persuade ✋, [knowledge] 📜, [class] colored badge).

Skill-check resolution: brief inline reveal `(WIL: 16 + 5 = 21 vs DC 14 — success)`, then auto-advance to success/failure node.

## 8. Content authoring

Single JSON per NPC. Content-pack compiler validates:
- All `next` references exist within file.
- All actions reference real flags/missions/items/encounters.
- All conditions reference real classes/items/flags.
- No orphan nodes (unreachable from startNode).
- No dead-ends without `end` kind.
- Skill-check stat is a valid stat key.

Authoring tools (Phase 2): emit dialogue graph as Graphviz `.dot` for review; CI uploads as PR artifact.

## 9. Save / load

`SaveData`:

```csharp
public Dictionary<string,object> NpcVars { get; set; }
public Dictionary<string,int> NpcDispositions { get; set; }
public DialogueResumeData? ActiveDialogue { get; set; }   // saving mid-dialogue
```

Mid-dialogue save allowed only in town (per `gameplay.autoSave=town`). Mid-combat-dialogue (encounter dialogue) excluded.

Save version → `"5"` after journal v4.

## 10. Phase 1.5 minimum

Phase 1.5 ships only:
- `say` + `end` node kinds.
- `flag_set/not_set`, `mission_status`, `gold`, `has_item` conditions.
- `set_flag`, `start_mission`, `reward`, `take_gold`, `give_item`, `goto` actions.
- Linear shopkeep dialogues (~6 NPCs).

Phase 2 adds skill checks, combat triggers, faction rep deltas, NPC vars, branch conditions.

## 11. Tests

- xUnit: condition evaluator over fixture state matrix.
- xUnit: action applier idempotency (same action twice ≠ doubled effect unless declared additive like rep delta).
- xUnit: full traversal of authored Phase 1.5 dialogues — every reachable terminal.
- Playwright: open dialogue, choose path, verify mission accepted + reward applied.

## 12. Out of scope

- Procedurally generated dialogue (`docs/design/09` explicitly forbids LLM prose; LLM only arranges).
- Voice acting.
- Lip sync / portrait animation.
- Branching cinematic camera.
- Real-time dialogue with typing animation Phase 1.5 (cosmetic toggle Phase 2).
