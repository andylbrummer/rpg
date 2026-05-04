# 核之詳 — Engine Specification

> Engine 無 IO，純函數為魂。入狀態，出新狀態。

## 1. 遊戲狀態機

```csharp
public enum GameMode
{
    Menu,      // 標題/選單
    Town,      // 城鎮選單
    Overworld, // 大地圖
    Dungeon,   // 迷宮探索
    Combat,    // 戰鬥
    Cutscene,  // 劇情事件
}

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
    GameRandom Random   // 確定性 RNG wrapper
);
```

**不變量**：`Mode == Dungeon` 時 `Dungeon != null`，餘類推。

## 2. 迷宮系統

### 2.1 Grid Cell

```csharp
public enum WallType : byte { Solid, Open, Door, LockedDoor, Hidden, Destructible }

public record Cell(
    bool Floor,
    WallType North, WallType South, WallType East, WallType West,
    string SegmentId,
    string? EncounterId,
    string? InteractableId,
    bool Explored,
    bool Visible
);
```

- Grid 尺寸：最大 64×64（4096 cells），實際通常 24×32
- 記憶體：每 cell ≈ 48 bytes。32×32 grid = ~48KB
- 傳輸：首次進入發完整 grid，後續僅發 `Explored`/`Visible` delta

### 2.2 房間段 (Room Segment)

```csharp
public record RoomSegment(
    string Id,
    string Template,
    SizeCategory Size,      // Small, Medium, Large, Setpiece
    Connections Connections,  // N/S/E/W/U/D → Open/Closed/Hidden
    int[,] Grid,              // 0=void, 1=floor, 2=wall, 3=feature
    string[] Tags,
    string? PrimaryEncounter,
    LootEntry[] Loot,
    Dictionary<string, Interactable> Interactables
);
```

### 2.3 迷宮組裝器

算法：圖生成 → 段放置 → 連通驗證 → 遭遇/戰利品撒點

```csharp
public static class DungeonAssembler
{
    public static DungeonState Assemble(
        DungeonTemplate template,
        RoomSegment[] pool,
        int partyLevel,
        CampaignConfig config,
        GameRandom rng)
    {
        // 1. 生成連通圖（spanning tree + 迴路）
        // 2. 為每節點選段（依 template 權重、尺寸約束）
        // 3. 鋪設 grid，處理門/牆連接
        // 4. 驗證：起點可達終點，所有房間可達
        // 5. 放置遭遇（依難度曲線）
        // 6. 放置戰利品
    }
}
```

**關鍵路徑保證**：Dijkstra 驗證起點→終點可達。不可達則棄置重組（最多 10 次）。

## 3. 戰鬥系統

### 3.1 戰鬥空間

```csharp
public enum RangeBand { Melee, Short, Long }

public record Combatant(
    Guid Id,
    string Name,
    bool IsPlayer,
    int Hp, int MaxHp,
    int Speed,
    RangeBand Position,
    StatusEffect[] Statuses,
    string[] Immunities,    // tags: "necromantic", "fire", etc.
    string[] Resistances
);
```

### 3.2 回合狀態機

```
EnterCombat ──► RollInitiative ──► RoundStart ──► [TurnLoop] ──► RoundEnd ──► CheckWin/Lose ──► Exit
```

```csharp
public record CombatState(
    Combatant[] Combatants,
    int RoundNumber,
    Guid[] InitiativeOrder,
    int CurrentTurnIndex,
    HashSet<string> AbilitiesUsedThisRound,  // 協同檢測用
    CombatResult? Result,   // null = ongoing, Win/Lose/Fled
    RangeBand[][] EnemyGroups  // 每群敵人之位置
);
```

### 3.3 行動解析

```csharp
public static CombatState ResolveAction(
    CombatState state,
    CombatAction action,
    ContentLibrary content,
    GameRandom rng)
{
    // 1. 驗證行動合法性（距離、消耗、回合）
    // 2. 計算命中（acc vs evade + rng）
    // 3. 計算傷害（base * mod - armor）
    // 4. 應用狀態效果
    // 5. 協同引擎：檢查 AbilitiesUsedThisRound
    // 6. 檢查死亡/撤退
    // 7. 推進回合指標
}
```

### 3.4 協同引擎 (Synergy Engine)

```csharp
public static class SynergyEngine
{
    private static readonly Dictionary<(string, string), SynergyEffect> Registry;

    public static SynergyResult? Check(
        string abilityId,
        HashSet<string> usedThisRound)
    {
        // O(1) hash lookup。order-independent：
        // if Registry.ContainsKey((abilityId, other)) or ((other, abilityId))
        // 同能力雙發不觸發
    }
}
```

### 3.5 敵人 AI

| 型 | 行為樹 |
|---|---|
| Bloom | 隨機攻擊最近目標，30% 機率變異（獲得隨機 buff） |
| Soldier | 集中最弱 HP → 低於 30% HP 嘗試撤退至 Long band → 若無路則死戰 |
| Construct | 守衛模式：優先攻擊威脅最高者（damage output）→ 弱點暴露後改變行為 |

AI 為純函數：`CombatState → CombatAction`。可測、可快照。

## 4. 陣營聲望

```csharp
public record FactionReputations(
    int Bureau,      // -100 ~ +100
    int Convocation,
    int Compact,
    int Stillness,
    int Cartography
);
```

閾值：
- ≥25：解鎖陣營商人
- ≥40：解鎖血脈鎖、獨家招募
- ≤-25：敵對，野外遭遇變為戰鬥
- ≤-50：城鎮內遭驅逐

## 5. 確定性 RNG

```csharp
public sealed class GameRandom
{
    private readonly Random _rng;
    public int Seed { get; }

    public int Roll(int min, int max) => _rng.Next(min, max + 1);
    public double NextDouble() => _rng.NextDouble();
    public void Shuffle<T>(T[] list) { /* Fisher-Yates */ }

    public GameRandom Fork(string context) => new(Seed + context.GetHashCode());
}
```

- 生產：Seed 取自 `Guid.NewGuid().GetHashCode()`
- 測試：Seed 來自測試夾具，保證快照可複現
- 存檔：保存當前 Seed + 已消耗次數，載入後 `Skip(n)` 恢復狀態

## 6. 存檔系統

```csharp
public record SaveFile(
    int SchemaVersion,   // 從 Phase 1 起即設為 1
    DateTimeOffset SavedAt,
    GameState State,
    byte[] Checksum      // xxHash64(State bytes)
);
```

- Phase 1-2：版本不相容即拒載（開發期允許斷檔）
- Phase 3：加入遷移層 `SaveMigrator.Upgrade(v1 → v2 → v3)`
- 儲存路徑：`%LocalAppData%/TheReach/saves/{slot}.sav`
