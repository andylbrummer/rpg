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
  branchLevel6?: string;
  awaitingBranchChoice?: boolean;
  availableBranches?: string[];
  branchWarnings?: string[];
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
  isUnaccounted?: boolean;
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

export interface TownRumor {
  id: string;
  text: string;
  truthStatus: string;
  verified: boolean;
  verificationResult: boolean | null;
  relatedContentId: string | null;
  relatedFactionId: string | null;
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
  rumors: TownRumor[];
}

export interface OverworldNode {
  id: string;
  name: string;
  type: 'town' | 'dungeon_entrance';
  factionPresence?: string[];
}

export interface OverworldRoute {
  from: string;
  to: string;
  distance: number;
  dangerRating: number;
  terrain: string;
  status: 'Open' | 'Contested' | 'Blocked' | 'BloomAffected';
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

export interface HeatState {
  value: number;
  tier: string;
}

export interface EvidenceState {
  suspectedFaction?: string;
  canConfront: boolean;
  canAccuse: boolean;
  hasIrrefutableProof: boolean;
}

export interface DeadCharacter {
  id: string;
  name: string;
  classId: string;
  level: number;
  resurrectionAttempts: number;
  branchAdvancementLocked: boolean;
  resurrectionCost: number;
  titheTokenCost: number;
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
  heat?: HeatState;
  evidence?: EvidenceState;
  partyGold?: number;
  partyInventory?: string[];
  expeditionCache?: ComponentStack[];
  downtimeCompleted?: string[];
  deadCharacters?: DeadCharacter[];
  titheTokens?: number;
  campaignEnded?: boolean;
  isFragileState?: boolean;
  rescueExpedition?: {
    isActive: boolean;
    dungeonType: string;
    tpkLocation: { x: number; y: number };
  } | null;
  epilogue?: string | null;
  actionLog?: ActionLogEntry[];
  wildCardAlliance?: {
    status: string;
    factionId: string | null;
    turn: number;
  };
}

export interface AnalyticsData {
  campaignsStarted: number;
  campaignsCompleted: number;
  mastermindsExposed: number;
  schemesStopped: number;
  betrayals: number;
  totalTurns: number;
  totalDeaths: number;
  synergiesDiscovered: string[];
  classesPlayed: string[];
  branchesChosen: string[];
  optionalDungeonsUnlocked: string[];
}

export type { PlayerAction, CombatAction, ProtocolEnvelope, HelloPayload, ErrorPayload, HeartbeatPingPayload, HeartbeatPongPayload } from './protocol.gen';
