# Save Format — Design Spec
Date: 2026-05-10
Status: design — consolidates `SaveData` versions touched by other 2026-05-10 specs into single normative source
Depends on: all 2026-05-10 specs that touch persisted state
Scope: file layout, schema versions, migration chain, atomicity, multi-slot, autosave. Single authoritative reference; other specs declare their additions; this spec is the integrator.

## 1. File layout

Per-user data root: `{appData}/rpc/` (Linux `${XDG_CONFIG_HOME:-~/.config}/rpc`; macOS `~/Library/Application Support/rpc`; Windows `%APPDATA%\rpc`).

```
{appData}/rpc/
├─ settings.kdl                        (per settings-keybinds spec)
├─ window.kdl                          (per photino-lifecycle spec)
├─ profile.kdl                         (per onboarding spec)
├─ saves/
│  ├─ manifest.json                    list of save slots + metadata
│  ├─ slot-1/
│  │  ├─ save.rpcsave                  main save (current)
│  │  ├─ save.rpcsave.bak              previous save
│  │  ├─ actions.jsonl                 action log (determinism spec)
│  │  ├─ replays/                      synergy replay buffers (journal spec)
│  │  └─ _autosave/
│  │     ├─ latest.rpcsave
│  │     ├─ pre_combat.rpcsave
│  │     └─ rotate-{1..4}.rpcsave
│  ├─ slot-2/...
│  └─ _crash/
│     ├─ 2026-05-10T22-00-00.rpcsave   (per error-recovery spec)
│     └─ ...
├─ mods/                                (Phase 3, per modding spec)
├─ telemetry/                           (per telemetry spec)
├─ crash-reports/                       (per error-recovery spec)
└─ logs/                                (per error-recovery spec)
```

## 2. Save file format

`.rpcsave` is gzipped JSON with magic header:

```
[8 bytes magic "RPCSAV01"]
[8 bytes uncompressed length]
[gzip-compressed JSON body]
```

Magic + length let loader fail fast on truncation. Compression knocks typical 200 KB JSON to ~30 KB.

Body schema (top-level):

```json
{
  "version": "8",
  "gameVersion": "0.4.0",
  "contentHash": "abc123",
  "savedAt": "ISO-8601",
  "campaignId": "uuid",
  "campaignTurn": 7,
  "campaignName": "Old Calder Run",
  "saveSlot": 1,
  "isAutosave": false,
  "isManualSave": true,

  "rootSeed": 12345,

  "gameState": { /* see §3 */ },

  "activeMods": [
    { "id":"base", "version":"0.4.0" }
  ],

  "metadata": {
    "playtimeMs": 5400000,
    "partyAvgLevel": 2,
    "currentLocation": "town:old-calder"
  }
}
```

`metadata` not used by engine; powers Save Slot picker preview.

## 3. GameState schema by version

Cumulative — each version adds the fields shown:

### v1 (Phase 1 base, pre-2026-05-10)

```json
{
  "player": { "position": {"x":32,"y":32}, "facing":"N" },
  "currentDungeon": { /* tiles, dimensions */ } | null,
  "currentDungeonType": "broken_engine" | null,
  "mode": "Menu",
  "party": [
    {
      "id":"guid","name":"Kael","classId":"bonewarden","level":1,"xp":0,
      "stats": { "str":4,"dex":3,"con":5,"int":4,"wil":4 },
      "currentHp":17,
      "equipment": { /* slots */ },
      "abilities":["bone_spear","tithe_touch"],
      "row":0
    }, ...
  ],
  "exploredTiles": ["x,y", ...]
}
```

### v2 (inventory-model + combat-state-extension)

Adds per character:

```json
"backpack": { "slots": [ItemStack | null × 8] },
"lifeState": "Healthy",
"resurrectionAttempts": 0,
"statPenalties": {},
"droppedBackpack": null,
"deathTile": null,
"bodyLost": false
```

And party-level:

```json
"cache": { "slots": [ItemStack | null × 12] },
"storage": [ItemStack | null × N],
"gold": 0,
"titheTokens": 0,
"worldTurnInventory": 0
```

### v3 (quest-mission)

Adds top-level:

```json
"missions": {
  "active": { /* missionId → MissionRuntime */ },
  "history": [ /* CompletedMission */ ]
},
"worldFlags": {},
"worldTurn": 0
```

### v4 (field-notes-journal)

Adds:

```json
"journal": {
  "synergies": {},
  "lore": {},
  "bestiary": {},
  "npcs": {},
  "rumors": {}
},
"replayBufferIds": []
```

### v5 (dialogue-system)

Adds:

```json
"npcVars": {},
"npcDispositions": {},
"activeDialogue": null
```

### v6 (world-clock)

Adds:

```json
"turnHistory": [ /* TurnEvent */ ],
"townVisitId": 0,
"lastDowntimeVisitId": {}
```

### v7 (determinism — already partially in earlier; formalize)

Adds:

```json
"rngTreeRoot": "ulong",
"stateHash": "hex"     // hash of state at save time, validated on load
```

### v8 (faction-system)

Adds:

```json
"factions": {
  "reputation": {},
  "roles": {},
  "phases": {},
  "firedEvents": {},
  "relationsTo": {}
},
"repHistory": [],
"campaignRolls": {
  "patron": "convocation",
  "threat": "stillness",
  "mastermind": "bureau",
  "scheme": "cascade-failure",
  "wildCard": "cartography",
  "complication": "bloom-siege"
}
```

## 4. Migration chain

`src/engine/RPC.Engine/Save/Migrations/`:

```
Migrations/
  Migration_v1_to_v2.cs
  Migration_v2_to_v3.cs
  Migration_v3_to_v4.cs
  Migration_v4_to_v5.cs
  Migration_v5_to_v6.cs
  Migration_v6_to_v7.cs
  Migration_v7_to_v8.cs
  MigrationChain.cs        // composes them in order
```

Each migration is `Func<JObject,JObject>`. Pure. Idempotent. No side effects.

Loader:

```csharp
public SaveData Load(string path) {
    var raw = ReadAndDecompress(path);
    ValidateMagic(raw);
    var json = JObject.Parse(raw.Body);
    var fromVersion = (string)json["version"];
    var migrated = MigrationChain.Apply(json, fromVersion, CurrentVersion);
    return DeserializeStrict<SaveData>(migrated);
}
```

Migration never deletes data — it transforms or defaults. If a new field has no sensible default per old state, choose neutral (empty dict, empty list, 0).

Backward-incompatible bumps (rare): MAJOR version bump per release-pipeline spec. Loader refuses with explicit error directing user to compatibility doc.

## 5. Atomic write

Saves never partially overwrite. Pattern:

```csharp
var tmp = path + ".tmp";
File.WriteAllBytes(tmp, payload);
new FileInfo(tmp).Refresh();
File.Replace(tmp, path, path + ".bak", ignoreMetadataErrors:true);
```

`File.Replace`:
- Rotates current → `.bak`.
- Renames `.tmp` → current.
- Atomic on Windows + Linux. macOS uses `rename` syscall — also atomic.

Result: any crash mid-write leaves either old save or new save valid. Never partial.

## 6. Slots

Up to 8 user slots + autosaves. `saves/manifest.json`:

```json
{
  "slots": [
    { "slot":1, "campaignName":"Old Calder Run", "lastSaved":"...", "playtimeMs":5400000, "partyAvgLevel":2, "thumbnail":"slot-1/preview.webp" },
    { "slot":2, "campaignName":"Test Bloom Site", "lastSaved":"...", "playtimeMs":1200000 }
  ]
}
```

Phase 1 ships **single slot** (per `docs/design/09` cross-phase save concerns). Phase 2 enables multi-slot UI. The file layout supports both from day one.

Slot picker UI (Phase 2): grid of slot cards with preview thumbnail (screenshot taken at last save), campaign name, playtime, last saved date. Click → load. Empty slot → new campaign.

## 7. Autosave rotation

`_autosave/rotate-{1..4}.rpcsave`: round-robin slots. On each autosave:

1. Shift rotate-3 → rotate-4, rotate-2 → rotate-3, rotate-1 → rotate-2.
2. Write new save to rotate-1.
3. Copy rotate-1 to `latest.rpcsave`.

`pre_combat.rpcsave` written before combat enter only (per error-recovery spec §9). Overwritten on next combat enter.

Total autosave storage cap: 5 files × 30 KB = ~150 KB. Negligible.

## 8. Save thumbnail

On save, capture a 320×180 screenshot of the current screen (or pre-rendered banner per scene). Encoded WebP. Stored alongside save: `slot-N/preview.webp`. Used in slot picker.

If screenshot fails, fall back to generic banner per scene type (town/dungeon).

## 9. Save action and timing

`action:save_game`:

```json
{ "type":"save_game", "slot":1 }
```

Server response:

```json
// success
{ "kind":"event", "type":"toast", "ack":"...",
  "payload":{ "key":"ui.toast.save_success" } }

// failure
{ "kind":"event", "type":"error", "ack":"...",
  "payload":{ "code":"save_failed", "message":"disk full" } }
```

Permitted in `Menu` mode only Phase 1. Phase 2 may add checkpoint saves.

Server save operation is synchronous within action handler. Frame budget: typical 200 KB JSON serialization + gzip + write = ~10 ms. Acceptable.

## 10. Load action

`action:load_game`:

```json
{ "type":"load_game", "slot":1 }
```

Permitted in `Menu` mode only. Replaces current `GameState`. Emits full `state_update`.

If selected slot file missing: error.
If parse fails: try `.bak`, then autosaves (per error-recovery spec §8).
If all fail: error toast, retain current state.

## 11. Quick save / quick load (Phase 2)

Hotkeys F5 / F9 (per settings spec keybinds):

- F5: save to current slot if Menu mode; else save to `_autosave/quick.rpcsave`.
- F9: load from `_autosave/quick.rpcsave` if exists, else current slot.

Quick saves are separate from numbered slots — they're a fast iteration tool.

## 12. Data integrity validation

After load + migration, validate before installing as GameState:

- All ids reference real content (per content-pipeline spec §11).
- HP values clamped (Math.Clamp(currentHp, 0, maxHp) per character).
- Level ≥ 1 (per 2026-05-07 fix already in code).
- WorldTurn ≥ 0.
- Reputation values within faction-defined bounds.
- `stateHash` matches recomputed hash from gameState (warn-only — version-tolerant).

Any violation = log warn, clamp / drop offending field. Don't refuse load — degrade.

## 13. Compatibility table

| Game version | Save version | Notes |
|---|---|---|
| 0.1.0 - 0.3.0 | 1 | Phase 1 base |
| 0.4.0 | 8 | All 2026-05-10 specs merged |

Pre-1.0 game versions don't promise save compatibility forward (a beta tester moving 0.3 → 0.5 may need to start fresh if a spec changed mid-flight). Post-1.0: strict promise that all versions migrate forward.

## 14. Tests

- xUnit: every migration step round-trips a representative save.
- xUnit: atomic write — kill process mid-save → either old or new file valid.
- xUnit: gzip + magic byte validation rejects malformed.
- xUnit: thumbnail capture + load preview.
- Playwright: save + reload preserves party HP, position, inventory, mission state.
- Manual: 100 saves over hours of play, audit corruption rate (target zero).

## 15. Out of scope

- Cloud save sync.
- Cross-platform save portability (Linux ↔ Windows ↔ Mac should already work since format is platform-agnostic; not validated until each platform ships).
- Manual save editing tool. Discouraged; saves are not config.
- Save sharing between users — Phase 3 may add for community challenges.
