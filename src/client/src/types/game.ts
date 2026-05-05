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

export interface CombatResult {
  victory: boolean;
  xpGained: number;
  levelUps: string[];
  roundCount: number;
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
  combatResult?: CombatResult;
}

export type PlayerAction =
  | { type: 'move_forward' }
  | { type: 'turn_left' }
  | { type: 'turn_right' }
  | { type: 'generate_dungeon' }
  | { type: 'enter_dungeon'; dungeonType: string }
  | { type: 'combat_action'; action: CombatAction }
  | { type: 'flee_combat' }
  | { type: 'rest' }
  | { type: 'return_to_town' }
  | { type: 'save_game' };

export interface CombatAction {
  actorId: string;
  type: 'Attack' | 'Defend' | 'Wait' | 'UseAbility' | 'UseItem' | 'Flee';
  targetId?: string;
  abilityId?: string;
  itemId?: string;
}
