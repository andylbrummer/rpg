# Telemetry — Design Spec
Date: 2026-05-10
Status: design — Phase 3 deliverable; Phase 1 + 2 ship local-only metrics
Depends on: settings spec (opt-in flag), error-recovery spec (crash report), determinism spec (action log)
Scope: local-first metrics, opt-in upload, schema, privacy. Single-player game; telemetry serves designer balance + bug repro, not engagement-farming.

## 1. Principles

- **Local-first.** Phase 1 + 2 write metrics to disk only. Never network-attached.
- **Opt-in for upload.** Phase 3 may offer "Help improve balance — share play data" toggle (default OFF). Explicit choice with what-gets-sent screen.
- **No PII.** Player name, save contents, IP — never logged or uploaded. Only mechanical metrics.
- **Player owns the data.** Settings → Telemetry → Export / Delete buttons.
- **No A/B testing.** No remote-config-driven game changes. Single canonical experience.
- **No engagement tracking.** Session length, retention curves — not relevant to design goals.

## 2. What gets logged

### 2.1 Balance metrics

| Event | Fields | Purpose |
|---|---|---|
| `combat_completed` | encounterId, rounds, partyAvgLevel, victory, fled, resourcesUsed | Combat tuning |
| `dungeon_completed` | dungeonType, duration_turns, encounters_won, resources_remaining_pct | Attrition tuning |
| `synergy_discovered` | synergyId, turnNumber, partyComp | Discovery flow validation |
| `synergy_used` | synergyId, count | Frequency analysis |
| `branch_chosen` | class, branch, level, partyComp | Branch popularity |
| `character_died` | class, level, cause, dungeonType | Death balance |
| `character_resurrected` | class, attempts, statPenalty | Resurrection economics |
| `mission_completed` | missionId, outcomeId, turnNumber | Outcome distribution |
| `mission_failed` | missionId, reason, turnNumber | Failure cause distribution |

### 2.2 Performance

| Event | Fields | Purpose |
|---|---|---|
| `frame_time_p99` | fpsP50, fpsP99, mode, dungeonType | Render perf hot spots |
| `action_latency` | actionType, ms_p50, ms_p99 | Server perf |
| `combat_resolve_ms` | rounds, ms | Combat AI cost |
| `state_payload_size` | bytes, full | Bandwidth |
| `content_load_ms` | rpkBytes, ms | Boot perf |

### 2.3 Errors

| Event | Fields | Purpose |
|---|---|---|
| `error_recoverable` | code, actionType | Action validation failures |
| `error_engine` | exceptionTypeName, sanitized stack | Bug repro |
| `error_client` | message, sanitized stack, screen | UI bug repro |
| `save_failed` | reason, slot | Save reliability |
| `desync_full_resync` | reason | Diff protocol health |

### 2.4 What is NOT logged

- Save file contents.
- Item / character names players type (player-authored strings).
- Dialogue choice text (only `nodeId` + `choiceIndex`).
- Specific tile coordinates (only mode + dungeon type).
- Times (only turn counts; no wall clock).
- Settings values beyond a few aggregate flags.
- Anything that could identify the install (no hostname, mac, user, hardware IDs).

## 3. Storage

`{appData}/rpc/telemetry/events.jsonl` — append-only:

```jsonl
{"t":"combat_completed","ts_iso":"...","fields":{...}}
{"t":"synergy_discovered","ts_iso":"...","fields":{...}}
```

`ts_iso` is wall clock for analytics ordering — does NOT participate in game determinism (turn-based logic uses world turns).

Rotation: weekly. `events.{week}.jsonl`. Retention: 90 days local. Older purged.

Bounded size cap: 100 MB total across rotations. Oldest deleted past cap.

## 4. Local viewing

Settings → Telemetry → View Recent Events opens a table modal with last 100 entries. Filter by type. Useful for designer + player verification of what's recorded.

Export: dump all current files into a `.zip` to a user-chosen location.

Delete: wipe all `telemetry/` content. Confirm-destructive modal.

## 5. Upload (Phase 3, opt-in only)

If `settings.telemetry.uploadEnabled` true:

- Daily batch: sample up to 1000 events, ship to endpoint via HTTPS POST.
- Payload format: same JSONL as local file, ndjson-compressed.
- Endpoint: `https://telemetry.{game-domain}/v1/ingest` (TLS pinned per Phase 3 release).
- No authentication needed (anonymized data).
- 30-second timeout, no retry storm.
- Network failure = log locally, try next day.

Opt-in screen mockup:

```
┌──────────────────────────────────────────────────────────────────┐
│ Share Play Data?                                                 │
│                                                                  │
│ We use anonymized telemetry to balance combat and find bugs.     │
│ No personal info, no game content, no save files are shared.     │
│                                                                  │
│ ☑ See exactly what's sent (toggles preview pane)                 │
│                                                                  │
│ Preview:                                                         │
│   • Combat encounter results (won/lost, rounds, party level)     │
│   • Synergy discovery counts                                     │
│   • Performance numbers                                          │
│   • Errors / crashes                                             │
│                                                                  │
│ [Enable]   [No thanks]                                           │
└──────────────────────────────────────────────────────────────────┘
```

Decline = local logging continues (it's harmless), nothing uploaded.

## 6. Privacy

- No tracking IDs. Each upload batch generates a fresh random UUID; sink dedupes only within batch.
- No cross-session correlation possible from upload sink alone.
- No referrer / user-agent strings beyond minimal "rpc/0.4.0".
- Sink retention policy: 1 year max, then aggregate-only.
- GDPR: data is non-personal by design; export/delete mechanism in-app covers right-to-erasure for local data; upload data already non-attributable so no per-user erasure needed.

## 7. Crash reports

Separate from event log (see error-recovery spec §12). Crash reports:
- Saved locally by default.
- Upload only on explicit "Send" button click per occurrence.
- One-time confirmation per report (no "always send").

## 8. Implementation

`src/engine/RPC.Engine/Telemetry/`:

```
Telemetry/
  TelemetrySink.cs           // file writer w/ rotation
  Events.cs                  // strongly-typed event records
  Uploader.cs                // Phase 3
```

`src/client/src/telemetry/`:

```
telemetry/
  ClientTelemetry.ts         // perf events from client
  index.ts                   // emit + queue
```

Client emits to server via `action:telemetry` (Phase 3 only); Phase 2 client perf logged locally to a separate file.

API:

```csharp
Telemetry.Emit(new CombatCompletedEvent(encounterId, rounds, ...));
```

```ts
ClientTelemetry.emit('frame_time_p99', { fpsP50, fpsP99, mode });
```

Calls are fire-and-forget. Background flush every 10 s or 100 events.

## 9. Designer dashboard (Phase 3)

If upload enabled and sink populated, a designer-facing dashboard aggregates:
- Combat win/loss distribution by encounter
- Synergy frequency
- Branch popularity
- Death cause distribution per dungeon
- Performance percentile heatmaps

Built on top of any standard analytics warehouse (BigQuery / ClickHouse). Out of this spec's scope; this spec defines only the producer side.

## 10. Tests

- Unit: emit + read round-trip of events.
- Unit: rotation at week boundary.
- Unit: size cap evicts oldest files.
- Unit: opt-out wipes upload queue without sending.
- Manual: verify export zip contains exactly local files.

## 11. Phase rollout

Phase 1: Telemetry sink + event types for combat + dungeon. Local only. No UI surfacing.

Phase 1.5: Add synergy + branch events. Settings → Telemetry → View / Export / Delete.

Phase 2: Performance + state-diff events. Local only.

Phase 3: Opt-in upload + dashboard.

## 12. Out of scope

- Real-time analytics / streaming.
- A/B experiments.
- Engagement metrics (DAU/MAU/retention).
- Monetization analytics (single-purchase or none).
- Cross-platform device tracking.
