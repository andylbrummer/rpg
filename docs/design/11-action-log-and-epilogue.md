# Action Log & Epilogue

## Purpose

The player action log is a structured, append-only record of campaign events. It drives the epilogue (Phase 3) and enables analytics. It is **not** a free-text journal — it is a machine-readable event stream.

## Event Schema

Each event is a JSON object with a timestamp (campaign turn), category, and payload.

```json
{
  "turn": 7,
  "act": 1,
  "category": "combat",
  "type": "character_died",
  "payload": {
    "characterId": "char-stillblade-01",
    "class": "Stillblade",
    "branch": "Warden",
    "level": 4,
    "dungeon": "broken-engine-alpha",
    "encounter": "engine-guardian-2"
  }
}
```

### Categories

| Category | Types | Phase Introduced |
|---|---|---|
| `combat` | `encounter_started`, `encounter_won`, `encounter_fled`, `character_downed`, `character_died`, `synergy_triggered`, `enemy_negotiated` | 1 |
| `dungeon` | `dungeon_entered`, `dungeon_completed`, `evidence_found`, `secret_discovered`, `settlement_fate_chosen` | 1 |
| `faction` | `rep_changed`, `vendor_unlocked`, `mission_completed`, `mission_failed`, `betrayal_committed` | 1.5 |
| `roster` | `character_recruited`, `branch_chosen`, `character_resurrected`, `character_benched` | 2 |
| `overworld` | `travel_started`, `travel_encounter_resolved`, `route_blocked`, `town_reached` | 1.5 |
| `narrative` | `mastermind_accused`, `scheme_exposed`, `wild_card_alliance_accepted`, `wild_card_alliance_refused` | 2 |
| `downtime` | `rest`, `train`, `craft`, `network`, `investigate`, `lay_low`, `tend_blooms` | 2 |

## Logging Rules

1. **Server authoritative:** All events are emitted by the .NET engine, never the client.
2. **Append-only:** Events are never edited or deleted. If a state reverses (e.g., a failed mission is retried and succeeded), both events exist.
3. **Deterministic IDs:** Character IDs and encounter IDs are stable so the log references the same entities throughout.
4. **Privacy:** The log contains no free-text input from the player (no typed names, no chat). It is safe for telemetry if the player opts in.

## Log Storage

- **In-memory:** Active during gameplay. Reset on new campaign.
- **Save file:** Serialized alongside game state. The log is part of the save schema from Phase 1 onward.
- **Size budget:** ~500 events per campaign × ~200 bytes = ~100KB. Trivial.

## Epilogue Generation

### Phase 2: Template Epilogue

Pre-authored templates with variable slots. Always available, no LLM dependency.

```
The {mastermind_faction} {succeeded|failed} in their attempt to {scheme_name}.
{town_name} was {saved|lost|abandoned}.
Your party {survived|suffered N losses|was wiped out}.
{wildcard_faction} {remained silent|offered alliance|became a lasting ally}.
```

- Variables are populated directly from the action log (final faction states, settlement fates, death count).
- Output: 2–4 sentences. Functional but emotionally flat.

### Phase 3: LLM Epilogue

Structured prompt fed to the LLM with the full action log summary.

**Prompt structure:**
1. **Campaign summary** — Six rolls, final faction states, scheme outcome.
2. **Key event chronology** — Up to 20 significant events selected by weight (deaths, betrayals, alliance choices, scheme milestones).
3. **Character arcs** — Branch choices, deaths, resurrections, standout combat moments per surviving/dead character.
4. **Constraint:** "Write 2–3 paragraphs summarizing the campaign's consequences. Do not invent events not in the log. Do not contradict the template epilogue facts."

**Fallback:** If the LLM is unavailable or returns invalid output, display the Phase 2 template epilogue. Cache the LLM result locally so the player sees the enhanced epilogue on subsequent loads.

## Analytics Hook

The action log is the source of truth for analytics (Phase 3, task 113):

- Synergies discovered → count unique `synergy_triggered` events
- Faction combos → final reputation states per campaign
- Branches picked → `branch_chosen` events
- Campaign outcomes → `scheme_exposed`, `mastermind_accused` outcomes

Analytics payload is a derived summary of the log, not the raw log itself, to respect privacy.
