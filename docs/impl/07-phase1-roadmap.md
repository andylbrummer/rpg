# Phase 1 築基 — Detailed Implementation Roadmap

> 九層之臺，起於累土。Phase 1 證明：移動順手？戰鬥過癮？三維可讀？

## Group 1：骨架（Skeleton）— 端到端連通

### T1：Photino 殼

**檔案**：`src/engine/RPC.Host/Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<GameEngine>();
builder.Services.AddSingleton<ContentLoader>();
builder.Services.AddSingleton<WebSocketManager>();

var app = builder.Build();
app.UseWebSockets();
app.MapWebSocketHandler("/ws");
app.MapContentEndpoints("/content");

// Photino 啟動 webview，載入 client/index.html
var window = new PhotinoWindow()
    .SetTitle("The Reach")
    .SetUseOsDefaultSize(false)
    .SetSize(1280, 720)
    .Center()
    .Load($"http://localhost:{app.Urls.First()}");

Task.Run(() => app.Run());
window.WaitForClose();
```

**驗收**：
- [ ] 雙擊 exe，視窗開，白屏或 Vite 預設頁可見
- [ ] 視窗可調大小，最小 1024×768
- [ ] 關閉視窗後程序終止（無殘留 dotnet process）

### T2：WebSocket 握手

**檔案**：`src/engine/RPC.Host/WebSocketHandler.cs`, `src/client/src/net/GameClient.ts`

```csharp
public class WebSocketHandler
{
    public async Task Handle(HttpContext ctx)
    {
        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        await ws.SendAsync(Encode(new { t = 1, p = new { epochMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() } }));
        // 心跳迴圈...
    }
}
```

**驗收**：
- [ ] 客戶端連線後 1s 內收到 Heartbeat
- [ ] UI 角落顯示綠點「已連線」
- [ ] 殺掉 Host 後客戶端 5s 內顯示紅點「已斷線」

### T3：空 Three.js 場景

**檔案**：`src/client/src/renderer/SceneManager.ts`

- PerspectiveCamera(60°, aspect, 0.1, 100)
- 灰色地板 PlaneGeometry(10, 10) + AmbientLight + DirectionalLight
- 渲染迴圈 requestAnimationFrame

**驗收**：
- [ ] 視窗內可見有光影的灰色地面
- [ ] 調整視窗大小，畫面比例正確
- [ ] F12 開發工具可見（開發模式）

### T4：REST 內容端點

**檔案**：`src/engine/RPC.Host/ContentEndpoints.cs`, `src/client/src/net/ContentClient.ts`

```csharp
app.MapGet("/content/{type}/{id}", (string type, string id, ContentLoader loader) =>
{
    var data = loader.LoadRaw(type, id);
    return data is null ? Results.NotFound() : Results.Bytes(data, "application/json");
});
```

**驗收**：
- [ ] 伺服器回傳硬編碼 JSON room segment
- [ ] 客戶端 fetch 並 console.log 內容
- [ ] 404 時前端不拋未處理異常

**Group 1 里程碑**：視窗內見 3D 灰地 + 綠點「已連線」。

---

## Group 2：迷宮導航

### T5：網格移動系統

**檔案**：`src/engine/RPC.Engine/Dungeon/Movement.cs`

```csharp
public static MovementResult TryMove(
    DungeonState dungeon,
    Direction facing,
    Direction moveDir)
{
    var desired = facing.Add(moveDir); // e.g. facing N + move Fwd = N
    var next = dungeon.PlayerPosition.Step(desired);

    if (!InBounds(next, dungeon.Grid)) return MovementResult.Blocked;
    var cell = dungeon.Grid[next.Y, next.X];
    if (!cell.Floor) return MovementResult.Blocked;
    if (cell.WallInDirection(desired) is Solid or LockedDoor)
        return MovementResult.Blocked;

    return MovementResult.Success(next, desired);
}
```

**驗收**：
- [ ] 純函數，無 IO，無隨機（移動本身無 RNG）
- [ ] 單元測試：撞牆回 Blocked，空地回 Success，門回 Blocked

### T6：房間段載入器

**檔案**：`src/engine/RPC.Content/SegmentLoader.cs`

```csharp
public RoomSegment Load(string id)
{
    var json = _pack.GetString($"segments/{id}");
    return JsonSerializer.Deserialize<RoomSegment>(json, _options);
}
```

**驗收**：
- [ ] 載入 12-15 個 Broken Engine 段，無拋異常
- [ ] 每段 `grid` 陣列維度與 `size` 標記一致
- [ ] Schema 驗證通過

### T7：迷宮組裝器

**檔案**：`src/engine/RPC.Engine/Dungeon/Assembler.cs`

算法：
1. 選起點段（`broken-engine-entrance`）
2. DFS 鋪設：每步從 pool 選一段，檢查連接口匹配
3. 放置終點段（`broken-engine-core`）
4. Dijkstra 驗證連通
5. 失敗則回溯重試（max 100 attempts）

**驗收**：
- [ ] 100 次組裝，100% 連通
- [ ] 平均 grid 尺寸 20×24 至 32×32
- [ ] 至少 1 條隱藏通道（Hidden door）
- [ ] 快照測試：同 seed 產出同 grid

### T8：Three.js 迷宮渲染器

**檔案**：`src/client/src/renderer/DungeonRenderer.ts`

- 接收 `Cell[][]`，生成合併 mesh
- 材質：地板 `#5a5a5a`，牆 `#3a3a3a`，門 `#8B4513`
- 無天花板（Phase 1 簡化）
- 第一人稱相機，FOV 60°

**驗收**：
- [ ] 可見牆壁圍繞的通道
- [ ] 門以不同顏色顯示
- [ ] 相機位置正確對齊網格中心

### T9：移動輸入迴圈

**檔案**：`src/client/src/input/DungeonInput.ts`, `src/engine/RPC.Host/Handlers/MoveHandler.cs`

流程：
```
KeyDown → 樂觀插值啟動 → WS send MoveReq →
Server validate → DeltaState / Error →
Client merge or snap-back
```

**驗收**：
- [ ] Arrow keys / WASD 皆可移動與轉向
- [ ] 輸入→畫面更新 <50ms（localhost 下肉眼無延遲）
- [ ] 撞牆無抖動，相機穩定
- [ ] 200ms 插值平滑，無瞬移

### T10：自動地圖

**檔案**：`src/client/src/ui/AutoMap.svelte`

```svelte
<canvas bind:this={canvas} width={mapW} height={mapH} />
<script>
  $: render($gameState.dungeon?.grid, $gameState.dungeon?.playerPosition);
</script>
```

**驗收**：
- [ ] 探索過的格子顯示輪廓
- [ ] 當前位置藍點 + 方向錐
- [ ] 門以棕色標記
- [ ] 秘密門未發現前不可見

**Group 2 里程碑**：可走完整個 Broken Engine 迷宮。自動地圖追蹤正確。

---

## Group 3：角色與背包

### T11：角色資料模型

**檔案**：已定義於 `06-data-models.md`

**驗收**：
- [ ] `CharacterState` 為 record struct（值型別，無 GC 壓力）
- [ ] 裝備變更正確反映 `EffectiveStats`
- [ ] 單元測試：等級 1 Bonewarden HP ≈ 18

### T12：隊伍系統

**檔案**：`src/engine/RPC.Engine/Party/PartySystem.cs`

- Phase 1：4 人隊伍，2 前 2 後（過渡配置）
- 前排：索引 0,1；後排：索引 2,3

**驗收**：
- [ ] 可建立 4 人隊伍
- [ ] 前後排指派可變更
- [ ] 死亡角色自動移出前排

### T13：4 職業內容

**檔案**：`content/classes/bonewarden.json`, `stillblade.json`, `cauterist.json`, `hollow.json`

每職業含：
- 基礎屬性（1 級）
- 3-4 個能力（ID、名稱、消耗、效果）
- 升級表（1-5 級，每級 HP、屬性增量）

**驗收**：
- [ ] JSON Schema 驗證通過
- [ ] 能力有唯一 ID，可被協同引擎引用

### T14：物品與裝備內容

**檔案**：`content/items/*.json`

Phase 1 需要：
- 武器×4（每職業一主手）
- 防具×6（頭×2、身×2、盾×2）
- 消耗品×6（治療藥×3、狀態藥×3）
- 元件×4（骨片、烙療補給、墨水、引擎充能）

**驗收**：
- [ ] 每件物品有圖示欄位（base64 佔位符可）
- [ ] 裝備後 stats 正確變化

### T15：背包 UI

**檔案**：`src/client/src/ui/InventoryPanel.svelte`, `src/client/src/ui/PartyStatusBar.svelte`

```svelte
<!-- PartyStatusBar -->
<div class="party-bar">
  {#each $party.active as char}
    <div class="char-tile">
      <div class="hp-bar" style="width: {(char.hp/char.maxHp)*100}%"></div>
      <span class="name">{char.name}</span>
      {#each char.statuses as s}
        <img src="/icons/{s.id}.png" alt={s.name} />
      {/each}
    </div>
  {/each}
</div>
```

**驗收**：
- [ ] 裝備/卸下操作反饋 <100ms
- [ ] 隊伍狀態條常駐畫面底部
- [ ] HP 條顏色分段：綠 >50%，黃 25-50%，紅 <25%

### T16：資源包編譯器 v1

**檔案**：`tools/content-pack/Compiler.cs`

```bash
dotnet run --project tools/content-pack -- compile content/ src/engine/RPC.Host/Content/
# 產出：content.rpk, manifest.json
```

**驗收**：
- [ ] 所有 JSON 輸入產出單一 `.rpk`
- [ ] Engine 可從 `.rpk` 讀取段與物品
- [ ] 開發模式可切換回直讀 JSON

**Group 3 里程碑**：創 4 人隊伍，裝備，看屬性。背包操作順暢。內容來自 `.rpk`。

---

## Group 4：戰鬥

### T17：戰鬥狀態機

**檔案**：`src/engine/RPC.Engine/Combat/CombatEngine.cs`

```csharp
public static CombatState Enter(
    PartyState party,
    EncounterDef encounter,
    GameRandom rng)
{
    var enemies = SpawnEnemies(encounter, rng);
    var all = party.Active.Select(ToCombatant)
        .Concat(enemies).ToArray();
    var order = RollInitiative(all, rng);
    return new CombatState(all, 1, order, 0, new(), null, Array.Empty<CombatLogEntry>(), null);
}
```

**驗收**：
- [ ] 純函數，相同 seed 產出相同 initiative order
- [ ] 狀態機覆蓋：Enter → Round → Turn → Resolve → Check → Exit

### T18：先攻系統

```csharp
private static Guid[] RollInitiative(Combatant[] all, GameRandom rng)
{
    return all
        .Select(c => (c.Id, Roll: c.Speed + rng.Roll(-3, 3)))
        .OrderByDescending(x => x.Roll)
        .Select(x => x.Id)
        .ToArray();
}
```

**驗收**：
- [ ] 每回合重骰
- [ ] 可見先攻條顯示順序
- [ ] 速度高者傾向先動，非絕對

### T19：行動解析

能力標籤系統（C9 決議）：
```csharp
public bool CanApply(Combatant target, AbilityDef ability)
{
    foreach (var tag in ability.Tags)
    {
        if (target.Immunities.Contains(tag)) return false;
    }
    return true;
}
```

**驗收**：
- [ ] Stillblade 免疫 necromantic buff
- [ ] Bloom creature 抗性 necromantic，弱 fire
- [ ] 命中計算：acc + d20 vs evade + 10

### T20：距離帶

- Melee：前排 + 近戰敵人
- Short：後排可射，敵人 caster
- Long：敵人 artillery，可撤退至此

**驗收**：
- [ ] 近戰攻擊無法觸及 Short/Long
- [ ] 敵人行動可改變距離帶

### T21-22：敵人資料與 AI

**檔案**：`content/enemies/*.json`, `src/engine/RPC.Engine/Combat/Ai/*.cs`

```csharp
public interface ICombatAi
{
    CombatAction Choose(Combatant self, CombatState state, GameRandom rng);
}

public class SoldierAi : ICombatAi
{
    public CombatAction Choose(Combatant self, CombatState state, GameRandom rng)
    {
        var weakest = state.Combatants
            .Where(c => c.IsPlayer && c.Hp > 0)
            .MinBy(c => c.Hp);

        if (self.Hp < self.MaxHp * 0.3 && CanRetreat(self))
            return new CombatAction(self.Id, ActionType.Wait, null, null, null); // retreat logic

        return new CombatAction(self.Id, ActionType.Attack, weakest?.Id, null, null);
    }
}
```

**驗收**：
- [ ] Soldier 集中最弱 HP 目標
- [ ] Soldier <30% HP 嘗試撤退
- [ ] Construct 守衛模式，優先攻擊輸出最高者

### T23：戰鬥渲染器

**檔案**：`src/client/src/renderer/CombatRenderer.ts`

- 背景：模糊化迷宮最後幀或暗色漸層
- 敵人：低多邊形模型（3-6 個敵人占位符）
- 距離帶：背景色塊區隔
- 動畫：揮擊（相機搖）、投射物（拋物線）、受擊（紅閃）

**驗收**：
- [ ] 進入戰鬥過渡 <500ms
- [ ] 傷害數字上浮 1s 後淡出
- [ ] 死亡敵人淡出

### T24：戰鬥 UI

**檔案**：`src/client/src/ui/combat/CombatPanel.svelte`

- 先攻條：頭像列，當前放大
- 行動選單：Attack / Defend / Cast > / Item > / Flee
- 目標選擇：敵人高亮，hover 顯示預估傷害
- 狀態效果：圖示 + 剩餘回合數

**驗收**：
- [ ] 選單操作 3 次點擊內完成（選行動→選能力→選目標）
- [ ] 非法目標禁用（灰色）
- [ ] 回合計時器可選（30s 自動 Wait）

### T25：快照測試架

**檔案**：`src/engine/RPC.Tests/CombatSnapshotTests.cs`

```csharp
[Theory]
[InlineData("scenarios/t01-basic-melee.json")]
[InlineData("scenarios/t02-bonewarden-cast.json")]
// ... 10 scenarios
public void CombatReplay(string scenarioPath)
{
    var (initial, actions, expected) = Load(scenarioPath);
    var engine = new CombatEngine(Content, new GameRandom(initial.Seed));
    var state = engine.Enter(initial.Party, initial.Encounter);

    foreach (var action in actions)
        state = engine.Resolve(state, action);

    state.Should().BeEquivalentTo(expected, opts => opts
        .Excluding(x => x.Log)
        .Excluding(x => x.CurrentTurnIndex));
}
```

**驗收**：
- [ ] 10 個劇本全過
- [ ] 劇本格式：JSON，含初始狀態、行動序列、期望終態

**Group 4 里程碑**：戰鬥手感戰術，先攻條助規劃，距離帶創造定位決策。10 快照全過。

---

## Group 5：循環閉環

### T26：遭遇觸發器

**檔案**：`src/engine/RPC.Engine/Dungeon/EncounterTrigger.cs`

```csharp
public static bool ShouldTrigger(Cell cell, HashSet<string> defeated)
{
    return cell.EncounterId is not null
        && !defeated.Contains(cell.EncounterId);
}
```

**驗收**：
- [ ] 踩到遭遇格即進戰鬥
- [ ] 勝利後該格不再觸發
- [ ] 逃跑後該格仍觸發（敵人還在）

### T27：迷宮↔戰鬥流

```
Dungeon ──踩遭遇──► Combat ──勝/逃──► Dungeon（同位置）
```

**驗收**：
- [ ] 退出戰鬥回原先位置與面向
- [ ] 資源（HP、物品、元件）跨場景持續
- [ ] 死亡敵人從該格移除

### T28：城鎮（選單式）

**檔案**：`src/client/src/ui/town/TownScreen.svelte`

畫面分頁：
- 酒館：招募清單（4-6 人），價格/等級/職業
- 市場：買賣網格，分類標籤
- 任務板：3 個迷宮任務卡片，線性解鎖
- 停機時間：每角色選一活動（Phase 1 僅 Rest/Train）

**驗收**：
- [ ] 可招募替換隊員
- [ ] 可買賣物品
- [ ] 選任務後進入對應迷宮

### T29：3 迷宮任務線

**檔案**：`content/dungeons/broken-engine-{1,2,3}.json`

難度遞增：
- 迷宮 1：12-15 段，3 遭遇，等級 1-2
- 迷宮 2：18-22 段，5 遭遇，等級 2-3
- 迷宮 3：24-30 段，7 遭遇 + 1 setpiece boss，等級 3-5

**驗收**：
- [ ] 完成一個解鎖下一個
- [ ] 每個迷宮視覺主題一致（Broken Engine）

### T30：升級

```csharp
public static CharacterState LevelUp(CharacterState c)
{
    var table = Content.Classes[c.ClassId].LevelTable[c.Level];
    return c with {
        Level = c.Level + 1,
        MaxHp = c.MaxHp + table.HpGain,
        BaseStats = c.BaseStats.Add(table.StatGain),
        KnownAbilities = c.KnownAbilities.Concat(table.NewAbilities).ToArray()
    };
}
```

**驗收**：
- [ ] 戰鬥與探索（新格子）皆給 XP
- [ ] 等級 5 封頂
- [ ] 升級在城鎮觸發

### T31：存檔/讀檔

**檔案**：`src/engine/RPC.Host/SaveManager.cs`

```csharp
public void Save(int slot, GameState state)
{
    var bytes = MessagePackSerializer.Serialize(state); // 或 JSON
    var hash = XXH64.Hash(bytes);
    File.WriteAllBytes($"saves/{slot}.sav", bytes.Concat(BitConverter.GetBytes(hash)).ToArray());
}
```

**驗收**：
- [ ] 單存檔槽
- [ ] 僅城鎮可存
- [ ] 存→讀→狀態完全一致（snapshot assert）

### T32：Playwright 煙霧測試

**檔案**：`src/client/e2e/smoke.spec.ts`

```typescript
test('walk and fight', async ({ page }) => {
  await page.goto('http://localhost:5000');
  await expect(page.locator('[data-testid="connection-status"]')).toHaveText('已連線');
  await page.keyboard.press('ArrowUp');
  await expect(page.locator('[data-testid="player-pos"]')).toContainText('1,0');
  // 走入遭遇格...
  await expect(page.locator('[data-testid="combat-panel"]')).toBeVisible();
  await page.click('[data-testid="action-attack"]');
  await page.click('[data-testid="target-enemy-0"]');
  await expect(page.locator('[data-testid="combat-log"]')).toContainText('命中');
});
```

**驗收**：
- [ ] 5-6 個測試：移動、開背包、進戰鬥、逃跑、存讀檔
- [ ] CI 中 headless 執行
- [ ] 全部通過

**Group 5 / Phase 1 里程碑**：
2 小時可通關 3 迷宮。戰鬥消耗到第 2 迷宮開始造成真實抉擇。升級有感。存讀檔無損。全 Playwright 通過。
