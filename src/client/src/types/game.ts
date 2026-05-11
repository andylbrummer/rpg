export interface Position {
  x: number;
  y: number;
}

export interface Player {
  x: number;
  y: number;
  facing: 'North' | 'East' | 'South' | 'West';
}

export type BorderType = 'None' | 'Wall' | 'Door' | 'SecretDoor';

export interface Tile {
  x: number;
  y: number;
  type: 'Empty' | 'Floor' | 'StairsUp' | 'StairsDown';
  north: BorderType;
  south: BorderType;
  east: BorderType;
  west: BorderType;
}

export interface CharacterStats {
  strength: number;
  dexterity: number;
  constitution: number;
  intelligence: number;
  willpower: number;
  maxHp: number;
  speed: number;
  accuracy: number;
  evade: number;
  power: number;
}

export interface Equipment {
  mainHand: string | null;
  offHand: string | null;
  armor: string | null;
  accessory1: string | null;
  accessory2: string | null;
}

export interface PartyMember {
  slot: number;
  name: string;
  classId: string;
  className: string;
  color: string;
  level: number;
  xp: number;
  hp: number;
  maxHp: number;
  row: number;
  alive: boolean;
  stats: CharacterStats;
  equipment: Equipment;
  knownAbilities: string[];
}

export interface Combatant {
  id: string;
  name: string;
  isPlayer: boolean;
  classId?: string;
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

export interface MissionOffer {
  id: string;
  title: string;
  description: string;
  minLevel: number;
  rewards: string[];
}

export interface VendorItem {
  itemId: string;
  name: string;
  price: number;
  quantity: number;
}

export interface TavernRecruit {
  id: string;
  name: string;
  classId: string;
  level: number;
  baseStats: {
    strength: number;
    dexterity: number;
    constitution: number;
    intelligence: number;
    willpower: number;
  };
  cost: number;
}

export interface TownState {
  currentTownId: string;
  availableMissions: MissionOffer[];
  vendorStock: VendorItem[];
  factionContacts: string[];
  tavernRoster: TavernRecruit[];
  viewedMissions: string[];
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
  town?: TownState;
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
  | { type: 'save_game' }
  | { type: 'reset_game' }
  | { type: 'swap_row'; slot: number }
  | { type: 'tavern_recruit'; targetId: string }
  | { type: 'mission_accept'; targetId: string }
  | { type: 'vendor_purchase'; targetId: string };

export interface CombatAction {
  actorId: string;
  type: 'Attack' | 'Defend' | 'Wait' | 'UseAbility' | 'UseItem' | 'Flee';
  targetId?: string;
  abilityId?: string;
  itemId?: string;
}
