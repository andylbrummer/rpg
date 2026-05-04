# 模之詳 — Data Models

> 型定則亂不生。C# 為核之型，TS 為形之鏡。

## C# Core Models (RPC.Engine)

```csharp
// ─── Shared Primitives ───
public readonly record struct Vec2(int X, int Y);
public enum Direction { North, East, South, West }
public enum GameMode { Menu, Town, Overworld, Dungeon, Combat, Cutscene }

// ─── Game State ───
public record GameState(
    Guid CampaignId,
    GameMode Mode,
    int TurnCounter,
    PartyState Party,
    RosterState Roster,
    DungeonState? Dungeon,
    CombatState? Combat,
    TownState? Town,
    OverworldState? Overworld,
    FactionReputations Reputations,
    CampaignConfig Config,
    RandomState RngState
);

// ─── Party & Characters ───
public record PartyState(
    CharacterState[] Active,      // length 4 (Phase 1) or 6 (1.5+)
    FormationSlot[] Formation     // 3 front + 3 back
);

public record FormationSlot(
    Guid CharacterId,
    Row Row
);

public enum Row { Front, Back }

public record CharacterState(
    Guid Id,
    string Name,
    string ClassId,
    string? BranchId,
    int Level,
    int Xp,
    int Hp,
    int MaxHp,
    Stats BaseStats,
    Stats EffectiveStats,   // after gear/buffs
    Equipment Equipment,
    ItemStack[] Inventory,  // 8 slots
    AbilityId[] KnownAbilities,
    StatusEffect[] Statuses,
    bool IsDead,
    int DeathCount          // 0-2, 3 = permanent
);

public record Stats(
    int Might,      // melee dmg, hp
    int Finesse,    // accuracy, evasion
    int Speed,      // initiative
    int Resolve,    // status resist
    int Wit         // ability potency
);

public record Equipment(
    string? Head,
    string? Body,
    string? MainHand,
    string? OffHand,
    string? Accessory
);

public record ItemStack(string ItemId, int Count);

// ─── Roster ───
public record RosterState(
    CharacterState[] Bench,       // up to 12 total
    int MaxSize
);

// ─── Dungeon ───
public record DungeonState(
    string TemplateId,
    string DungeonId,
    Cell[,] Grid,
    Vec2 PlayerPosition,
    Direction PlayerFacing,
    HashSet<string> DefeatedEncounters,
    HashSet<string> InteractedObjects,
    int DungeonLevel   // scales encounters
);

public record Cell(
    bool Floor,
    WallType North, WallType South, WallType East, WallType West,
    string SegmentId,
    string? EncounterId,
    string? InteractableId,
    bool Explored,
    bool Visible
);

public enum WallType : byte { Solid, Open, Door, LockedDoor, Hidden, Destructible }

// ─── Combat ───
public record CombatState(
    Combatant[] Combatants,
    int RoundNumber,
    Guid[] InitiativeOrder,
    int CurrentTurnIndex,
    HashSet<string> AbilitiesUsedThisRound,
    CombatResult? Result,
    CombatLogEntry[] Log,
    Vec2? RetreatDirection   // valid if Result == Fled
);

public record Combatant(
    Guid Id,
    string Name,
    bool IsPlayer,
    int Hp, int MaxHp,
    int Speed,
    RangeBand Position,
    StatusEffect[] Statuses,
    string[] Immunities,
    string[] Resistances,
    string? AiProfile,
    string[] AvailableAbilities
);

public enum RangeBand { Melee, Short, Long }
public enum CombatResult { Ongoing, Win, Lose, Fled }

public record CombatLogEntry(
    int Round,
    Guid ActorId,
    string ActionType,
    Guid? TargetId,
    int Damage,
    bool WasCrit,
    string? SynergyTriggered
);

public record CombatAction(
    Guid ActorId,
    ActionType Type,
    Guid? TargetId,
    string? AbilityId,
    int? ItemSlot
);

public enum ActionType { Attack, Defend, Cast, UseItem, Flee, Wait }

// ─── Abilities & Status ───
public record AbilityDef(
    string Id,
    string Name,
    string Description,
    ActionType ActionType,
    TargetType Target,
    RangeBand[] ValidRanges,
    ComponentCost[] Costs,
    Effect[] Effects,
    string[] Tags,         // "necromantic", "fire", "buff", etc.
    int Cooldown
);

public record ComponentCost(string ComponentId, int Amount);

public record Effect(
    string Type,           // "damage", "heal", "buff", "debuff", "move", "summon"
    int Potency,
    string? StatusId,
    int Duration
);

public record StatusEffect(
    string Id,
    string Name,
    int RemainingDuration,
    int Potency,
    string[] Tags
);

// ─── Factions ───
public record FactionReputations(
    int Bureau,
    int Convocation,
    int Compact,
    int Stillness,
    int Cartography
)
{
    public int this[string faction] => faction switch {
        "bureau" => Bureau,
        "convocation" => Convocation,
        "compact" => Compact,
        "stillness" => Stillness,
        "cartography" => Cartography,
        _ => 0
    };
}

// ─── Town ───
public record TownState(
    string TownId,
    string Name,
    string[] PresentFactions,
    string EngineType,
    Vendor[] Vendors,
    Recruit[] AvailableRecruits,
    Mission[] AvailableMissions,
    string[] Rumors
);

public record Vendor(
    string Id,
    string Name,
    string? FactionId,
    int? RepThreshold,
    ShopItem[] Stock
);

public record Recruit(
    Guid Id,
    string Name,
    string ClassId,
    string? BranchId,
    int Level,
    int Cost,
    string? RequiredFaction,
    int? RequiredRep
);

// ─── Overworld ───
public record OverworldState(
    string CurrentNodeId,
    string[] DiscoveredNodes,
    Route[] Routes,
    int TurnsRemaining
);

public record Route(
    string From,
    string To,
    int Distance,
    int Danger,
    string Terrain,
    RouteStatus Status
);

public enum RouteStatus { Open, Contested, Blocked, BloomAffected }

// ─── Campaign Config ───
public record CampaignConfig(
    CampaignRolls Rolls,
    string[] DungeonSequence,
    Dictionary<string, DungeonAssignment> DungeonAssignments,
    Dictionary<string, TownConfig> TownConfigs,
    Dictionary<string, FactionTimeline> FactionTimelines,
    WildcardConfig? Wildcard
);

public record CampaignRolls(
    string Patron,
    string Threat,
    string Mastermind,
    string Scheme,
    string WildCard,
    string Complication
);

public record DungeonAssignment(
    string[] FactionPresence,
    EvidenceSlot[] EvidenceSlots,
    Dictionary<string, string> NpcCasting,
    int[] EncounterEscalation
);

public record EvidenceSlot(string SegmentTag, string EvidenceId);

public record FactionTimeline(int InvestigatingEnd, int PreparingEnd, string[] Events);

public record WildcardConfig(string DungeonId, int[] TurnWindow, int RepThreshold);
```

## TypeScript Mirror Types

```typescript
// types/game.ts — 手動維護，與 C# 同名同形

export type Direction = 'North' | 'East' | 'South' | 'West';
export type GameMode = 'Menu' | 'Town' | 'Overworld' | 'Dungeon' | 'Combat' | 'Cutscene';
export type WallType = 'Solid' | 'Open' | 'Door' | 'LockedDoor' | 'Hidden' | 'Destructible';
export type RangeBand = 'Melee' | 'Short' | 'Long';
export type Row = 'Front' | 'Back';
export type CombatResult = 'Ongoing' | 'Win' | 'Lose' | 'Fled';
export type RouteStatus = 'Open' | 'Contested' | 'Blocked' | 'BloomAffected';

export interface GameState {
  campaignId: string;
  mode: GameMode;
  turnCounter: number;
  party: PartyState;
  roster: RosterState;
  dungeon: DungeonState | null;
  combat: CombatState | null;
  town: TownState | null;
  overworld: OverworldState | null;
  reputations: FactionReputations;
  config: CampaignConfig;
}

export interface PartyState {
  active: CharacterState[];
  formation: FormationSlot[];
}

export interface FormationSlot {
  characterId: string;
  row: Row;
}

export interface CharacterState {
  id: string;
  name: string;
  classId: string;
  branchId: string | null;
  level: number;
  xp: number;
  hp: number;
  maxHp: number;
  baseStats: Stats;
  effectiveStats: Stats;
  equipment: Equipment;
  inventory: ItemStack[];
  knownAbilities: string[];
  statuses: StatusEffect[];
  isDead: boolean;
  deathCount: number;
}

export interface Stats {
  might: number;
  finesse: number;
  speed: number;
  resolve: number;
  wit: number;
}

export interface Equipment {
  head: string | null;
  body: string | null;
  mainHand: string | null;
  offHand: string | null;
  accessory: string | null;
}

export interface ItemStack {
  itemId: string;
  count: number;
}

export interface RosterState {
  bench: CharacterState[];
  maxSize: number;
}

export interface DungeonState {
  templateId: string;
  dungeonId: string;
  grid: Cell[][];
  playerPosition: Vec2;
  playerFacing: Direction;
  defeatedEncounters: string[];
  interactedObjects: string[];
  dungeonLevel: number;
}

export interface Cell {
  floor: boolean;
  north: WallType;
  south: WallType;
  east: WallType;
  west: WallType;
  segmentId: string;
  encounterId: string | null;
  interactableId: string | null;
  explored: boolean;
  visible: boolean;
}

export interface CombatState {
  combatants: Combatant[];
  roundNumber: number;
  initiativeOrder: string[];  // UUIDs
  currentTurnIndex: number;
  abilitiesUsedThisRound: string[];
  result: CombatResult | null;
  log: CombatLogEntry[];
  retreatDirection: Vec2 | null;
}

export interface Combatant {
  id: string;
  name: string;
  isPlayer: boolean;
  hp: number;
  maxHp: number;
  speed: number;
  position: RangeBand;
  statuses: StatusEffect[];
  immunities: string[];
  resistances: string[];
  aiProfile: string | null;
  availableAbilities: string[];
}

export interface CombatLogEntry {
  round: number;
  actorId: string;
  actionType: string;
  targetId: string | null;
  damage: number;
  wasCrit: boolean;
  synergyTriggered: string | null;
}

export interface CombatAction {
  actorId: string;
  type: 'Attack' | 'Defend' | 'Cast' | 'UseItem' | 'Flee' | 'Wait';
  targetId: string | null;
  abilityId: string | null;
  itemSlot: number | null;
}

export interface StatusEffect {
  id: string;
  name: string;
  remainingDuration: number;
  potency: number;
  tags: string[];
}

export interface FactionReputations {
  bureau: number;
  convocation: number;
  compact: number;
  stillness: number;
  cartography: number;
}

export interface TownState {
  townId: string;
  name: string;
  presentFactions: string[];
  engineType: string;
  vendors: Vendor[];
  availableRecruits: Recruit[];
  availableMissions: Mission[];
  rumors: string[];
}

export interface Vendor {
  id: string;
  name: string;
  factionId: string | null;
  repThreshold: number | null;
  stock: ShopItem[];
}

export interface Recruit {
  id: string;
  name: string;
  classId: string;
  branchId: string | null;
  level: number;
  cost: number;
  requiredFaction: string | null;
  requiredRep: number | null;
}

export interface OverworldState {
  currentNodeId: string;
  discoveredNodes: string[];
  routes: Route[];
  turnsRemaining: number;
}

export interface Route {
  from: string;
  to: string;
  distance: number;
  danger: number;
  terrain: string;
  status: RouteStatus;
}

export interface CampaignConfig {
  rolls: CampaignRolls;
  dungeonSequence: string[];
  dungeonAssignments: Record<string, DungeonAssignment>;
  townConfigs: Record<string, TownConfig>;
  factionTimelines: Record<string, FactionTimeline>;
  wildcard: WildcardConfig | null;
}

export interface CampaignRolls {
  patron: string;
  threat: string;
  mastermind: string;
  scheme: string;
  wildCard: string;
  complication: string;
}

export interface DungeonAssignment {
  factionPresence: string[];
  evidenceSlots: EvidenceSlot[];
  npcCasting: Record<string, string>;
  encounterEscalation: number[];
}

export interface EvidenceSlot {
  segmentTag: string;
  evidenceId: string;
}

export interface FactionTimeline {
  investigatingEnd: number;
  preparingEnd: number;
  events: string[];
}

export interface WildcardConfig {
  dungeon: string;
  turnWindow: [number, number];
  repThreshold: number;
}

export interface Vec2 { x: number; y: number; }
```

## 同步守則

- C# 為主，TS 為鏡
- Phase 1-2：手動同步， drift 由整合測試捕捉
- Phase 2 末：評估 C# → TS codegen（NSwag / custom Roslyn）
- 更名、增刪欄位時，兩檔同改，PR 標記 `[TYPE-SYNC]`
