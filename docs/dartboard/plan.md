# The Reach — Implementation Dartboard

> 靶心為的，矢之所向。知所先後，則近道矣。

## Current Status

| Phase | State | Blocking On |
|---|---|---|
| **Spec** | ✅ Complete | — |
| **Phase 1: Skeleton (G1)** | ✅ Complete | — |
| **Phase 1: Navigation (G2)** | ✅ Complete | — |
| **Phase 1: Characters (G3)** | 🎯 In Progress | G2 complete |
| **Phase 1: Combat (G4)** | 🟡 Partial | G3 |
| **Phase 1: Loop (G5)** | ⏳ Blocked | G3 + G4 |
| Phase 1.5 | ⏳ Blocked | Phase 1 complete |
| Phase 2 | ⏳ Blocked | Phase 1.5 complete |
| Phase 3 | ⏳ Blocked | Phase 2 complete |

---

## Group 1: Skeleton — Priority 🔴 P0

**Goal:** Something on screen. Every layer connected.

| # | Task | Owner | Est | Status |
|---|---|---|---|---|
| T1 | Photino shell boots | Backend | 2d | ✅ Done |
| T2 | WebSocket handshake | Both | 1d | ✅ Done |
| T3 | Empty Three.js scene | Frontend | 1d | ✅ Done |
| T4 | REST content endpoint | Both | 1d | ✅ Done |

**Acceptance:** Window shows gray floor + "Connected" dot.

---

## Group 2: Dungeon Navigation — Priority 🔴 P0

**Goal:** Walking through a dungeon feels good.

| # | Task | Owner | Est | Status |
|---|---|---|---|---|
| T5 | Grid movement (pure function) | Backend | 2d | ✅ Done |
| T6 | Room segment loader | Backend | 2d | ✅ Done |
| T7 | Dungeon assembler | Backend | 3d | ✅ Done |
| T8 | Three.js dungeon renderer | Frontend | 3d | ✅ Done |
| T9 | Movement input loop | Both | 2d | ✅ Done |
| T10 | Automap | Frontend | 2d | ✅ Done |

**Acceptance:** Procedurally assembled Broken Engine dungeon. Movement <50ms input-to-render. Automap tracks correctly.

---

## Group 3: Characters & Inventory — Priority 🟡 P1

**Goal:** Party creation and inventory management works.

| # | Task | Owner | Est | Status |
|---|---|---|---|---|
| T11 | Character data model | Backend | 1d | ✅ Done |
| T12 | Party system (4 chars, 2+2) | Backend | 1d | ✅ Done |
| T13 | Content: 4 classes | Content | 2d | 🎯 Next |
| T14 | Content: items & equipment | Content | 2d | 🟡 Ready |
| T15 | Inventory UI | Frontend | 3d | 🟡 Ready |
| T16 | Content pack compiler v1 | Tools | 2d | 🟡 Ready |

**Acceptance:** Create party of 4, equip, view stats. Content loads from binary pack.

---

## Group 4: Combat — Priority 🟡 P1

**Goal:** Combat feels tactical even without synergies.

| # | Task | Owner | Est | Status |
|---|---|---|---|---|
| T17 | Combat state machine | Backend | 3d | 🟡 Ready |
| T18 | Initiative system | Backend | 1d | 🟡 Ready |
| T19 | Action resolution | Backend | 3d | 🟡 Ready |
| T20 | Range bands | Backend | 1d | 🟡 Ready |
| T21 | Enemy data | Content | 1d | 🟡 Ready |
| T22 | Enemy AI | Backend | 2d | 🟡 Ready |
| T23 | Combat renderer | Frontend | 3d | 🟡 Ready |
| T24 | Combat UI | Frontend | 3d | 🟡 Ready |
| T25 | Snapshot test harness | Tests | 2d | 🟡 Ready |

**Acceptance:** 10 snapshot tests pass. Range bands create positioning decisions.

---

## Group 5: The Loop — Priority 🟢 P2

**Goal:** Playable game loop. 3 dungeons, town, save/load.

| # | Task | Owner | Est | Status |
|---|---|---|---|---|
| T26 | Encounter triggers | Backend | 1d | 🟢 Ready (needs G2) |
| T27 | Dungeon-combat-dungeon flow | Both | 2d | 🟢 Ready |
| T28 | Hub town (menu-based) | Frontend | 3d | 🟢 Ready |
| T29 | 3-dungeon questline | Content+Backend | 3d | 🟢 Ready |
| T30 | Leveling | Backend | 1d | 🟢 Ready |
| T31 | Save/load | Backend | 2d | 🟢 Ready |
| T32 | Playwright smoke tests | Tests | 2d | 🟢 Ready |

**Acceptance:** 1-2 hour playthrough. Save/load round-trips without data loss.

---

## Blockers & Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Photino cross-platform issues | Low | Test Linux build first, CI covers all |
| Content volume (Phase 2) | High | Spreadsheet pipeline, segment reuse |
| Type drift (C# ↔ TS) | Medium | Snapshot tests catch, codegen if >3 bugs |
| LLM output quality (Phase 3) | High | Validation layer + 3 retries + fallback |

---

## Next Actions (Immediate)

1. **Initialize .NET solution** — `src/engine/RPC.sln` with 4 projects
2. **Initialize frontend** — `npm create vite@latest src/client -- --template svelte-ts`
3. **Create first room segment JSON** — `content/segments/broken-engine-entrance.json`
4. **Wire T1-T4 end-to-end** — Prove the stack works

---

## Dependency Graph

```
G1 (Skeleton)
  └─► G2 (Navigation)
        ├─► G3 (Characters)
        │     └─► G4 (Combat)
        │           └─► G5 (Loop)
        └─► G5 (Loop) [automap feeds exploration XP]
```

---

## Decision Log

| Date | Decision | Context |
|---|---|---|
| — | JSON for all phases (C1) | Debug visibility > bandwidth |
| — | 4 chars Phase 1, 2+2 formation (C4) | Faster to playable, balance is throwaway |
| — | Tag-based immunity system (C9) | Generalizes to all resistances |
| — | JSON Schema + manual TS sync | Codegen deferred until drift causes bugs |
| — | Content hot-reload in dev | Skip .rpk in dev mode |
| — | Deterministic RNG with seed | Snapshot tests reproducible |

---

*Last updated: 2026-05-04*
