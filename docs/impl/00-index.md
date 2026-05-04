# 實作規格總目 — Implementation Spec Index

> 綱舉目張。按序讀之，則全局在胸。

## 閱讀順序

| 序 | 檔名 | 內容 | 頁 |
|---|---|---|---|
| 00 | `00-index.md` | 此目錄 | — |
| 01 | `01-architecture.md` | 三層架構、專案樹、建置流、效能標的 | 1 |
| 02 | `02-protocol.md` | WS/REST 協議、幀結構、狀態同步、錯誤碼 | 2 |
| 03 | `03-engine.md` | .NET Engine：狀態機、迷宮、戰鬥、RNG、存檔 | 3 |
| 04 | `04-client.md` | 前端：Three.js、Svelte、輸入、網路、快取 | 4 |
| 05 | `05-content.md` | 內容管道：JSON Schema、.rpk 格式、熱重載 | 5 |
| 06 | `06-data-models.md` | C# / TypeScript 型別鏡像，全定義 | 6 |
| 07 | `07-phase1-roadmap.md` | Phase 1 詳細任務、驗收條件、檔案路徑 | 7 |
| 08 | `08-combat-spec.md` | 戰鬥公式、能力結構、敵人 AI、狀態、逃跑 | 8 |
| 09 | `09-dungeon-spec.md` | 迷宮組裝算法、段格式、連通驗證、遭遇放置 | 9 |
| 10 | `10-testing-spec.md` | 測試金字塔、快照、E2E、CI、性能基準 | 10 |
| 11 | `11-host-spec.md` | Photino 啟動、視窗生命周期、開發/發布模式 | 11 |
| 12 | `12-future-phases.md` | Phase 1.5 / 2 / 3 技術擴展路線 | 12 |

## 快速定位

**若你負責後端**：01 → 02 → 03 → 06 → 08 → 09 → 10
**若你負責前端**：01 → 02 → 04 → 06 → 07
**若你負責內容**：05 → 07 → 08 → 09
**若你負責測試**：10 → 07 → 08

## 與設計文件之對應

| 設計文件 | 對應實作規格 |
|---|---|
| `design/01-vision.md` | 01-architecture（目標平台、效能標的） |
| `design/02-world.md` | 03-engine（Bloom 互動、世界狀態） |
| `design/03-narrative.md` | 03-engine（CampaignConfig、證據系統） |
| `design/04-factions.md` | 03-engine（Reputation、陣營 AI） |
| `design/05-characters.md` | 06-data-models（CharacterState、Branch） |
| `design/06-combat.md` | 08-combat-spec（公式、協同、Unaccounted） |
| `design/07-dungeon.md` | 09-dungeon-spec（段格式、組裝算法） |
| `design/08-overworld.md` | 03-engine（OverworldState、Route） |
| `design/09-mvp.md` | 07-phase1-roadmap（逐任務展開） |
| `plans/00-overview.md` | 01-architecture（技術棧對照） |
| `plans/01-phase1.md` | 07-phase1-roadmap（全部展開） |
| `plans/02-phase1.5.md` | 12-future-phases（擴展路線） |
| `plans/03-phase2.md` | 12-future-phases |
| `plans/04-phase3.md` | 12-future-phases |
| `plans/05-complications.md` | 各規格中「C1-C12 決議」散見 |

## 狀態

- [x] 架構定稿
- [x] 協議定稿
- [x] Engine API 定稿
- [x] Client 結構定稿
- [x] 內容管道定稿
- [x] 資料模型定稿
- [x] Phase 1 任務分解
- [x] 戰鬥詳規
- [x] 迷宮詳規
- [x] 測試策略定稿
- [x] Host 規格
- [ ] Phase 1.5+ 擴展規格（見 12-future-phases.md）
