# Deep Validation Report: Personal/rpg Loop

**Date**: 2026-05-16
**Phase**: Phase 3: Engine Refactor — Tasks 105 + 109
**Branch**: `build/kimi`

---

## Test Matrix

| Suite | Result | Notes |
|---|---|---|
| xUnit (engine) | **761 / 761 pass** | No failures |
| Playwright e2e (smoke) | **3 / 3 pass** | Clean |
| Playwright e2e (combat+loop+town) | **16 / 16 pass** | Clean |
| Playwright e2e (g1+g2+g3) | **8 / 9 pass** | 1 pre-existing flaky (g2-navigation) |
| Playwright e2e (g7+g9+setpiece+synergy) | **8 / 10 pass** | 2 pre-existing failures (synergy-feedback, setpiece-encounter) |
| TypeScript (`tsc --noEmit`) | **Clean** | 0 errors |
| Client build (`npm run build`) | **Clean** | 1.77s, chunk size warning only |
| Content-pack CLI | **Valid** | manifest + synergy map + .rpk generated (166,855 bytes) |

## Task 105: The Unaccounted — Complete

### Rule-Breaking Behaviors (5/5 passing)

| Behavior | Test | Status |
|---|---|---|
| Interrupt | `Interrupt_Unaccounted_Gets_Extra_Turns` | ✅ Pass |
| Phase | `Phase_Unaccounted_Changes_Row_Before_Acting` | ✅ Pass |
| ReachThrough | `ReachThrough_Can_Target_Back_Row` | ✅ Pass |
| Dread | `Dread_Applied_On_Unaccounted_Attack` | ✅ Pass |
| Reassemble | `Reassemble_Two_Dead_Unaccounted_Create_New` | ✅ Pass |

### Counter Classes (5/5 passing)

| Counter | Test | Mechanic |
|---|---|---|
| Shield Wall | `ShieldWall_Blocks_Unaccounted_Interrupt` | `shield_wall` status on any ally blocks interrupt insertion |
| Burned | `Burned_Corpse_Does_Not_Reassemble` | Fire-tagged abilities apply `burned` to Unaccounted on kill; burned corpses excluded from reassembly |
| War Cry | `WarCry_Dispels_Dread` | `war_cry` ability clears `dread` from all allies on resolve |
| Summon Absorb | `Summon_Absorbs_BackRow_Targeting` | Unaccounted reach-through redirects to summoned unit when present |
| Stalker | (implicit) | Engine does not enforce player targeting range; all classes can target any row |

## Task 109: Ironman Mode — Complete (Minimal Viable)

### Features Implemented

| Feature | Test | Status |
|---|---|---|
| Flag persistence | `Ironman_Flag_Saved_And_Loaded` | ✅ Pass |
| Default false | `Ironman_Flag_Defaults_To_False` | ✅ Pass |
| Auto-save | `AutoSave_On_State_Change_When_Ironman` | ✅ Pass |
| No auto-save when off | `No_AutoSave_When_Not_Ironman` | ✅ Pass |
| TPK deletion | `TPK_Deletes_Save_When_Ironman` | ✅ Pass |
| TPK no-delete when off | `TPK_Does_Not_Delete_Save_When_Not_Ironman` | ✅ Pass |

### Implementation Details

- `IsIronman` flag added to `SaveData` (backward compatible, defaults to `false`)
- `GameState.SavePath` property added for configurable save location (defaults to `SaveSystem.SavePath`)
- `GameCommandHandler` auto-saves after every state-changing action when `IsIronman = true`
- `CombatService` deletes save file on TPK (`allPlayersDead && IsIronman`)
- `SetIronmanCommand` added to command protocol

### Deferred to Later

- Bench rescue expedition (3 bench characters enter dungeon to recover TPK party)
- Fragile-state warning UI (< 3 bench chars + turn > 25)

## Infrastructure Fixes

- **e2e fixture save cleanup**: `tests/e2e/specs/fixtures.ts` now deletes `~/.local/share/TheReach/save.json` before starting the server per worker, preventing turn-count accumulation across test runs.

## Files Changed (This Session)

- **Modified**: `src/engine/RPC.Engine/Combat/CombatEngine.cs` — Unaccounted behaviors + counter logic
- **Modified**: `src/engine/RPC.Engine/Combat/Combatant.cs` — `DeadUnaccounted.Burned` flag
- **Modified**: `src/engine/RPC.Engine/Combat/CombatService.cs` — Ironman TPK deletion
- **Modified**: `src/engine/RPC.Engine/Commands/GameCommandHandler.cs` — Ironman auto-save + `SetIronmanCommand`
- **Modified**: `src/engine/RPC.Engine/Commands/CommandTypes.cs` — `SetIronmanCommand`
- **Modified**: `src/engine/RPC.Engine/GameState.cs` — `IsIronman` + `SavePath`
- **Modified**: `src/engine/RPC.Engine/Save/SaveData.cs` — `IsIronman`
- **Modified**: `src/engine/RPC.Engine/Save/SaveRestorer.cs` — `RestoreIronman`
- **Modified**: `src/engine/RPC.Engine/Save/SaveSystem.cs` — Save/load `IsIronman`
- **Modified**: `tests/e2e/specs/fixtures.ts` — Save-file cleanup
- **New**: `src/engine/RPC.Tests/IronmanTests.cs` — 6 Ironman tests
- **Modified**: `src/engine/RPC.Tests/UnaccountedTests.cs` — 10 Unaccounted tests

## Regressions

**None identified.**

## Next Actions

1. **Task 106**: Unaccounted renderer (client-side visual glitches, chromatic aberration)
2. **Task 107**: Unaccounted audio (silent movement, reversed audio, wrong pitch)
3. **Task 109 cont.**: Bench rescue expedition + fragile-state warning
