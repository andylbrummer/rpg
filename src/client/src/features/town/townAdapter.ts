import type { GameState } from '$shared/types/game';

export function selectTown(state: GameState) {
  return state.town;
}

export function selectAvailableMissions(state: GameState) {
  return state.town?.availableMissions ?? [];
}

export function selectVendorStock(state: GameState) {
  return state.town?.vendorStock ?? [];
}

export function selectTavernRoster(state: GameState) {
  return state.town?.tavernRoster ?? [];
}
