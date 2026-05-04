# 迷之詳 — Dungeon Assembly Deep Specification

> 迷宮非畫，乃圖。圖必連通，路必有終。

## 1. 房間段幾何

段以二維整數網格定義：

```
0 = void (未使用)
1 = floor (可行走)
2 = wall (不可穿越，渲染為牆)
3 = feature (互動點：控制台、寶箱、開關)
4 = hazard (陷阱、傷害區)
```

```json
{
  "id": "broken-engine-corridor-L",
  "template": "broken-engine",
  "size": "small",
  "category": "corridor",
  "connections": {
    "north": "open",
    "south": "open",
    "east": "none",
    "west": "none"
  },
  "geometry": {
    "grid": [
      [2, 2, 2, 2],
      [2, 1, 1, 2],
      [2, 1, 1, 2],
      [2, 2, 2, 2]
    ],
    "elevation": 0,
    "features": []
  }
}
```

## 2. 連接點語義

每段有 4 個方向連接口（N/S/E/W）：
- `open`：開放，匹配對方 `open`
- `closed`：以 `Solid` 牆封閉，可變為 `Hidden` 門
- `hidden`：以 `Hidden` 牆封閉，發現後變 `Open`
- `none`：該方向無出口（段邊界即迷宮邊界）

連接規則：
```csharp
bool CanConnect(Connection a, Connection b)
{
    // open ↔ open / closed / hidden 皆可
    // closed ↔ closed 形成普通牆
    // hidden ↔ hidden 形成秘密通道
    // none 不可有任何連接
    if (a == Connection.None || b == Connection.None) return false;
    return true;
}
```

## 3. 組裝算法

### 3.1 圖生成

```csharp
DungeonGraph GenerateGraph(DungeonTemplate template, GameRandom rng)
{
    var nodes = new List<GraphNode>();
    var edges = new List<GraphEdge>();

    // 1. 放置起點
    nodes.Add(new GraphNode(template.StartSegment, Vec2.Zero));

    // 2. DFS 擴展
    var queue = new Queue<GraphNode>();
    queue.Enqueue(nodes[0]);
    while (nodes.Count < template.TargetRoomCount && queue.Count > 0)
    {
        var current = queue.Dequeue();
        foreach (var dir in CardinalDirections.Shuffle(rng))
        {
            if (current.HasConnection(dir)) continue;
            var nextPos = current.Position.Step(dir);
            if (nodes.Any(n => n.Position == nextPos)) continue;

            var segment = PickSegment(template.Pool, current.OpenConnections, rng);
            if (segment is null) continue;

            var node = new GraphNode(segment, nextPos);
            nodes.Add(node);
            edges.Add(new GraphEdge(current, node, dir));
            queue.Enqueue(node);
        }
    }

    // 3. 添加迴路（loop）
    AddLoops(nodes, edges, template.LoopProbability, rng);

    // 4. 放置終點
    ReplaceFurthestWithEnd(nodes, edges, template.EndSegment, rng);

    return new DungeonGraph(nodes, edges);
}
```

### 3.2 段選擇

```csharp
RoomSegment PickSegment(
    RoomSegment[] pool,
    Direction[] requiredOpenings,
    GameRandom rng)
{
    var candidates = pool
        .Where(s => requiredOpenings.All(dir => s.Connections[dir] != Connection.None))
        .ToArray();

    if (candidates.Length == 0) return null;

    // 加權：corridor 偏好連接 corridor，chamber 後接 chamber 或 corridor
    var weights = candidates.Select(c =>
        c.Category == "corridor" ? 2.0 :
        c.Category == "chamber" ? 1.5 :
        c.Category == "setpiece" ? 0.3 : 1.0);

    return rng.WeightedPick(candidates, weights);
}
```

### 3.3 Grid 鋪設

```csharp
Cell[,] BuildGrid(DungeonGraph graph)
{
    // 計算 bounding box
    var (minX, maxX, minY, maxY) = graph.Bounds;
    var offsetX = -minX;
    var offsetY = -minY;
    var w = maxX - minX + 1;
    var h = maxY - minY + 1;

    // 每段 grid 最大 8×8，所以實際 pixel grid = w*8, h*8
    var gridW = w * 8;
    var gridH = h * 8;
    var grid = new Cell[gridH, gridW];

    foreach (var node in graph.Nodes)
    {
        var baseX = (node.Position.X + offsetX) * 8;
        var baseY = (node.Position.Y + offsetY) * 8;
        BlitSegment(grid, node.Segment, baseX, baseY);
    }

    // 處理段間門
    foreach (var edge in graph.Edges)
    {
        PlaceDoor(grid, edge);
    }

    return grid;
}
```

### 3.4 門放置

兩段相接處，若雙方皆 `open` → `Open`（無門，自由通行）
若一方 `closed` → `Door`（可開啟）
若雙方 `hidden` → `Hidden`（秘密門）

```csharp
void PlaceDoor(Cell[,] grid, GraphEdge edge)
{
    var (a, b, dir) = (edge.A, edge.B, edge.Direction);
    var doorCell = FindBorderCell(grid, a, b, dir);

    var wallDir = dir.ToWallDirection();
    var type = (a.Segment.Connections[dir], b.Segment.Connections[dir.Opposite()]) switch
    {
        (Open, Open) => WallType.Open,
        (Closed, _) or (_, Closed) => WallType.Door,
        (Hidden, Hidden) => WallType.Hidden,
        _ => WallType.Solid
    };

    SetWall(doorCell, wallDir, type);
}
```

## 4. 遭遇放置

```csharp
void PlaceEncounters(DungeonState dungeon, EncounterTable table, GameRandom rng)
{
    var eligibleCells = dungeon.Grid.OfType<Cell>()
        .Where(c => c.Floor && c.Category == "chamber" && c.EncounterId is null)
        .ToList();

    rng.Shuffle(eligibleCells);

    var budget = table.EncounterBudget;
    var placed = 0;
    foreach (var encounter in table.Encounters)
    {
        if (placed >= budget) break;
        var cell = eligibleCells.FirstOrDefault(c =>
            DistanceFromStart(c) >= encounter.MinDepth &&
            DistanceFromNearestEncounter(c) >= 3); // 至少隔 3 格

        if (cell is null) continue;
        cell.EncounterId = encounter.Id;
        placed++;
    }
}
```

## 5. 連通驗證

```csharp
bool ValidateConnectivity(Cell[,] grid, Vec2 start, Vec2 end)
{
    var queue = new Queue<Vec2>();
    var visited = new HashSet<Vec2>();
    queue.Enqueue(start);
    visited.Add(start);

    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        if (current == end) return true;

        foreach (var dir in CardinalDirections)
        {
            var next = current.Step(dir);
            if (!InBounds(next, grid)) continue;
            if (visited.Contains(next)) continue;

            var cell = grid[current.Y, current.X];
            if (cell.WallInDirection(dir) is Solid or LockedDoor or Hidden)
                continue;

            queue.Enqueue(next);
            visited.Add(next);
        }
    }
    return false;
}
```

## 6. Phase 1 段清單（Broken Engine）

| ID | 尺寸 | 類別 | 連接口 | 說明 |
|---|---|---|---|---|
| `be-entrance` | Medium | chamber | N:open,S:open,E:closed,W:closed | 起點，有控制台 |
| `be-corridor-1` | Small | corridor | N:open,S:open | 直走廊 |
| `be-corridor-L` | Small | corridor | N:open,E:open | L 轉角 |
| `be-corridor-T` | Small | corridor | N:open,S:open,E:open | T 字路口 |
| `be-chamber-small` | Small | chamber | N:open | 小房，可放戰利品 |
| `be-chamber-medium` | Medium | chamber | N:open,S:open,E:open | 中房，可放遭遇 |
| `be-chamber-large` | Large | chamber | N:open,S:open,E:open,W:open | 大房，setpiece 前戰場 |
| `be-dead-end` | Small | dead-end | N:open | 死胡同，藏寶 |
| `be-pipe-room` | Medium | puzzle | N:open,S:open | 管線謎題（Phase 1 簡化為純裝飾） |
| `be-gear-hall` | Large | arena | N:open,S:open,E:closed,W:closed | 齒輪大廳，視覺 setpiece |
| `be-control-room` | Large | setpiece | N:open | 終點，Boss 戰 |
| `be-secret-vault` | Small | treasure | E:hidden | 秘密房，隱藏門連接 |

## 7. Underway 特殊規則（Phase 2+）

```csharp
DungeonState AssembleUnderway(
    RoomSegment[] pool,       // 獨立 pool，不與其他模板共享
    Vec2[] fixedJunctions,    // 固定交叉口
    CampaignState campaign,
    GameRandom rng)
{
    // 1. 固定交叉口不變
    // 2. 交叉口之間的路徑每次重組
    // 3. 環境狀態依 campaign turn 遞減
    var degradation = Math.Min(3, campaign.TurnCounter / 10);
    // degradation 0: 完好
    // degradation 1: 輕微積水
    // degradation 2: 崩塌段
    // degradation 3: bloom 入侵
}
```
