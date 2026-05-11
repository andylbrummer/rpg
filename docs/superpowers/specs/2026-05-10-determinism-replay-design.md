# Determinism & Replay — Design Spec
Date: 2026-05-10
Status: design — Phase 2 deliverable; Phase 1 already snapshots combat tests
Depends on: combat-ai spec, websocket-protocol spec, field-notes-journal spec
Scope: seed management, RNG isolation, replay format, divergence detection, debug tools. Enables snapshot tests, synergy replay buffers, bug repro, future replay-share.

## 1. Determinism contract

Given:
- starting `GameState` (or save file)
- ordered sequence of player actions with timestamps
- identical content RPK hash

…running the engine MUST produce identical end state and identical `event:fx` event stream.

Out of scope for determinism: floating-point ordering across CPUs (we don't use floats in combat math), client-side animation timing (cosmetic), audio playback.

## 2. RNG architecture

Existing `GameRandom` in `src/engine/RPC.Engine/Combat/GameRandom.cs` is per-instance. Extend to a layered model:

```
RootSeed (long, set per save)
  ├─► EncounterRng     (sub-seed = hash(RootSeed, "encounter", dungeonId))
  ├─► CombatRng        (sub-seed = hash(RootSeed, "combat", combatStartTurn))
  │     ├─► InitRng    (sub-seed = hash(CombatRng, "init"))
  │     ├─► HitRng     (sub-seed = hash(CombatRng, "hit"))
  │     ├─► DamageRng  (sub-seed = hash(CombatRng, "damage"))
  │     └─► AiRng      (sub-seed = hash(CombatRng, "ai"))
  ├─► LootRng          (sub-seed = hash(RootSeed, "loot", encounterId))
  ├─► DungeonRng       (sub-seed = hash(RootSeed, "dungeon", dungeonId))
  └─► WorldRng         (sub-seed = hash(RootSeed, "world", worldTurn))
```

Hash: `xxhash64`. Sub-seed derivation deterministic from string keys; consumers always get fresh sub-rng per-purpose.

### 2.1 API

```csharp
public sealed class RngTree {
    private readonly ulong _root;
    public RngTree(ulong rootSeed) { _root = rootSeed; }
    public GameRandom For(params object[] context) => new(MixSeed(_root, context));
    private static ulong MixSeed(ulong seed, object[] ctx) { /* xxhash64 chain */ }
}
```

`GameState`:

```csharp
public ulong RootSeed { get; init; }
public RngTree Rng { get; init; }
```

Replaces ad-hoc `new GameRandom(_encounterRng.Roll(1, 10000))` chains in current `GameState.cs:168,180,194`.

### 2.2 Why this matters

Snapshot tests already rely on determinism. Current code seeds combat from encounter rng with a random roll — fragile to refactors that change roll order. Tree-derived seeds make each sub-system insulated: changing initiative algorithm doesn't perturb damage rolls.

## 3. Action log

Server maintains a per-save **action log**:

```csharp
public record LoggedAction(
    int Sequence,             // monotonic per save
    int WorldTurn,
    GameMode Mode,
    string ActionType,
    string PayloadJson,
    DateTime ServerReceivedAt,
    string ResultStateHash    // hash of state after applying
);
```

Persisted at `saves/{slot}/actions.jsonl` (append-only). Rotated when reaches 10k lines; older lines moved to `actions.{rotation}.jsonl`.

Action log is what makes replay possible: given save + log, full session reconstructable.

## 4. State hash

Engine emits a deterministic hash of `GameState` after every action:

```csharp
public static string HashState(GameState s) {
    // Canonical serialization (sorted keys, fixed culture, no DateTime)
    // SHA-256 of canonical bytes → hex
}
```

`ResultStateHash` recorded in action log. Used for:
- Divergence detection: replay engine compares post-step hash to recorded; mismatch = bug.
- Anti-cheat (Phase 3 multiplayer if pursued).

Hash excludes cosmetic fields (`LastUpdate` DateTime, transient fx queues). Strictly game-relevant state only.

## 5. Replay format

`.rpcreplay` file = JSON line stream:

```jsonl
{"kind":"header","version":1,"gameVersion":"0.3.0","contentHash":"abc...","rootSeed":12345,"startedAt":"..."}
{"kind":"initial_state","stateHash":"...","compressed":"base64-gzip-of-savefile"}
{"kind":"action","seq":1,"turn":0,"mode":"Menu","type":"enter_dungeon","payload":{"dungeonType":"broken_engine"},"resultHash":"..."}
{"kind":"action","seq":2,"turn":0,"mode":"Exploration","type":"move_forward","payload":{},"resultHash":"..."}
...
{"kind":"footer","ended":"...","reason":"victory|defeat|quit|crash"}
```

Self-contained: header + initial save + action stream + footer. Replays portable across machines if game version + content hash match.

### 5.1 Replay player

`tools/replay/`:

```
replay/
  Cli.cs            // command-line: load .rpcreplay, run, compare hashes
  Player.cs         // step-by-step or full-run
```

Outputs PASS / FAIL with first divergence point if any.

### 5.2 In-game replay viewer

Field Notes spec §5 (synergy replay buffers) uses a slim subset:
- 3-round combat snippet
- Start state + action list within combat
- Played back at variable speed

Full session replay not surfaced in-game Phase 1.5 — debug tool only. Phase 3 may add a "Campaign Story" viewer.

## 6. Snapshot tests

Existing combat snapshot harness (Phase 1 T25) extends to:

```csharp
[Fact]
public void Scenario_BoneArcher_AgainstFrontLineParty() {
    var snapshot = LoadSnapshot("bone_archer_vs_front.json");
    var actual = ReplayHarness.Run(snapshot);
    Assert.Equal(snapshot.ExpectedFinalState, actual.FinalState);
    Assert.Equal(snapshot.ExpectedLog, actual.Log);
}
```

Snapshot JSON:

```json
{
  "id": "bone_archer_vs_front",
  "rootSeed": 42,
  "initialParty": [...],
  "encounter": "bone_archer_x2",
  "actions": [
    { "type":"combat_action", "action":{"type":"attack","actorId":"...","targetId":"..."} },
    ...
  ],
  "expectedFinalState": { "stateHash": "..." },
  "expectedLog": [ "Kael attacks Bone Archer for 7", "...", "Victory" ]
}
```

`tools/snapshot-update/` re-records goldens when intentional behavior change. CI fails on accidental drift.

Phase 1 baseline target: 10 snapshots (per plan T25). Phase 1.5 expand to 30 covering downed/stabilized, synergies, AI archetypes.

## 7. Divergence reports

When replay diverges:

```
Divergence at action seq=14 (combat_action attack)
Expected state hash: 9f3a4c...
Actual state hash:   8e1b22...

Diff:
  party[0].Hp: 18 → 19      (off by 1; suspect rounding)
  combat.round: 3 → 3
  combat.log[12]: "Kael deals 7" → "Kael deals 6"
```

Tooling computes structural diff between expected and actual GameStates, prints first 20 diffs. Designer-friendly.

## 8. Debug tools

`debug.showSeed` setting (settings spec §2) reveals root seed in TopBar. Click → copy to clipboard. Bug reports embed seed + save + action log.

`debug.forceSeed` setting: override root seed at next save creation. Use for reproducing user-reported bugs.

`debug.stateHashOverlay`: tiny watermark in corner showing current state hash. Spotters can correlate "state hash X = visual bug here".

## 9. Performance

- xxhash64 ~1 GB/s on typical CPU. State serialization ~100 KB → 100 µs hash.
- Action log append: <50 µs per line.
- Replay player: ~10k actions/second on dev machine.

Budget acceptable. If state size grows past 1 MB Phase 2, switch to incremental hashing.

## 10. Save / load

`SaveData` carries `RootSeed`. Action log lives separately at `saves/{slot}/actions.jsonl` (not embedded — keeps save file small). On load, if action log present, optionally verify by replay (off by default — opt-in via debug flag).

Save version → `"7"` (after world clock v6).

## 11. Phase rollout

Phase 1: RngTree + state hash + action log writer. Replay player as CLI. Snapshot tests on engine.

Phase 1.5: snapshot-update tool. Synergy replay buffers consume the same machinery.

Phase 2: divergence dashboard CI report. Designer-facing replay viewer for bug reports.

Phase 3: replay-as-share (export `.rpcreplay`, post to community). Anti-cheat hash check for shared replays.

## 12. Tests

- xUnit: RngTree determinism (same seed + context → same stream).
- xUnit: state hash stability over re-serialization round trips.
- xUnit: replay player reproduces 5 reference replays exactly.
- xUnit: divergence detection on synthetic mismatch.
- Manual: replay viewer reproduces a bug report end-to-end.

## 13. Out of scope

- Real-time rollback (this is turn-based; no rollback needed).
- Multiplayer state sync (Phase 3 if pursued).
- Compressed action log format — JSONL is fine at projected sizes (≤1 MB per campaign).
- Encrypted/signed replays (Phase 3 with anti-cheat).
