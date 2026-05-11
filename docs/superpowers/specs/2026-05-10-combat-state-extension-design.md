# Combat State Extension — Design Spec
Date: 2026-05-10
Status: design — not yet implemented
Depends on: `docs/design/05-characters-and-classes.md` §Death & Resurrection; `docs/design/06-combat-system.md`
Scope: Downed / Stabilized / Dead states, Cauterist stabilize action, body recovery, resurrection economy hooks. Wire into engine, server, UI.

## 1. State model

Add `LifeState` to combatant + character:

```csharp
public enum LifeState {
    Healthy,    // Hp > 0
    Downed,     // Hp == 0, in combat or recently in combat, not stabilized
    Stabilized, // Hp == 0, stabilized this combat; survives combat end as alive @ 1 Hp
    Dead        // permanent until resurrected
}
```

Transitions:

```
Healthy ──(Hp→0)──► Downed
Downed ──(Cauterist Stabilize / healing item / heal spell)──► Stabilized
Downed ──(combat ends while Downed)──► Dead
Stabilized ──(combat ends)──► Healthy @ 1 Hp
Downed ──(damage taken while Downed)──► Downed (Hp stays 0; tick "fatal blow" — 3 hits while Downed → Dead immediately)
Dead ──(town resurrection)──► Healthy with permanent stat penalty (§4)
```

Downed character:
- Cannot act, takes no turn.
- Their portrait still displays in initiative timeline, dimmed + skull overlay.
- Counts toward party-wipe check (all Healthy+Stabilized == 0 → defeat).
- Drops their Backpack to the body if dies (see inventory spec §1.6).

Stabilized character:
- Cannot act in the remaining combat.
- HP stays 0 until combat ends. Then auto-set to 1.
- Cannot be re-downed during the same combat (treated as out of fight at 1 conceptual HP; further AoE damage is logged but ignored).

## 2. Stabilize action

Cauterist class action `stabilize`:
- Target: one Downed ally at any range band (no range restriction — flavor: cautery applied at distance via cautery whips? Simpler: cauterist must spend movement quick-action to close, abstracted in Phase 1 by free range).
- Cost: 1 Cautery Supply.
- Resolution: target moves Downed → Stabilized.
- Action type: standard action (not quick).
- Always succeeds when Cautery Supply available and target Downed.
- Does not restore HP; just prevents death.

Healing items + heal spells also stabilize if applied to Downed target:
- Item or spell heals for N HP. If target is Downed, the heal sets Hp = max(1, N - any overflow rules), state → Stabilized for rest of combat (the character does NOT re-enter action — they're still hors de combat for this fight per balance reasons. UI shows clearly).
- Exception: Cauterist (Surgeon) branch Phase 1.5 ability `revive` restores Downed to action this round with N HP. Spec'd for branch unlock.

Phase 1 only Bone Spear / Tithe Touch / Cauterize / etc. land as abilities. `stabilize` is added to Cauterist Phase-1 base kit (currently has `cauterize`, `scalpel_dance`). Add it.

## 3. Engine changes

`Combatant.cs` (existing): add `LifeState State` field, default `Healthy`.

`CombatEngine.cs` (existing): in damage resolution after `Hp = Math.Max(0, Hp - dmg)`:

```csharp
if (target.Hp == 0 && target.State == LifeState.Healthy) {
    target = target with { State = LifeState.Downed, FatalHits = 0 };
    log.Add(new CombatLogEntry($"{target.Name} is downed"));
    Fx.Emit("downed", target.Id);
}
else if (target.State == LifeState.Downed) {
    target = target with { FatalHits = target.FatalHits + 1 };
    if (target.FatalHits >= 3) {
        target = target with { State = LifeState.Dead };
        log.Add(new CombatLogEntry($"{target.Name} has died"));
        Fx.Emit("died", target.Id);
    }
}
```

Turn skip: in `Tick`, when picking next actor, skip combatants with `State != Healthy`. They stay in initiative for display but yield turn.

End-of-combat resolution in `GameState.SubmitCombatAction` (existing logic that copies combatant Hp back to Party): extend:

```csharp
foreach (var combatant in Combat.Combatants.Where(c => c.IsPlayer)) {
    var member = ...;
    var newHp = combatant.State switch {
        LifeState.Stabilized => 1,
        LifeState.Downed => 0,           // ends combat downed → dies next line
        LifeState.Dead => 0,
        _ => combatant.Hp
    };
    var newLifeState = combatant.State switch {
        LifeState.Downed => LifeState.Dead,     // unstabilized at end = dead
        LifeState.Stabilized => LifeState.Healthy,
        _ => combatant.State
    };
    Party.SetMember(index, member with {
        CurrentHp = newHp,
        LifeState = newLifeState,
        Xp = newXp,
        Backpack = (newLifeState == LifeState.Dead) ? Backpack.Empty : member.Backpack,
        DroppedBackpack = (newLifeState == LifeState.Dead) ? member.Backpack : null
    });
}
```

`CharacterState` (existing record): add `LifeState LifeState` (default `Healthy`) + `Backpack? DroppedBackpack` (gear left on the body for recovery).

`PartyState`: helper `bool IsWiped()` returns true when all members are `Dead` or `Downed`. (Downed during a wipe convert to Dead at combat resolution.)

## 4. Resurrection (town only)

New facility hook: **Bone Clerk** sub-tab of Sanctum (see screens spec §7).

Resurrection costs (from `docs/design/05`):

| Attempt # | Gold | Tithe Tokens | Penalty |
|---|---|---|---|
| 1 | 500 | 1 | -1 to random primary stat (permanent) |
| 2 | 1500 | 2 | -2 to random primary stat (permanent), branch advancement locked until next "Cleansing" Phase 2 quest |
| 3 | — | — | Cannot resurrect. Permanent death. |

Server action `resurrect`:

```
{ type: "resurrect", characterId: <guid> }
```

Validates:
- Character `LifeState == Dead`.
- Character `ResurrectionAttempts < 2`.
- Party has gold + tithe tokens to pay.

On success:
- Deduct gold + tithe tokens.
- `CharacterState` → `LifeState = Healthy`, `CurrentHp = max(1, MaxHp / 2)`, `ResurrectionAttempts++`.
- Apply random stat penalty: pick from `[STR, DEX, CON, INT, WIL]` weighted equal, decrement by 1 or 2 per attempt.
- Persist penalty as `StatPenalties` map on character so re-stat-block computes show the loss.

`CharacterState` additions:

```csharp
public LifeState LifeState { get; init; } = LifeState.Healthy;
public int ResurrectionAttempts { get; init; }
public Dictionary<string,int> StatPenalties { get; init; } = new();
public Backpack? DroppedBackpack { get; init; }   // gear on body if died in dungeon
public Position? DeathTile { get; init; }          // tile coords when died
```

## 5. Body recovery

When character dies in a dungeon:
- `DroppedBackpack` populated with their backpack at death moment, backpack cleared.
- `DeathTile` recorded.
- World places a `body_marker` interactable entity at that tile (Phase 1 stub: tile flag + automap icon).

When surviving party returns to that tile:
- Interact action opens a recovery modal showing the dropped items + a Recover All button (moves into Cache if space, else per-item move UI).
- Modal also has "Resurrect Hopes — Tag Body for Bone Clerk": flags the corpse so resurrection in town is possible. Without tagging, character cannot be resurrected (body lost to dungeon).

If party leaves dungeon without tagging:
- Character marked `BodyLost = true` on `CharacterState`. Bone Clerk refuses service. Replacement-only.
- Dropped backpack lost.

Phase 1: simplify — exiting dungeon auto-tags any party-death body (no separate action needed). Cache recovery still requires interaction. Phase 1.5 adds the tag-as-separate-step for tension.

## 6. UI

### 6.1 PartyStatusBar / PartyRail

Per-character portrait state styling:

| State | Visual |
|---|---|
| Healthy | normal |
| Downed | grayscale, skull icon overlay top-right, red slow-pulse border, HP bar 0/Max in red |
| Stabilized | sepia, bandage icon overlay, HP bar 0/Max in amber, "Stable" text under name |
| Dead | full desaturate, X-skull overlay, HP bar collapsed to thin red line, "Dead" text |

### 6.2 CombatOverlay initiative timeline

Downed combatants stay in timeline ordered position but at 40% opacity with skull overlay. Their "turn" auto-advances with brief 200ms flash. Stabilized = bandage overlay, 60% opacity. Dead = removed from timeline at the round boundary.

### 6.3 Targeting

When selecting target for Cauterist `stabilize`:
- Only Downed allies are valid; valid targets brass-highlighted.
- Healthy or Stabilized allies dimmed with "Not downed" tooltip.

When selecting target for damage abilities:
- Downed/Stabilized/Dead allies un-targetable.
- Enemies in Downed/Stabilized state n/a in Phase 1 (enemies skip the Downed state — they go straight from Hp>0 to Dead). Phase 2 may add enemy Downed for capture mechanics.

### 6.4 Flash alerts

- Downed → red vignette burst + screen shake 180ms + "Kael is DOWN" toast assertive `aria-live="assertive"`.
- Stabilized → green sparkle on portrait + "Kael stabilized" toast.
- Fatal blow on Downed → bad burst + "Kael has died" toast persistent until ack.
- Combat end with Downed → toast "Kael did not survive — body left in dungeon" + automap pin on death tile.
- Body tagged for recovery → brass shimmer on automap pin.

### 6.5 Town — Bone Clerk

Card list of all `Dead` characters with `BodyLost == false`. Each row: portrait (desaturated), name+class, attempt counter (e.g., "0/2 attempts"), cost (gold + tithe), penalty preview ("-1 to random stat"). Resurrect button disabled if can't afford. Confirm modal required (destructive — permanent stat loss).

If `BodyLost`: row shows "Body lost — cannot resurrect" with greyed out button.

If `ResurrectionAttempts >= 2`: row shows "Permanently dead — cannot resurrect" + Dismiss button (removes from list).

## 7. Save / load

`SaveSystem.cs`: bump `Version` to `"2"` (aligns with inventory spec). Persist:
- `LifeState`, `ResurrectionAttempts`, `StatPenalties`, `DroppedBackpack`, `DeathTile`, `BodyLost` per character
- Body markers in dungeon: persist as part of `Dungeon` state if dungeon is mid-run

Migration v1→v2: default all to `Healthy`, 0 attempts, empty penalties.

## 8. Tests

- xUnit unit: state transition matrix (Healthy↔Downed↔Stabilized↔Dead, all valid + invalid sources).
- xUnit snapshot: 6 combat scenarios — single down then heal, single down then end-combat death, party of 4 all downed, fatal-blow 3-strike rule, cauterist stabilize with 0 supplies (rejected), stabilized character takes AoE.
- xUnit integration: full flow — die in dungeon, return to body, recover items, return to town, resurrect, verify stat penalty applied.
- Playwright: party member downed during combat → screen alerts fire → flee combat → returns to dungeon with dead state → return to town → Bone Clerk shows entry → resurrect path.

## 9. Balance notes

- Cautery Supplies stack 12 per slot, Cauterist starts with 1 slot worth Phase 1. Encourages Stabilize budget.
- 3-hit fatal threshold on Downed prevents AoE one-shot from auto-killing a Downed teammate before any heal lands.
- Stabilized = 1 HP post-combat ensures heal/rest needed in town — keeps attrition pressure.

## 10. Out of scope

- Enemy Downed/Capture mechanic (Phase 2).
- Multi-resurrection Tithe Token economy beyond #1 and #2 (Phase 2 may add a #3 "Engine-bound" option at huge cost).
- Permadeath bench rescue expedition (Phase 3 ironman).
