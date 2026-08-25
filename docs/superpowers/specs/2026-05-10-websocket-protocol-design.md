# WebSocket Protocol — Design Spec
Date: 2026-05-10
Status: design — partial impl exists in `src/engine/RPC.Host/Web/GameServer.cs` + `src/client/src/net/GameClient.ts`. Formalize now to prevent drift.
Scope: message envelope, version handshake, action enum, state push policy, error model, reconnect semantics. JSON over WebSocket. Phase 1 = full state push; Phase 2 = delta protocol slot reserved.

## 1. Envelope

All frames are JSON. Two top-level kinds: **Action** (C→S) and **Event** (S→C).

```json
// Client → Server
{
  "kind": "action",
  "id": "uuid-v4",          // client-generated, server echoes in ack
  "v": 1,                   // protocol version (current = 1)
  "type": "move_forward",   // see §3 enum
  "payload": { /* type-specific */ }
}

// Server → Client
{
  "kind": "event",
  "id": "uuid-v4",          // event-id (server-generated)
  "v": 1,
  "type": "state_update",   // see §4 enum
  "ack": "uuid-v4 of action being acknowledged | null",
  "payload": { /* type-specific */ }
}
```

Server NEVER initiates an Action; client NEVER emits an Event.

Frame size cap: client-emitted ≤ 64 KB (large for safety; typical action <1 KB). Server-emitted ≤ 1 MB (full state push for large dungeons). Server uses `MemoryStream` accumulation per existing `GameServer.cs:275` already correctly handling >4 KB frames.

## 2. Handshake

On WebSocket OPEN:

1. **Server** immediately sends `event:hello`:
   ```json
   { "kind":"event", "type":"hello", "v":1,
     "payload": { "serverVersion":"0.3.0", "supportedProtocols":[1],
                  "sessionId":"uuid", "serverTime":"ISO-8601" } }
   ```
2. **Client** responds with `action:hello`:
   ```json
   { "kind":"action", "type":"hello", "v":1,
     "payload": { "clientVersion":"0.3.0", "preferredProtocol":1,
                  "resumeSessionId":"uuid|null" } }
   ```
3. **Server** sends initial `event:state_update` with full `GameState`. Session now active.

If `preferredProtocol` not in `supportedProtocols`: server replies `event:fatal` with `protocol_unsupported`, closes socket with code 4000.

If `resumeSessionId` matches a paused session (≤120s old): server resumes that session's GameState. Otherwise new session, new sessionId, new default state.

## 3. Action types (C→S)

Frozen v1 enum. Adding a new action bumps minor (still v1). Removing/renaming bumps to v2.

| Type | Payload | Modes allowed | Notes |
|---|---|---|---|
| `hello` | see §2 | always | once per session |
| `move_forward` | `{}` | exploration | |
| `turn_left` | `{}` | exploration | |
| `turn_right` | `{}` | exploration | |
| `turn_around` | `{}` | exploration | 180° in place |
| `strafe_left` | `{}` | exploration | Phase 1.5 |
| `strafe_right` | `{}` | exploration | Phase 1.5 |
| `move_backward` | `{}` | exploration | Phase 1.5 |
| `interact` | `{}` | exploration | uses tile-front interactable |
| `enter_dungeon` | `{dungeonType:"broken_engine"}` | menu | |
| `enter_combat` | `{}` | exploration | debug / forced encounter |
| `combat_action` | `{action:CombatAction}` | combat | see §3.1 |
| `flee_combat` | `{}` | combat | |
| `return_to_town` | `{}` | exploration, combat-post-result | |
| `rest` | `{}` | menu | inn rest |
| `save_game` | `{slot?:int}` | menu | Phase 1: slot ignored (single slot) |
| `load_game` | `{slot?:int}` | menu | |
| `reset_game` | `{}` | always | |
| `swap_row` | `{characterId,row}` | menu, exploration | already implemented |
| `inv_move` | `{from,to,qty}` | exploration, menu | see inventory spec |
| `inv_equip` | `{characterId,slotIndex,equipSlot}` | exploration, menu, combat-quick (Phase 1.5) | |
| `inv_unequip` | `{characterId,equipSlot,dstSlotIndex}` | exploration, menu | |
| `inv_use` | `{from,targetCharacterId?}` | combat (consumable action), exploration | |
| `inv_drop` | `{from,qty}` | exploration | |
| `inv_split` | `{from,qty,to}` | exploration, menu | |
| `inv_sort` | `{zone,characterId?}` | menu | |
| `resurrect` | `{characterId}` | menu (Bone Clerk) | see combat-state spec |
| `vendor_buy` | `{vendorId,itemId,qty,dst}` | menu | |
| `vendor_sell` | `{from,qty}` | menu | |
| `level_up` | `{characterId,choices?}` | menu | Phase 1 auto; Phase 1.5 manual |
| `mission_accept` | `{missionId}` | menu | Phase 1.5 |
| `ping` | `{t:clientTimestampMs}` | always | heartbeat |
| `subscribe` | `{topics:string[]}` | always | reserved Phase 2 (selective updates) |

### 3.1 `CombatAction` payload

```json
{
  "actorId": "guid",
  "type": "attack|defend|cast|use_item|flee|wait|stabilize",
  "abilityId": "string|null",
  "targetId": "guid|null",
  "targetGroup": "string|null",
  "itemSlot": "InventoryAddress|null"
}
```

### 3.2 Mode-mismatch rejection

If server receives an action invalid for current `GameState.Mode`, replies with `event:error` `code:wrong_mode`, includes the violated rule. Does not apply.

## 4. Event types (S→C)

| Type | Payload | Notes |
|---|---|---|
| `hello` | see §2 | once per session |
| `state_update` | `{ state: GameState, full:bool }` | `full:true` = entire snapshot; `false` = partial diff (Phase 2). Phase 1 always `true`. |
| `combat_log` | `{ entries: CombatLogEntry[] }` | streamed alongside state if log grew |
| `fx` | `{ events: FxEvent[] }` | client-side fx triggers (damage numbers, flashes). Server emits, client renders. Replaces ad-hoc inference. |
| `toast` | `{ level:"info|warn|error", text, dismissAfterMs? }` | server-pushed user message |
| `ack` | `{ ackId: actionId }` | optional positive ack when no state changes (e.g., no-op action) |
| `error` | `{ code, message, ackId? }` | recoverable error |
| `fatal` | `{ code, message }` | unrecoverable; server closes socket |
| `ping` | `{ t:serverTimestampMs, clientT?:n }` | heartbeat echo |
| `paused` | `{}` | server entered paused state (focus loss Phase 2) |
| `resumed` | `{}` | server resumed |

### 4.1 `FxEvent` types

```json
{ "type":"damage_number", "target":"guid|tilePos", "value":12,
  "crit":false, "kind":"physical|fire|necrotic|heal" }
{ "type":"miss", "target":"guid" }
{ "type":"dodge", "target":"guid" }
{ "type":"status_applied", "target":"guid", "statusId":"bleeding", "duration":3 }
{ "type":"status_removed", "target":"guid", "statusId":"bleeding" }
{ "type":"downed", "target":"guid" }
{ "type":"stabilized", "target":"guid" }
{ "type":"died", "target":"guid" }
{ "type":"level_up", "target":"guid", "newLevel":2 }
{ "type":"loot", "items":[{itemId,qty}], "source":"chest|enemy|tile" }
{ "type":"encounter_incoming", "encounterId":"string" }
{ "type":"tile_revealed", "tiles":[{x,y}] }
```

Centralizing fx in server events lets client UI fire flashes/toasts deterministically without re-computing diffs from `state_update`.

## 5. State payload

`state` in `state_update` is the client-facing projection of server `GameState`. Fields (Phase 1):

```ts
interface GameState {
  protocolVersion: 1;
  mode: 'Menu' | 'Exploration' | 'Combat' | 'Dialog';
  player: { x: number; y: number; facing: 'N'|'E'|'S'|'W' };
  party: PartyMember[];
  dungeon?: {
    type: string;
    width: number; height: number;
    tiles: TileWindow;          // see §5.1
    explored: string[];         // "x,y" keys in current send window
    bodyMarkers: { characterId:string; x:number; y:number }[];
  };
  combat?: CombatState;
  lastCombatResult?: CombatResult;
  inventory?: InventorySnapshot;  // see inventory spec
  gold: number;
  titheTokens: number;
  worldTurn: number;
  serverTime: string;
}
```

### 5.1 TileWindow

Server sends only tiles within `sendRadius` (currently 8) of player. Out-of-window tiles are not transmitted. Already implemented in `GameServer.cs:528`. Document the contract:

```ts
interface TileWindow {
  origin: { x:number; y:number };  // center, == player pos
  radius: number;                  // sendRadius
  tiles: Tile[];                   // flat array, indexed (x - origin.x + r) + (y - origin.y + r)*w
}
```

Client maintains a sparse persistent map of revealed tiles, **never deleting** previously revealed tiles. Server sends current window; client merges. Automap reads from client-side persistent map.

## 6. Error model

`event:error` payload:

```json
{ "code": "wrong_mode|invalid_action|out_of_range|insufficient_resources|stack_full|locked_item|not_implemented",
  "message": "human-readable detail",
  "ackId": "originating action id|null" }
```

Recoverable. Client toasts level=warn or error per code. Action `id` lets client correlate to the failed UI request.

`event:fatal` payload:

```json
{ "code": "protocol_unsupported|server_error|version_skew|session_lost",
  "message": "..." }
```

Server closes socket with code 4xxx. Client surfaces a non-dismissable modal and offers Reconnect.

## 7. Reconnect

`GameClient.ts` already implements exponential backoff (`Math.min(2^n*1000, 30000)`, max 5 attempts).

Add idempotency:
- Client persists last sent action `id` in memory across reconnects.
- On reconnect handshake, client sends `resumeSessionId`. Server attempts to resume.
- If resumed: server replays any actions client emits with `id` it has not processed yet (client retransmits queued in-flight actions).
- If new session: client discards queue, full state snapshot from server is authoritative.

Queue rules:
- Client queues actions only when socket NOT open.
- Max queue size 32; older actions dropped with warning toast.
- On reconnect, queue flushes in order before any new actions.

Session GC:
- Server retains paused session state 120 s. After that, evicted. New session next connect.
- `event:fatal code:session_lost` if client provides expired `resumeSessionId`.

## 8. Heartbeat

- Client sends `action:ping {t}` every 15 s when idle.
- Server replies `event:ping {t, clientT}`.
- Client tracks RTT moving average for status indicator color thresholds.
- If client misses 2 consecutive pings (>30 s): mark disconnected, force socket close + reconnect flow.
- Server: if no client message in 45 s: close socket.

## 9. Versioning policy

- `v` field is the **protocol** version, not server build.
- v1 additions allowed (new action/event types, new optional payload fields) — client must tolerate unknown event types (log + ignore).
- v1 removals or semantic changes are NOT allowed; bump to v2.
- Both `hello` exchanges include client/server build strings for telemetry; they do not gate compatibility.
- Servers MUST accept v=1 for at least one minor build after introducing v2.

## 10. Implementation work

`src/engine/RPC.Host/Web/`:
- `Protocol.cs` — new: typed records for envelope, action enum, event enum, error codes
- `MessageDispatcher.cs` — new: replaces ad-hoc switch in `GameServer.HandleMessage`. Maps action → handler delegate. Centralized validation.
- `GameServer.cs` — refactor to use `Protocol` types and `MessageDispatcher`. Add `hello` exchange. Add `error` envelope. Wrap existing replies with new envelope.

`src/client/src/net/`:
- `Protocol.ts` — new: mirror types. Auto-codegen target for Phase 2.
- `GameClient.ts` — refactor: emit/parse envelopes. Add `id` generation, ack correlation map, queue+drain on reconnect, ping loop. Wire `resumeSessionId` flow.
- `FxBus.ts` — new (also referenced in design-system spec): subscribe to `event:fx` payloads, drain into Svelte stores for damage numbers / toasts / vignettes.

Phase 1 deliverables: envelope + hello + state_update + error + ack + ping + fx events. Subscribe / paused-resumed deferred.

## 11. Tests

- xUnit (server): envelope parse round-trip, action enum coverage, mode-mismatch rejection per action, version negotiation matrix.
- xUnit replay: feed canned action log → snapshot state. Used by combat snapshot tests.
- Playwright: kill server mid-action → reconnect → action re-emits → state converges. Simulate 30s pause → resume → state preserved.
- Fuzz: random JSON payloads → server must not panic (return `event:error` or close cleanly).

## 12. Security

- WebSocket origin check: only Photino webview origin (`file://` or app://) + localhost dev (`http://localhost:5173`).
- No authentication in Phase 1 (single-user local). Phase 3 LLM-as-a-service: add bearer token in `hello`.
- Payload size enforcement: any frame > size cap → drop + `event:fatal code:server_error`.
- All payloads validated against JSON schema in `Protocol.cs` before dispatch.

## 13. Out of scope

- Multi-user / multiplayer.
- Binary framing (msgpack / cbor) — Phase 2 if state push becomes a bottleneck.
- Server push-only state diff (delta) — Phase 2; reserved bit `full:false` in `state_update`.
- Compression (permessage-deflate) — leave to WebSocket layer if needed.
