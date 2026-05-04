# 形之詳 — Client Specification

> 前端為畫，後端為筆。筆動則畫隨。

## 1. 技術棧

| 域 | 選 | 由 |
|---|---|---|
| 打包 | Vite | HMR 速，TS/Svelte 原生支援 |
| 渲染 | Three.js r160 | WebGL，低多邊形，網格迷宮無需全引擎 |
| UI | Svelte 4/5 | 編譯響應式，無 VDOM 與渲染迴圈競爭 |
| 語言 | TypeScript 5.3 | 型別安全，Three.js 型別完善 |
| 狀態 | 輕量 store | Svelte `writable`/`derived`，無 Redux |
| 快取 | IndexedDB | 內容分塊快取，ETag 失效 |

## 2. 渲染器架構

### 2.1 場景圖

```
Scene
├── Camera (Perspective, 60° FOV)
├── DungeonGroup
│   ├── FloorMesh[]      # PlaneGeometry, 2x2 per cell
│   ├── WallMesh[]       # BoxGeometry, 合併同材質以減 draw call
│   ├── CeilingMesh[]    # 可選（Phase 1 可省略以減負擔）
│   └── DoorMesh[]       # 獨立以支援開門動畫
├── EntityGroup
│   ├── PlayerMarker     # 僅自動地圖可見
│   └── LootMarkers[]    # 可互動高亮
└── Lighting
    ├── AmbientLight
    └── PointLights[]    # 每 setpiece 段一盞
```

### 2.2 網格移動與相機

相機鎖定第一人稱，網格中心對齊。

```typescript
interface CameraState {
  gridX: number;
  gridY: number;
  facing: 0 | 1 | 2 | 3; // N E S W
  // 插值狀態（客戶端自有，非伺服器狀態）
  interpFrom: { x: number; y: number; facing: number } | null;
  interpStart: number;    // performance.now()
  interpDuration: 200;    // ms，固定
}
```

**樂觀移動流程**：
1. 按鍵 → 客戶端立即啟動插值動畫
2. 同時發 `MoveReq` 至伺服器
3. 伺服器回 `Ack` + `DeltaState`（新位置）
4. 正常：插值結束時新狀態已至，無縫
5. 拒絕（撞牆）：收到 `Error` → 中斷插值，200ms 內 snap-back

### 2.3 迷宮幾何生成

```typescript
function buildDungeonGeometry(grid: Cell[][]): DungeonMeshes {
  const floors: THREE.Mesh[] = [];
  const walls: THREE.Mesh[] = [];

  for (let y = 0; y < grid.length; y++) {
    for (let x = 0; x < grid[0].length; x++) {
      const cell = grid[y][x];
      if (cell.floor) {
        floors.push(createFloor(x, y, cell.segmentId));
      }
      if (cell.north === 'solid') walls.push(createWall(x, y, 'N'));
      // ... etc
    }
  }

  // 合併同材質 mesh 以減 draw call
  return {
    floor: mergeGeometries(floors),
    wall: mergeGeometries(walls),
  };
}
```

材質依 `template` 切換：
- `broken-engine`：灰金屬、鏽、管線
- `bloom-site`：有機質、紫綠色調、發光斑點
- `contested-ruin`：石磚、焦痕、營火餘燼

### 2.4 自動地圖

```typescript
// Svelte component
<AutoMap {grid} {playerPos} {exploredCells} />
```

- Canvas 2D 繪製，獨立於 Three.js
- 已探索格：淡灰輪廓
- 牆：深灰線
- 門：棕色矩形
- 當前位置：藍點 + 方向錐
- 秘密門：初見不繪，發現後以虛線補繪

## 3. UI 面板

### 3.1 戰鬥介面

```
┌─────────────────────────────────────┐
│ [回合順序條]  A ► B □ C □ D □     │
├─────────────────────────────────────┤
│                                     │
│      [Three.js 戰鬥場景]            │
│      敵人站位（距離帶可視）          │
│                                     │
├──────────────────┬──────────────────┤
│ [行動選單]       │ [隊伍狀態]       │
│ 攻擊 >           │ ▓▓▓▓░░ Warden   │
│ 防禦             │ ▓▓▓▓▓▓ Scorcher │
│ 施法 >           │ [狀態圖示]        │
│ 物品 >           │                  │
│ 逃跑             │                  │
└──────────────────┴──────────────────┘
```

- 回合順序條：頭像橫列，當前回合者放大 + 邊框脈衝
- 距離帶：背景分三區（Melee/Short/Long）色塊區隔
- 傷害數字：Three.js 場景內 floating text，向上飄散 1s
- 協同觸發：全螢幕邊緣閃光 + 音效 + 自動開啟 Field Notes

### 3.2 背包介面

```typescript
interface InventoryUIProps {
  characters: CharacterState[];
  selectedCharIndex: number;
  expeditionCache: Item[];
}
```

- 裝備槽：頭、身、主手、副手、飾品
- 背包格：8 格/角色，可拖曳
- 遠征儲備：12 格共享，僅戰鬥間可存取
- 元件堆疊：顯示數量小標

### 3.3 城鎮介面

純 Svelte 選單，無 Three.js：
- 酒館：招募名單（等級/職業/分支/價格）
- 市場：買賣網格，陣營商人標籤（鎖定時顯示需求聲望）
- 任務板：主線/支線任務卡片
- 停機時間：每角色一行，下拉選活動

## 4. 網路層

### 4.1 WebSocket 客戶端

```typescript
class GameClient {
  private ws: WebSocket;
  private pendingAcks: Map<number, () => void>;
  private seq = 0;

  connect(url: string) {
    this.ws = new WebSocket(url);
    this.ws.onmessage = (ev) => {
      const msg = JSON.parse(ev.data) as ServerMessage;
      this.handle(msg);
    };
  }

  send(type: MessageType, payload: unknown) {
    const seq = ++this.seq;
    this.ws.send(JSON.stringify({ t: type, s: seq, p: payload }));
    return new Promise<void>((res) => this.pendingAcks.set(seq, res));
  }

  private handle(msg: ServerMessage) {
    if (msg.t === MessageType.Ack) {
      this.pendingAcks.get(msg.p.seq)?.();
      this.pendingAcks.delete(msg.p.seq);
    } else if (msg.t === MessageType.GameState) {
      gameState.set(msg.p);
    } else if (msg.t === MessageType.DeltaState) {
      applyDeltas(msg.p);
    }
  }
}
```

### 4.2 內容快取

```typescript
class ContentCache {
  private db: IDBDatabase;

  async get(id: string, etag?: string): Promise<ArrayBuffer> {
    const cached = await this.db.get('content', id);
    if (cached && cached.etag === etag) return cached.data;

    const res = await fetch(`/content/${id}`, {
      headers: etag ? { 'If-None-Match': etag } : {},
    });
    if (res.status === 304) return cached.data;

    const data = await res.arrayBuffer();
    await this.db.put('content', { id, etag: res.headers.get('ETag'), data });
    return data;
  }
}
```

## 5. 輸入處理

```typescript
const KEY_MAP: Record<string, ClientAction> = {
  ArrowUp:    { type: 'move', dir: 'N' },
  ArrowDown:  { type: 'move', dir: 'S' },
  ArrowLeft:  { type: 'turn', dir: 'L' },
  ArrowRight: { type: 'turn', dir: 'R' },
  w:          { type: 'move', dir: 'N' },
  s:          { type: 'move', dir: 'S' },
  a:          { type: 'strafe', dir: 'W' },
  d:          { type: 'strafe', dir: 'E' },
  q:          { type: 'turn', dir: 'L' },
  e:          { type: 'turn', dir: 'R' },
  i:          { type: 'ui', panel: 'inventory' },
  m:          { type: 'ui', panel: 'map' },
  Escape:     { type: 'ui', panel: 'close' },
};
```

輸入緩衝：戰鬥中排隊動作；探索中禁止連發（200ms debounce）。
