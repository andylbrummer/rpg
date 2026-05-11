# Testing Strategy — Design Spec
Date: 2026-05-10
Status: design — consolidates testing approaches scattered across all specs; mandates per-tier coverage
Depends on: all specs (each declares its tests)
Scope: test pyramid, test types, tooling, CI gating, snapshot management, performance budgets, accessibility validation. Single normative source for "what gets tested where."

## 1. Pyramid

```
                  ▲
              E2E │ ~50 tests (Playwright)
                  │
            INTEG │ ~150 tests (xUnit integration)
                  │
             UNIT │ ~600 tests (xUnit + Vitest)
                  │
              ▼
```

Targets approximate; ratio matters more than absolute count.

## 2. Layers

### 2.1 Unit (engine: xUnit; client: Vitest)

- Pure domain functions: combat math, formula parsing, inventory ops, dungeon assembly invariants, state migrations.
- No I/O, no mocks unless data fixture.
- < 100 ms per test, parallelizable.
- Coverage target: 80% on `src/engine/RPC.Engine/` domain. UI components: targeted props/state branches, not full DOM.

### 2.2 Integration (xUnit)

- Cross-module flows: action → state change → event emission; mission trigger → state update → reward applied; save → load round-trip.
- May start an in-memory server.
- No webview / browser.
- < 1 s per test.

### 2.3 Snapshot (xUnit)

- Combat replay scenarios (per determinism-replay spec §6).
- Authored .json fixtures with initial state + action stream + expected final state hash + log.
- Re-recordable via `tools/snapshot-update/`.
- CI gates on snapshot mismatch.
- Phase 1: 10 scenarios; Phase 1.5: 30; Phase 2: 60.

### 2.4 E2E (Playwright)

- Real Photino-equivalent (browser pointed at built bundle).
- Critical paths: launch → connect → enter dungeon → combat → return → save → reload.
- Visual snapshots at 768 / 1024 / 1280 widths.
- Slower (5-30 s each); kept minimal.
- Existing `tests/e2e/specs/` directory.

### 2.5 Property tests (FsCheck for .NET; fast-check for client)

- Per-spec where applicable: diff round-trip (state-diff spec §11), formula evaluator (ability spec §9), zone-move matrix (inventory spec §6).
- Random inputs explore edge cases manual tests miss.

### 2.6 Performance tests (xUnit + BenchmarkDotNet)

- Per spec's perf budget (e.g., AI <1 ms/turn, diff <2 ms compute).
- BenchmarkDotNet for fine-grained micro-bench.
- xUnit timing assertions for "must complete under N ms" gates.
- Reported in CI as artifacts; trends watched but not gated unless regression > 50%.

### 2.7 Accessibility (axe-core + manual)

- axe-core run on every Playwright UI test.
- Lighthouse a11y score ≥ 95 on key screens.
- Manual screen-reader pass per accessibility spec §8.

## 3. Per-spec test responsibilities

Every spec landing in 2026-05-10 declares tests in its §Tests section. Compliance gate: PR adding a new system MUST land tests at appropriate tier.

| Spec | Required tiers |
|---|---|
| design-system | Visual snapshots (E2E) |
| screens | Visual snapshots + flow (E2E) |
| websocket-protocol | Unit (envelope), integration (handshake), property (fuzz payloads) |
| save-format | Unit (migration), property (round-trip), integration (atomic write) |
| inventory-model | Unit (move matrix), property (zone invariants), snapshot (Bloom decay) |
| combat-state-extension | Unit (state transitions), snapshot (downed + resurrect) |
| combat-ai | Unit (decisions), snapshot (per archetype), property (determinism) |
| quest-mission | Unit (trigger eval), integration (full playthrough) |
| field-notes-journal | Unit (observation idempotency), integration (synergy → journal) |
| world-clock | Unit (turn advance), integration (faction tick) |
| faction-system | Unit (phase transition), integration (full 35-turn campaign) |
| dialogue-system | Unit (condition eval), integration (traverse all reachable nodes) |
| ability-system | Unit (formula), snapshot (10 abilities resolved) |
| damage-status | Unit (resistance + stack), snapshot (multi-status combat) |
| encounter-generation | Unit (weighting), property (filter correctness) |
| dungeon-assembly | Unit (deterministic), property (critical path exists) |
| progression-system | Unit (XP curve + level-up), integration (branch choice) |
| vendor-economy | Unit (buy/sell), property (gold conservation) |
| party-formation | Unit (targeting), snapshot (front-wipe back-expose) |
| crafting | Unit (validation), integration (downtime → craft) |
| rumor-system | Unit (truth state), integration (verification flow) |
| determinism-replay | Unit (RngTree), integration (replay reproduces) |
| state-diff-protocol | Property (diff round-trip) |
| error-recovery | Unit (revert on throw), integration (corrupt-save fallback) |
| content-pipeline | Unit (schema), property (graph cycles) |
| asset-pipeline | Determinism (same input → same bytes) |
| settings-keybinds | Unit (KDL round-trip), integration (rebind apply) |
| audio-architecture | Unit (cue picker, voice eviction) |
| photino-lifecycle | Manual |
| localization | Unit (lookup, pluralize), integration (locale switch) |
| onboarding | Unit (nudge dedup), E2E (first-run flow) |
| accessibility | axe-core, lighthouse, manual SR |
| dev-experience | Manual |
| modding | Unit (load order), integration (mod overlay) |
| telemetry | Unit (rotation + cap) |
| release-pipeline | Manual install per platform |

## 4. Tooling

| Tool | Role |
|---|---|
| xUnit | .NET unit + integration |
| Vitest | client unit |
| Playwright | client E2E |
| FsCheck | .NET property |
| fast-check | client property |
| BenchmarkDotNet | .NET microbench |
| axe-core | a11y validation |
| Lighthouse | a11y + perf score |
| Ajv | content schema validation in tools/content-pack |

All bundled into CI via `dotnet test` + `npm run test` + `npm run e2e`.

## 5. CI gating

### 5.1 Pre-merge required

- All unit + integration tests pass.
- New tests landed for changed code (judged in review).
- Linting: `dotnet format` + Prettier clean.
- Schema validation (content + KDL) passes.

### 5.2 Pre-merge optional (informational)

- Performance benchmarks (warn if regression > 20%).
- Visual snapshots (require human review if changed).
- Coverage delta (warn if dropped > 5%).

### 5.3 Pre-release

- Full E2E pass on all platforms (CI matrix).
- All snapshot tests current (no `[Skip]`s).
- A11y manual pass complete.
- Release checklist in `docs/release-checklist.md` ticked.

## 6. Snapshot management

Goldens live under `tests/snapshots/`:

```
tests/snapshots/
  combat/
    bone_archer_vs_front.json
    ...
  ai-traces/
    ranged_kiter_retreat.json
    ...
  visual/
    town_screen_1280.png
    town_screen_768.png
    ...
```

Update workflow:
1. Run `tools/snapshot-update --filter=combat/*` after intentional change.
2. Inspect diff.
3. Commit goldens with code change in same PR.
4. CI rejects PR if uncommitted updates needed.

Visual snapshots: pixel diff tolerance 0.1%. Larger diffs require explicit `--accept` in update tool.

## 7. Test data

`tests/fixtures/`:
- Sample content (small RPK with 3 dungeons, 5 enemies, 10 items, 3 missions).
- Sample saves at various phases (v1, v8, etc.).
- Replay files for synergy + AI scenarios.

Maintained by hand; CI builds tests against fixture pack.

## 8. Performance budgets (assertion targets)

| Operation | Budget | Spec |
|---|---|---|
| Unit test | < 100 ms | this spec |
| Integration test | < 1 s | this spec |
| E2E test | < 30 s | this spec |
| Server action dispatch | < 5 ms p99 | websocket-protocol |
| Combat turn resolve | < 500 ms | docs/design/09 |
| Dungeon load | < 3 s | docs/design/09 |
| State diff compute | < 2 ms p99 | state-diff-protocol |
| AI decision | < 1 ms | combat-ai |
| Save write | < 50 ms | save-format |
| Save load | < 200 ms | save-format |
| Frame render | < 16.6 ms (60 fps) | docs/design/09 |
| Audio cue start | < 30 ms after trigger | audio-architecture |

xUnit + BenchmarkDotNet enforce. Violations fail build only on >50% regression (avoid flake).

## 9. Test naming

`Method_Scenario_ExpectedBehavior` for xUnit:

```csharp
[Fact]
public void Move_FromBackpackToCacheInCombat_RejectsWithWrongMode() { ... }
```

Playwright tests use sentence titles:

```ts
test('player can enter dungeon and return to town', async () => { ... });
```

## 10. Flaky test policy

Any test marked `[Skip]` requires:
- Linked issue.
- Owner.
- Re-enable target date.

CI surfaces skip count in PR comment. Targets ≤ 3 active skips at any time.

Flake (intermittent pass/fail): mark, investigate, fix. Never `[Skip]` to mask.

## 11. Coverage

`coverlet` for .NET, `c8` for client. Reports uploaded to artifact + summarized in PR comment. No strict gate (gaming coverage % isn't quality), but monitored:

| Layer | Target |
|---|---|
| Domain (engine) | 80% |
| Server (host) | 60% |
| Client business logic | 70% |
| Client UI rendering | not tracked |
| Tools | 50% |
| Content / scripts | n/a |

## 12. Manual test matrix (pre-release)

Documented in `docs/test-matrix.md` (Phase 2). Covers:
- Each platform install + first launch.
- Each major flow end-to-end.
- Accessibility passes.
- Localization screens.
- Save round-trip through version migration.

## 13. Out of scope

- Mutation testing.
- Fuzz testing of save files (covered by error-recovery + property tests on parser).
- Load testing (single-player, not needed).
- Multi-day soak tests.
