import type { GameState } from '$shared/types/game';

export function selectOverworld(state: GameState) {
  return state.overworld;
}

export function selectCurrentNodeId(state: GameState): string {
  return state.overworld?.currentNodeId ?? '';
}

export function selectTurns(state: GameState): number {
  return state.overworld?.turns ?? 0;
}

export function selectRoutes(state: GameState) {
  return state.overworld?.routes ?? [];
}
