import type { GameState, CombatState, CombatResult } from '$shared/types/game';

export function selectCombat(state: GameState): CombatState | undefined {
  return state.combat;
}

export function selectCombatResult(state: GameState): CombatResult | undefined {
  return state.combatResult;
}

export function selectIsCombatFinished(state: GameState): boolean {
  return state.combat?.isFinished ?? false;
}
