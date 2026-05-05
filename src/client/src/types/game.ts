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

export interface PartyMember {
  slot: number;
  name: string;
  classId: string;
  level: number;
  hp: number;
  maxHp: number;
  row: number;
  alive: boolean;
}

export interface Combatant {
  id: string;
  name: string;
  isPlayer: boolean;
  hp: number;
  maxHp: number;
  speed: number;
  row: number;
  alive: boolean;
  isCurrent: boolean;
}

export interface CombatLogEntry {
  actor: string;
  message: string;
  round: number;
}

export interface CombatState {
  phase: string;
  round: number;
  combatants: Combatant[];
  initiativeOrder: string[];
  currentTurnIndex: number;
  log: CombatLogEntry[];
  isFinished: boolean;
}

export interface GameState {
  type: 'state';
  mode: 'Menu' | 'Exploration' | 'Combat' | 'Dialog';
  player: Player;
  tiles: Tile[];
  explored: Tile[];
  hasDungeon: boolean;
  party: PartyMember[];
  combat?: CombatState;
}

export type PlayerAction =
  | { type: 'move_forward' }
  | { type: 'turn_left' }
  | { type: 'turn_right' }
  | { type: 'generate_dungeon' }
  | { type: 'combat_action'; action: CombatAction }
  | { type: 'flee_combat' };

export interface CombatAction {
  actorId: string;
  type: 'Attack' | 'Defend' | 'Wait' | 'UseAbility' | 'UseItem' | 'Flee';
  targetId?: string;
  abilityId?: string;
  itemId?: string;
}
