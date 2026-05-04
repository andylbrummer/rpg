export interface Position {
  x: number;
  y: number;
}

export interface Player {
  x: number;
  y: number;
  facing: 'North' | 'East' | 'South' | 'West';
}

export interface Tile {
  x: number;
  y: number;
  type: 'Empty' | 'Floor' | 'Wall' | 'Door' | 'SecretDoor' | 'StairsUp' | 'StairsDown';
}

export interface GameState {
  type: 'state';
  mode: 'Menu' | 'Exploration' | 'Combat' | 'Dialog';
  player: Player;
  tiles: Tile[];
  explored: Tile[];
  hasDungeon: boolean;
}

export type PlayerAction = 
  | { type: 'move_forward' }
  | { type: 'turn_left' }
  | { type: 'turn_right' }
  | { type: 'generate_dungeon' };
