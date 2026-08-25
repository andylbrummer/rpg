# Tutorial & Onboarding — Design Spec
Date: 2026-05-10
Status: design — Phase 1.5 deliverable
Depends on: dialogue spec, quest-mission spec, screens spec, design-system spec
Scope: first-time player guidance without violating doc 01's "no quest markers, no hand-holding" pillar. Diegetic instruction, optional hints, contextual reveals.

## 1. Constraints from design

`docs/design/01-vision.md` pillar #4: "Earned discovery. No quest markers or hand-holding. The world communicates through environmental storytelling…"

This rules out:
- Floating arrows / waypoints.
- Tutorial NPC saying "Press W to move."
- Modal popups blocking gameplay.
- Highlight-and-explain UI elements.

Acceptable:
- First-time-only contextual nudges (small, dismissable, never blocking).
- Diegetic hints integrated into world (NPC dialogue, environment).
- Settings → Help reference (player-opted).
- Field Notes "How To" entries that read like in-fiction journal pages.

## 2. Layers

Three layers, each toggleable independently:

### 2.1 Diegetic onboarding (always on)

Built into first dungeon + first town encounter. Not a "tutorial mode" — just the start of the game written to teach by example.

First mission (`foothold-broken-engine`, per quest spec §5):
- Inspector Harrow dialogue at start explains stakes (briefing).
- First corridor is straight, no branches → teach move.
- First junction has a clear path forward + visible secret door (teach exploration).
- First encounter is one rat (easy) → teach combat UI without pressure.
- After combat: brief NPC line via radio-stone item picked up: "Mind your bone fragments — the dead don't pay for themselves."
- Mid-dungeon: a Cauterist NPC encounter teaches healing (offers consumable + line about applying it).
- End: Engine console interaction teaches branching choices.

This is content authoring (covered in dialogue + mission specs), not engine work. Listed here for cross-reference.

### 2.2 Contextual nudges (default on, toggleable)

Small, transient prompts that appear once per fresh save when the player encounters a new system. Disappear after 6 seconds or any interaction. Never block input.

```
┌──────────────────────────────────────────────┐
│  ⓘ  Press W or ↑ to step forward             │
└──────────────────────────────────────────────┘
```

Positioned bottom-center, ghost-style (semitransparent), brass underline.

Triggers (per fresh save, suppressed if dismissed):

| Trigger | Nudge |
|---|---|
| First exploration tile | "W / ↑ to step forward. A D / ← → to turn." |
| Stand at junction first time | "Some passages aren't obvious. Try moving where walls look thin." |
| First encounter starts | "Initiative bar shows turn order. Pick an action when it's your turn." |
| First combat hit landed | "Status effects appear under each character's name." |
| First combat won | "Resources don't refill between fights. Carry wisely." |
| First level-up | "Level-up resolves in town. Check your character sheet." |
| First town visit | "Press I for inventory. Press J for Field Notes." |
| First downed teammate | "Cauterist can stabilize a downed ally. Hurry." |
| First mission completed | "Check the Mission Board for what's next." |

Implementation:

```ts
NudgeBus.maybeShow('first_combat_hit_landed', "Status effects appear under each character's name.");
```

Internal: per-save flag `nudge.<id>.shown = true`. Once true, never re-shows. Settings → Accessibility → "Reset nudges" clears all flags.

### 2.3 Help reference (always available, never auto-shown)

Settings → Help opens a structured reference, written in-fiction-flavor but practically accurate:

```
┌─Help──────────────────────────────────────────┐
│ Categories:                                   │
│  • Movement                                   │
│  • Combat                                     │
│  • Inventory                                  │
│  • Party                                      │
│  • Field Notes                                │
│  • Settings & Keybinds                        │
│                                               │
│ Selected: Combat                              │
│ ─────                                         │
│ "Initiative determines turn order each round. │
│  Front-row characters take melee hits before  │
│  back-row. Status effects stack until cleared │
│  or expired. Bone fragments and cautery       │
│  supplies don't refill between encounters."   │
│                                               │
│  Actions:                                     │
│   Attack    — basic strike, scaled by STR     │
│   Defend    — +2 DEF this round               │
│   Cast      — use an ability                  │
│   Use Item  — consumable                      │
│   Flee      — exit combat if all alive        │
│   Wait      — yield turn                      │
│                                               │
│  Keybinds: shown in Settings → Keybinds       │
└───────────────────────────────────────────────┘
```

Written by a designer; lives in `content/help/` as JSON loaded into the modal. Each entry: title, body, related-keys, related-content-id (for "see also" links). Localized via i18n spec.

## 3. Field Notes "How To" entries

Phase 1.5 Field Notes (per journal spec) has a "How To" tab visible only if any nudges have ever fired. Contents = collected nudges + diegetic flavor:

```
─── How To: Read the Battlefield ───
"Watch the initiative line above. Even the dead — bone-bound or otherwise —
follow the rhythm. Break the rhythm and you break the line."

— Marcher's field manual, anonymous
```

This converts transient nudges into a permanent reference, but only after the player has seen them in play.

## 4. Disclaimers + opt-out

Settings → Accessibility section:

```
☑ Show contextual hints (recommended for first run)
☐ Show keybind reminders in HUD
☐ Pause game on hint
```

Default first install: hints ON, HUD reminders OFF, pause OFF.

Subsequent saves on same install default to the user's settings — installing the game once and starting many campaigns doesn't keep tutorializing.

## 5. First-run detection

Tracked separately from per-save flags:

`{appData}/rpc/profile.kdl`:

```kdl
firstLaunch "2026-05-10T22:00:00Z"
campaignsStarted 1
totalHoursPlayed 0
hintsCompleted 6
hintsDismissedEarly 1
```

After 3 campaigns OR > 5 hours OR hints completed, future fresh saves default hints OFF without prompting. Settings still respects manual toggle.

## 6. Diegetic anti-pattern guardrails

Forbidden in any onboarding:
- Floating arrows pointing to UI elements.
- Pause + overlay tutorial cards.
- "Press X to continue" forcing sequence.
- Generic "good job!" toasts.
- Tutorial NPCs that exist solely to be tutorial.
- Anything that breaks the fourth wall.

Allowed:
- One-line nudge bottom-center (§2.2).
- Diegetic in-character dialogue.
- Help reference (player-initiated).
- Lore items that happen to teach (a tutor's journal found in a dungeon).

## 7. New-Game Plus considerations

Phase 3: after first campaign completion, future campaigns suppress all nudges automatically. New-Game Plus assumes mastery.

## 8. Content authoring

`content/help/` — JSON entries:

```json
{
  "id": "help-combat",
  "title": "Combat",
  "category": "core",
  "body": "Initiative determines...",
  "actions": [
    { "id":"attack", "name":"Attack", "desc":"basic strike scaled by STR" },
    ...
  ],
  "relatedKeys": ["combatActionAttack","combatActionDefend"],
  "relatedContent": ["class-bonewarden","ability-bone-spear"]
}
```

Nudges defined in `content/nudges/` as a registry:

```json
{
  "id": "first_combat_hit_landed",
  "trigger": { "kind":"engine_event", "event":"combat_hit", "condition":"first_per_save" },
  "text": "Status effects appear under each character's name."
}
```

Engine emits events; nudge service matches them. Decoupled from individual game systems.

## 9. Implementation

`src/client/src/onboarding/`:

```
onboarding/
  NudgeBus.ts          // matches engine events to nudge defs, manages dedup
  NudgeLayer.svelte    // renders ghost prompt
  HelpModal.svelte     // help reference
  ProfileStore.ts      // firstLaunch + dismissed counts
```

`NudgeBus` subscribes to engine events forwarded over WS or emitted client-side. Single-instance Svelte store.

## 10. Tests

- Unit: nudge fires once, then never again on same save.
- Unit: settings → reset nudges → flags cleared, future events re-fire.
- Playwright: fresh save → take first step → nudge appears → wait 6s → nudge fades.
- Playwright: open help modal → navigate categories → close.

## 11. Out of scope

- Skippable cutscenes (no cinematics).
- Companion AI hints ("Sera says: try defending!"). Diegetic only via authored NPC dialogue.
- Difficulty-tied tutorials.
- Mid-campaign tutorials for late-game mechanics (synergies = Field Notes hints per journal spec).
- Voice-acted tutorial.
