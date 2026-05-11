# State Diff Protocol — Design Spec
Date: 2026-05-10
Status: design — Phase 2 deliverable; Phase 1 uses full snapshot
Depends on: websocket-protocol spec (reserves `full:false` bit)
Scope: incremental state updates, bandwidth reduction, ordering guarantees, fallback to full sync. Currently full GameState pushed per action (~30-100 KB). Phase 2 needs sub-KB typical updates.

## 1. Motivation

Phase 2 adds:
- Factions (5 state machines)
- Rumors (~50 active)
- NPCs (~30 with state)
- Mission catalog growth
- Larger party + bench (up to 12 chars × richer state)

Projected snapshot 200-500 KB. Sending per movement step over WS noticeable. Diff target: median <2 KB, p99 <20 KB.

## 2. Diff format

Use JSON Patch (RFC 6902) with extensions for arrays:

```json
{ "ops": [
    { "op":"replace", "path":"/player/x", "value":17 },
    { "op":"replace", "path":"/player/facing", "value":"E" },
    { "op":"add", "path":"/dungeon/explored/-", "value":"17,42" },
    { "op":"add", "path":"/dungeon/tiles/window/123", "value": {...} },
    { "op":"replace", "path":"/party/0/CurrentHp", "value":18 }
  ],
  "baseHash":"abc123",
  "resultHash":"def456",
  "seq":42
}
```

`baseHash` = hash of the client-side state diff is applied against. `resultHash` = expected hash after applying. Both let client detect desync.

Server emits via `event:state_update`:

```json
{ "kind":"event", "type":"state_update",
  "payload":{ "full":false, "diff":{...} } }
```

OR (when full):

```json
{ "kind":"event", "type":"state_update",
  "payload":{ "full":true, "state":{...}, "hash":"..." } }
```

## 3. Ordering and sequence

Server maintains a monotonic `stateSeq` (separate from action `id`). Each emit increments by 1.

Client tracks last applied `stateSeq`. If a diff arrives with `seq > lastSeq+1`, client requests full resync via `action:state_resync`.

`subscribe` action (reserved in protocol spec) becomes meaningful here:

```json
{ "kind":"action", "type":"subscribe",
  "payload":{ "topics":["player","party","dungeon.window","combat"] } }
```

Server only diffs subscribed topics. Phase 2 default subscription = all. Phase 2+ optimization: hide irrelevant topics (e.g., journal updates not needed during combat overlay).

## 4. Topic boundaries

Diff scoping by top-level state keys:

| Topic | Includes |
|---|---|
| `player` | position, facing |
| `party` | members, equipment, status, hp |
| `dungeon.meta` | type, dimensions, encounter table |
| `dungeon.window` | tile send window, explored set |
| `combat` | combatants, log, blackboard, phase |
| `inventory` | backpacks, cache, gold, tithe |
| `missions` | active, available, history |
| `journal` | synergies, lore, bestiary |
| `world` | turn, flags, factions (Phase 2) |
| `dialog` | active dialog state |

Each topic independently diffable. Cross-topic invariants (e.g., character HP in both party + combat) — server is authoritative source; both update.

## 5. Computation

Server keeps the **last sent state per client**. On state change:

1. Compute new `GameState`.
2. Run diff against last-sent.
3. Filter ops by client's subscribed topics.
4. Emit `state_update` with diff.
5. Update last-sent to new state.

Diff lib: `Microsoft.AspNetCore.JsonPatch` (.NET) or custom shallow diff for hot paths. Custom recommended — JsonPatch lib is generic; we know our shape.

Special-case ops for hot fields:
- `player.position` always single `replace`.
- `combat.combatants[i].Hp` keyed by id, not index, to survive reorderings.
- `dungeon.explored` — set semantics; track adds only (clear is rare, full sync).

## 6. Client apply

`src/client/src/net/StateApplicator.ts`:

```ts
function applyDiff(state: GameState, diff: Diff): GameState {
  if (clientHash(state) !== diff.baseHash) return REQUEST_FULL_SYNC;
  const next = structuredClone(state);
  for (const op of diff.ops) applyOp(next, op);
  if (clientHash(next) !== diff.resultHash) return REQUEST_FULL_SYNC;
  return next;
}
```

Hash validation **before** UI updates. Mismatch = silently request full snapshot, do not present intermediate inconsistent state.

## 7. Fallbacks

Server forces full sync when:
- Diff size > 50% of estimated full snapshot size (no benefit).
- Client requests `state_resync`.
- Reconnect / session resume.
- Mode transition (Menu → Exploration → Combat → Menu) — full snapshot keeps client sane.
- Save load.

## 8. Performance budget

| Operation | Budget |
|---|---|
| Server diff compute | <2ms p99 (~200 KB state) |
| Diff payload | median <2 KB, p99 <20 KB |
| Client apply | <1ms |
| Hash compute (xxhash64) | <500 µs |

If exceeded, profile and shard further (e.g., per-character diff bundles).

## 9. Phase 1 compatibility

Protocol spec already lets `full:true` continue working — Phase 1 servers always emit full; Phase 2 servers may emit either. Clients MUST handle both. Phase 1 clients receiving diffs from a Phase 2 server: server detects via `clientVersion` in hello and downgrades to full mode for old clients.

## 10. Save / load impact

No save format change. Diff exists only on wire.

## 11. Tests

- xUnit: diff round-trip — apply(state, diff(state, state')) === state'.
- xUnit: hash mismatch → full resync request emitted.
- Property test (FsCheck): random state mutation → diff applies cleanly.
- Playwright: 1000 movement steps → bandwidth comparison full vs diff (measure ws bytes).

## 12. Debugging

- Dev mode: log per-diff size + apply time to console.
- Hash trace mode: log `(baseHash → resultHash, ops)` for last 100 diffs.
- `debug.forceFullSync` setting: disable diffs (regression isolation).

## 13. Out of scope

- Binary diff formats (msgpack / cbor) — JSON Patch sufficient; revisit if bandwidth becomes critical.
- Delta compression of `dungeon.window` tile data (use full window when player moves > radius).
- Optimistic client prediction (server is authoritative; latency local).
