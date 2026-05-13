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

export interface ComponentStack {
  itemId: string;
  count: number;
  maxStack: number;
}

export interface PartyMember {
  slot: number;
  id: string;
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
  branchChoice?: string;
  awaitingBranchChoice?: boolean;
  availableBranches?: string[];
  classAbilities?: Array<{ id: string; name: string; branch?: string }>;
  componentInventory: ComponentStack[];
}

export interface AbilityDef {
  id: string;
  name: string;
  range?: string;
  target?: string;
  requiredRow?: string;
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
  abilities?: AbilityDef[];
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
  repReward: number;
  factionId: string;
}

export interface VendorItem {
  itemId: string;
  name: string;
  price: number;
  quantity: number;
}

export interface FactionVendor {
  factionId: string;
  name: string;
  threshold: number;
  stock: VendorItem[];
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

export interface FactionContact {
  id: string;
  name: string;
  factionId: string;
  portrait: string;
}

export interface ActiveMission {
  id: string;
  title: string;
  description: string;
  repReward: number;
  factionId: string;
  status: string;
}

export interface TownState {
  currentTownId: string;
  availableMissions: MissionOffer[];
  vendorStock: VendorItem[];
  factionVendors: FactionVendor[];
  factionContacts: FactionContact[];
  tavernRoster: TavernRecruit[];
  viewedMissions: string[];
  questLog: ActiveMission[];
}

export interface OverworldNode {
  id: string;
  name: string;
  type: 'town' | 'dungeon_entrance';
}

export interface OverworldRoute {
  from: string;
  to: string;
  distance: number;
  dangerRating: number;
  terrain: string;
}

export interface OverworldState {
  currentNodeId: string;
  nodes: OverworldNode[];
  routes: OverworldRoute[];
  turns: number;
}

export interface TravelEncounter {
  id: string;
  name: string;
  resolutionType: 'combat' | 'stat_test' | 'dialogue';
  statName?: string;
  factionId?: string;
  reputationValue: number;
  hasSurpriseRound: boolean;
  priceTier: number;
  options?: string[];
}

export interface ActionLogEntry {
  turn: number;
  category: string;
  type: string;
  payload: Record<string, string>;
}

export interface EvidenceState {
  suspectedFaction?: string;
  canConfront: boolean;
  canAccuse: boolean;
  hasIrrefutableProof: boolean;
}

export interface GameState {
  type: 'state';
  mode: 'Menu' | 'Exploration' | 'Combat' | 'Dialog';
  player: Player;
  tiles: Tile[];
  explored: Tile[];
  hasDungeon: boolean;
  dungeonType?: string;
  party: PartyMember[];
  combat?: CombatState;
  combatResult?: CombatResult;
  town?: TownState;
  overworld?: OverworldState;
  travelEncounter?: TravelEncounter;
  reputation?: Record<string, number>;
  evidence?: EvidenceState;
  partyGold?: number;
  partyInventory?: string[];
  expeditionCache?: ComponentStack[];
  campaignEnded?: boolean;
  actionLog?: ActionLogEntry[];
}

export type PlayerAction =
  | { type: 'move_forward' }
  | { type: 'move_back' }
  | { type: 'strafe_left' }
  | { type: 'strafe_right' }
  | { type: 'turn_left' }
  | { type: 'turn_right' }
  | { type: 'cancel' }
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
  | { type: 'vendor_purchase'; targetId: string }
  | { type: 'travel'; targetId: string }
  | { type: 'resolve_travel_encounter'; targetId: string }
  | { type: 'set_reputation'; targetId: string; value: number }
  | { type: 'complete_mission'; targetId: string }
  | { type: 'fail_mission'; targetId: string }
  | { type: 'abandon_mission'; targetId: string }
  | { type: 'dialogue_choice'; targetId: string; value: number }
  | { type: 'branch_choose'; targetId: string; branch: string }
  | { type: 'transfer_to_cache'; slot: number; targetId: string; value: number }
  | { type: 'transfer_from_cache'; slot: number; targetId: string; value: number };

export interface CombatAction {
  actorId: string;
  type: 'Attack' | 'Defend' | 'Wait' | 'UseAbility' | 'UseItem' | 'Flee';
  targetId?: string;
  abilityId?: string;
  itemId?: string;
}

// Protocol Envelope v2 types

export interface ProtocolEnvelope {
  v: number;
  type: string;
  seq: number;
  ackSeq?: number;
  payload: Record<string, unknown>;
}

export interface HelloPayload {
  protocolVersion: number;
  sessionId: string;
}

export interface ErrorPayload {
  code: string;
  message: string;
  recoverable: boolean;
}

export interface HeartbeatPingPayload {
  pingSeq: number;
}

export interface HeartbeatPongPayload {
  pingSeq: number;
}
