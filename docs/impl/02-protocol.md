# 通訊約 — Protocol Specification

> 訊息如舟，格式如楫。楫不正則舟覆。

## C1 決議：JSON for All Phases

MessagePack 雖密，然除錯難。回合制遊戲，頻寬非瓶頸。可讀性勝之。全期用 JSON。

## WebSocket 幀結構

```json
{
  "t": 1,
  "p": { }
}
```

- `t`: `MessageType` 整數（見下表）
- `p`: payload，依型別而異

### MessageType 枚舉

| 值 | 名 | 方向 | 說明 |
|---|---|---|---|
| 1 | `Heartbeat` | S→C | `{ "epochMs": 171... }` 每秒 |
| 2 | `MoveReq` | C→S | `{ "dir": "N" \| "S" \| "E" \| "W" }` |
| 3 | `TurnReq` | C→S | `{ "dir": "L" \| "R" }` 左轉/右轉 |
| 4 | `InteractReq` | C→S | `{ "targetId": "string" }` |
| 5 | `CombatActionReq` | C→S | `{ "actorId": "uuid", "action": "Attack"\|"Defend"\|"Cast"\|"UseItem"\|"Flee"\|"Wait", "targetId?": "uuid", "abilityId?": "string", "itemSlot?": int }` |
| 6 | `GameState` | S→C | 全狀態或 delta（見下方） |
| 7 | `CombatState` | S→C | 戰鬥專用狀態幀 |
| 8 | `DungeonGrid` | S→C | 首次進入迷宮時發送完整 grid |
| 9 | `DeltaState` | S→C | `{ "path": "party[0].hp", "value": 12 }` 陣列 |
| 10 | `Error` | S→C | `{ "code": "INVALID_MOVE", "msg": "..." }` |
| 11 | `Transition` | S→C | `{ "from": "Dungeon", "to": "Combat", "payload": {} }` 場景切換 |
| 12 | `Ack` | S→C | `{ "seq": 42 }` 客戶端動作確認 |

## 狀態同步策略

**初始同步**：進入新場景（迷宮/戰鬥/城鎮）時發完整 `GameState`。

**Delta 同步**：場景內變化發 `DeltaState` 陣列。客戶端用 immutable update 合併。

**樂觀移動**：客戶端按鍵即啟 200ms 相機插值動畫，不等待伺服器。若收到 `Error` 則 snap-back。

## REST 端點

| 方法 | 路徑 | 回應 | 說明 |
|---|---|---|---|
| GET | `/content/{pack}/{id}` | binary / json | 內容資源。`ETag` 快取。 |
| GET | `/content/manifest.json` | JSON | 所有包之 hash 清單。 |
| POST | `/save` | 204 | 寫存檔（Host 轉 Engine 序列化）。 |
| GET | `/save` | JSON | 讀存檔。 |

內容回傳格式依 `Accept: application/octet-stream` 或 `application/json` 而定。開發模式回 JSON，發布回 binary `.rpk` chunk。

## Error Codes

```csharp
public enum ErrorCode
{
    InvalidMove = 1,      // 撞牆、未解鎖門
    InvalidAction = 2,    // 戰鬥中不可用之舉
    TargetOutOfRange = 3,
    InsufficientComponent = 4,
    NotYourTurn = 5,
    InvalidState = 6,     // 場景不符（如城鎮中發戰鬥Action）
}
```

## 心跳與斷線

- 伺服器每秒廣播 `Heartbeat`
- 客戶端 5s 未收到即顯示「連線中斷」遮罩
- WS 自動重連，重連成功後伺服器重發當前場景完整狀態
