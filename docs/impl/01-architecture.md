# 構架總綱 — System Architecture

> 天地有大美而不言，系統有定構而後行。
> The Reach 之工程，分三層：殼、核、形。

## 層次

```
┌─────────────────────────────────────────┐
│           Photino Shell (.NET)          │
│  ┌─────────────┐    ┌─────────────────┐ │
│  │ RPC.Host    │◄──►│ WebView2/CEF    │ │
│  │  · WS srv   │    │  · Svelte UI    │ │
│  │  · REST srv │    │  · Three.js     │ │
│  │  · Engine   │    │  · IndexedDB    │ │
│  └─────────────┘    └─────────────────┘ │
│         ↑↓ WS/REST   (localhost only)   │
└─────────────────────────────────────────┘
```

| 層 | 責 | 技 |
|---|---|---|
| **殼** `RPC.Host` | Photino 啟動、WS/REST 伺服、存檔IO | .NET 8+, Photino.NET |
| **核** `RPC.Engine` | 戰鬥、迷宮組裝、陣營狀態、RNG | .NET ClassLib, zero-alloc hot path |
| **形** `client/` | 渲染、輸入、UI、快取 | TS+Svelte+Vite, Three.js |

## 核心鐵律

**核獨攬權。** 凡隨機、位置、戰鬥判定、存檔，盡歸 `.NET Engine`。前端僅為畫筆與傳聲筒。

**殼不涉邏輯。** `RPC.Host` 僅轉譯：WS 幀 → Engine 函數呼叫 → 狀態幀 → WS 廣播。

## 專案樹

```
src/
├── engine/
│   ├── RPC.Engine/         # 純邏輯庫，無 IO
│   ├── RPC.Content/        # 二進位資源包讀取器
│   ├── RPC.Host/           # Photino + WS + REST
│   └── RPC.Tests/          # xUnit + snapshot
├── client/
│   ├── src/
│   │   ├── renderer/       # Three.js 迷宮視角
│   │   ├── ui/             # Svelte 面板
│   │   ├── net/            # WS 客戶端、REST 內容獲取
│   │   ├── cache/          # IndexedDB 內容快取
│   │   └── types/          # TS 型別（手動維護至 Phase 2）
│   ├── index.html
│   └── vite.config.ts
├── content/                # JSON 原始檔
│   ├── segments/
│   ├── encounters/
│   ├── items/
│   ├── npcs/
│   └── schemas/            # JSON Schema 驗證
└── tools/
    └── content-pack/       # JSON → binary pack 編譯器
```

## 建置流

```
content/*.json  ──► content-pack (build-time) ──► .rpk 二進位包
                                                   │
RPC.Engine (run-time) ◄────────────────────────────┘
                           mmap / Span<byte> 零拷貝讀取
```

開發模式可跳過 `.rpk`，直讀 JSON，啟 WS hot-reload 通知前端刷新內容。

## 效能標的

| 項 | 標的 |
|---|---|
| 迷宮幀率 | ≥60fps (mid-range GPU) |
| 戰鬥回合解析 | ≤500ms (server-side) |
| 迷宮載入 | ≤3s |
| WS 延遲 (localhost) | <1ms |
| 輸入→渲染 | <50ms (含樂觀插值) |
