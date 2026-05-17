import type { GameState, Tile } from '$shared/types/game';

export function selectPlayerPosition(state: GameState) {
  return state.player;
}

export function selectVisibleTiles(state: GameState): Tile[] {
  return state.tiles ?? [];
}

export function selectExploredTiles(state: GameState): Tile[] {
  return state.explored ?? [];
}

export function selectHasDungeon(state: GameState): boolean {
  return state.hasDungeon;
}

export function selectDungeonType(state: GameState): string | undefined {
  return state.dungeonType;
}
