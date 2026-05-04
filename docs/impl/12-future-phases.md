# 來者之規 — Future Phases Extension Roadmap

> Phase 1 築基已畢，此後之擴展，皆有其序。

## Phase 1.5：最小可行戰略

### 新增技術項目

| # | 項目 | 檔案 | 說明 |
|---|---|---|---|
| 33 | 隊伍擴至 6 人 | `RPC.Engine/Party/PartySystem.cs` | `Active` 長度 6，`Row` 3+3 |
| 34 | 排依賴能力 | `RPC.Engine/Combat/AbilityValidator.cs` | `ValidRanges` 加入 `Row` 條件 |
| 35 | Fieldwright + Inkblood | `content/classes/*.json` | 職業資料 |
| 36 | 編隊 UI | `client/src/ui/FormationPanel.svelte` | 拖曳指派前後排 |
| 37 | 6 人戰鬥渲染 | `client/src/renderer/CombatRenderer.ts` | 每距離帶 3 敵人槽位 |
| 38 | 聲望系統 | `RPC.Engine/Factions/Reputation.cs` | `FactionReputations` 已有，擴展閾值邏輯 |
| 39 | Bureau + Convocation | `content/factions/*.json` | 陣營資料、商人庫存 |
| 40 | 聲望商人 | `client/src/ui/town/FactionVendor.svelte` | 鎖定/解鎖 UI |
| 41 | 陣營 NPC | `client/src/ui/town/FactionContact.svelte` | 對話面板 |
| 42 | 聲望後果 | `RPC.Engine/Factions/ReputationConsequences.cs` | 任務完成 ±聲望 |
| 43 | 協同引擎 | `RPC.Engine/Combat/SynergyEngine.cs` | `HashSet` 檢測，已預埋 |
| 44 | 5 協同 | `content/synergies/*.json` | 能力對與效果 |
| 45 | 協同反饋 | `client/src/ui/combat/SynergyFX.svelte` | 閃光 + 音效 + Field Notes |
| 46 | Field Notes | `client/src/ui/journal/FieldNotes.svelte` | 已發現協同日誌 |
| 47 | 兩節點大地圖 | `RPC.Engine/Overworld/Overworld.cs` | 最簡節點圖 |
| 48 | 大地圖 UI | `client/src/ui/overworld/OverworldMap.svelte` | 節點點擊旅行 |
| 49 | 旅行遭遇 | `RPC.Engine/Overworld/TravelEncounters.cs` | 單一路線遭遇表 |
| 50 | 回合計數器 | `client/src/ui/hud/TurnCounter.svelte` | 15 回合顯示 |
| 51 | Bloom Site | `content/segments/bloom-site-*.json` | 新段、新主題、新敵人 |

### 1.5 資料模型變更

```csharp
// PartyState: Active 4 → 6
public record PartyState(CharacterState[] Active, FormationSlot[] Formation);

// SynergyDef 新增（內容層）
public record SynergyDef(string Id, string AbilityA, string AbilityB, Effect Bonus, string Hint);

// OverworldState（已有，啟用）
public record OverworldState(string CurrentNodeId, string[] DiscoveredNodes, Route[] Routes, int TurnsRemaining);
```

---

## Phase 2：戰略縱深

### 新增技術項目

| # | 項目 | 複雜度 | 說明 |
|---|---|---|---|
| 52 | Marcher + Ashmouth | 低 | 內容 |
| 53 | 分支系統 | 中 | Level 3/6 永久分支選擇，UI 彈窗 |
| 54 | 全 8 職業全分支 | 高 | ~20 分支，能力樹 |
| 55 | 等級上限 10 | 低 | 擴展經驗曲線 |
| 56 | 名冊/替補 | 中 | 12 人上限，替補無經驗，戰地晉升 50% |
| 57 | 名冊管理 UI | 中 | 替補視圖、拖曳交換 |
| 58 | 全 5 陣營 | 中 | 內容 |
| 59 | Compact 簽名機制 | 高 | 祖先談判、血脈鎖、家族檔案 |
| 60 | 陣營獨家招募 | 低 | 聲望閾值檢查 |
| 61 | 分支後備 | 中 | 未達聲望時給弱化版分支 |
| 62 | 聲望影響遭遇 | 中 | 巡邏敵意檢查 |
| 63 | 全協同庫 15-20 | 高 | 內容量 |
| 64 | 陣營士兵 AI | 中 | 撤退、協同、裝備匹配 |
| 65 | 死亡與復活 | 中 | 倒下→死亡→Bone Clerk 復活→永久死亡 |
| 66 | 元件背包 | 中 | 8 格/角色 + 12 格遠征儲備 |
| 67 | 元件背包 UI | 中 | 堆疊顯示、低量警告 |
| 68 | 停機時間 | 低 | 城鎮每角色一行動 |
| 69 | 停機時間 UI | 低 | 下拉選活動 |
| 70 | 完整節點大地圖 | 中 | 2-4 城鎮、路線屬性 |
| 71 | 大地圖渲染器 | 中 | 視覺節點圖、危險指示 |
| 72 | 完整旅行遭遇 | 中 | 每路線獨立表 |
| 73 | 路線狀態變化 | 中 | open→contested→blocked→bloom |
| 74 | 完整城鎮設施 | 中 | 酒館(謠言)、市場(物價)、公署、Bone Clerk |
| 75 | 謠言系統 | 中 | true/outdated/planted 標籤 |
| 76 | 關閉窗口信號 | 低 | 環境/直接/計時器三層 |
| 77 | 六擲系統 | 中 | 手寫配置檔 |
| 78 | 3 計謀 + 3 併發症 | 高 | 事件鏈內容 |
| 79 | 證據系統 | 中 | 每陣營計數器，閾值效果 |
| 80 | 主謀發現流 | 中 | 五管道，職業檢定 |
| 81 | 4 迷宮模板 | 高 | Broken Engine, Bloom Site, Contested Ruin, Underway |
| 82 | 回合計數 + 世界狀態 | 中 | 35 回合，三幕 |
| 83 | 野卡觸發 | 低 | 聲望閾值檢查 |
| 84 | 戰役快照測試 | 低 | 配置→斷言 |

### 2.1 TypeScript 型別同步（C8 決議）

Phase 2 開始評估 C# → TS codegen：

```bash
# 選項 A：NSwag
# 選項 B：自製 Roslyn Source Generator
# 選項 C：維持手動，直到 drift 造成實際 bug
```

**建議**：Phase 2 前中期仍手動。若單次 release 內發生 3 次以上型別不匹配 bug，則引入 codegen。

### 2.2 內容製作工作流（C7 決議）

```
Spreadsheet (items, encounters, stat blocks)
    ↓ export CSV
Python script → JSON
    ↓
Schema Validator (build-time)
    ↓
.rpk compiler
```

手寫內容（不進試算表）：
- Room segments（幾何數據過複雜）
- Evidence documents（敘事文本）
- NPC dialogue（對話樹）

---

## Phase 3：完全體

### 新增技術項目

| # | 項目 | 複雜度 | 說明 |
|---|---|---|---|
| 85 | 剩餘迷宮模板 | 高 | Boneyard, Sealed Vault, Settlement, Ossuary |
| 86 | 全 6 計謀 | 高 | 事件鏈 |
| 87 | 全 6 併發症 | 高 | 世界狀態修正 |
| 88 | 全協同 40-50 | 高 | 含秘密/環境/物品協同 |
| 89 | 全 NPC 庫 | 高 | 命名角色、對話集 |
| 90 | 證據文件庫 | 高 | 每計謀/主謀組合之證據鏈 |
| 91 | 環境敘事 | 中 | 物品描述、迷宮銘文 |
| 92 | 內容索引 | 低 | Build-time，tag + ID 對照 |
| 93 | LLM 生成提示 | 高 | 結構化 prompt，約 2000 tokens |
| 94 | 戰役配置 Schema | 中 | JSON Schema 驗證 LLM 輸出 |
| 95 | 驗證層 | 中 | 完整性、一致性、可完成性、陣營一致性 |
| 96 | 生成管道 | 中 | Six rolls → LLM → validate → config |
| 97 | 內容定址 | 低 | LLM 輸出 ID → Engine 解析 |
| 98 | 生成快照測試 | 低 | 已知組合 → assert 通過 |
| 99 | 陣營狀態機 | 中 | Investigating → Preparing → Executing |
| 100 | 編寫事件鏈 | 高 | 每陣營每轉換 2-3 事件 |
| 101 | 事件排程器 | 中 | 轉換時觸發 |
| 102 | 可見陣營行動 | 低 | 大地圖標記變化 |
| 103 | 陣營交互規則 | 中 | 兩陣營同 Executing 時解決 |
| 104 | 陣營 AI 快照 | 低 | 時間線 + 斷言 |
| 105 | Unaccounted 敵型 | 高 | 打斷/相位/重組/穿透/恐懼 |
| 106 | Unaccounted 渲染 | 高 | 視覺風格斷裂 |
| 107 | Unaccounted 音訊 | 高 | 錯誤音高、反轉音訊、寂靜 |
| 108 | 戰役尾聲 | 中 | 行動日誌 → 模板填空 / LLM 生成 |
| 109 | 鐵人模式 | 中 | 單存檔、死亡刪檔、救援遠征 |
| 110 | 秘密內容 | 高 | 隱藏協同、背叛路線 |
| 111 | 音訊系統 | 中 | 環境、戰鬥、陣營主題 |
| 112 | 光照/天氣 | 中 | 迷宮變化、大地圖天氣 |
| 113 | 分析鉤子 | 低 | 本地儲存統計，選擇性遙測 |
| 114 | 完整 Playwright | 中 | 擴展至戰役生成、陣營遭遇 |
| 115 | 性能分析 | 低 | 60fps / 500ms / 3s / 30s 標的驗證 |

### 3.1 LLM 整合架構（C10 決議）

```
┌─────────────────────────────────────┐
│  Campaign Generation Pipeline       │
│                                     │
│  SixRolls ──► PromptBuilder ──►    │
│  LLM Interface (pluggable)          │
│     ├── Claude (default)            │
│     ├── Local model (future)        │
│     └── Fallback: hand-authored     │
│         ──► Response ──►            │
│  JSON Parser ──► Schema Validator   │
│     ├── Pass ──► Content Addresser  │
│     └── Fail ──► Retry (max 3)      │
│         └── Max retry ──► Fallback  │
└─────────────────────────────────────┘
```

Prompt 結構：
```
You are a campaign arranger for a dungeon crawler RPG.
Given these six rolls, select and arrange content from the library.

Rolls:
- Patron: {patron}
- Threat: {threat}
- Mastermind: {mastermind}
- Scheme: {scheme}
- WildCard: {wildCard}
- Complication: {complication}

Available content (indexed by tag):
{contentIndexSummary}

Output STRICT JSON following this schema:
{schema}

Rules:
1. Reference ALL content by ID only.
2. Evidence count must be >= 10.
3. Critical path must be traversable.
4. No faction plays conflicting roles.
```

### 3.2 Epilogue 兩層（C12 決議）

```csharp
public string GenerateEpilogue(CampaignLog log, bool llmAvailable)
{
    // Tier 1: 模板填空，永遠可用
    var template = Content.GetTemplate("epilogue-base");
    var filled = template
        .Replace("{mastermind}", log.MastermindFaction)
        .Replace("{scheme_result}", log.SchemeSucceeded ? "succeeded" : "failed")
        .Replace("{saved_towns}", string.Join(", ", log.SavedTowns))
        .Replace("{casualties}", log.PartyCasualties.ToString());

    if (!llmAvailable) return filled;

    // Tier 2: LLM 潤色（若可用）
    var prompt = $"Refine this campaign summary into 2-3 paragraphs: {filled}";
    var enhanced = _llm.Generate(prompt, maxTokens: 300);
    return enhanced ?? filled; // LLM 失敗回退模板
}
```

### 3.3 Unaccounted 技術實現

| 規則破壞 | 實現方式 | 對策 |
|---|---|---|
| Interrupt | 狀態機插入額外回合，不經 initiative order | Warden Shield Wall 仍生效 |
| Phase | `Combatant.Position` 可在任意 resolve 後突變 | Stalker 任意距離帶瞄準 |
| Reassemble | `OnDeath` 觸發 2 回合後 timer，生成新 Combatant | Cauterist fire 永久阻止（設 `preventReassemble` flag） |
| Reach Through | 攻擊目標選擇器跳過前排檢查 | Animator 召喚物可被選為目標 |
| Dread | `StatusEffect.Duration = int.MaxValue`，僅 `OnSourceKilled` 清除 | Agitator War Cry 驅散 |

---

## 風險矩陣

| 風險 | 階段 | 影響 | 緩解 |
|---|---|---|---|
| LLM 輸出品質差 | 3 | 高 | 驗證層 + 3 次重試 + 手寫後備 |
| 內容量爆炸 | 2-3 | 高 | 試算表管道 + 段重複利用 + 優先 setpiece |
| Unaccounted 平衡 | 3 | 中 | 快照測試多隊形組合 |
| TypeScript 型別漂移 | 2 | 中 | 整合測試捕捉，適時引入 codegen |
| Photino 跨平台問題 | 1 | 低 | Linux/macOS CI 建置驗證 |
