# 測之詳 — Testing Strategy

> 未經測試之碼，猶未磨之劍。鋒利與否，一戰方知。

## 1. 測試金字塔

```
        △ E2E (Playwright)
       ╱ ╲    5-6 tests, 關鍵流驗證
      ╱   ╲
     ╱─────╲ Integration (內容管道)
    ╱  20+   ╲   JSON → binary → load
   ╱───────────╲
  ╱   Snapshot   ╲  戰鬥回放，斷言終態
 ╱    (10-50+)    ╲
╱─────────────────────╲
╲      Unit (200+)     ╱
 ╲  傷害計算、移動、AI ╱
  ╲───────────────────╱
```

## 2. 單元測試（xUnit）

### 2.1 命名規約

```csharp
[Fact]
public void TryMove_FacingNorth_MoveForward_IncrementsY()
{ }

[Theory]
[InlineData(10, 5, true)]   // acc 10 vs evade 5 → hit on roll 5+
[InlineData(5, 10, false)]  // acc 5 vs evade 10 → hit only on roll 15+
public void HitCalculation_AccuracyVsEvasion(int acc, int eva, bool expected)
{ }
```

### 2.2 必測單元清單

| 模組 | 數量 | 重點 |
|---|---|---|
| Movement | 15 | 四方向、撞牆、穿門、邊界 |
| CombatCalc | 25 | 命中、傷害、暴擊、抗性、免疫 |
| Initiative | 10 | 排序、同值、延遲、速度變化 |
| AI | 15 | 三種 AI 各 5 個情境 |
| Inventory | 10 | 裝備、堆疊、消耗、空間 |
| Reputation | 8 | 閾值、正負、多陣營交互 |
| Synergy | 12 | 觸發、不觸發、順序無關 |
| DungeonAsm | 10 | 連通、迴路、門放置 |

## 3. 快照測試

### 3.1 戰鬥回放格式

```json
{
  "name": "t01-bonewarden-vs-scout",
  "seed": 12345,
  "initial": {
    "party": [ { "classId": "bonewarden", "level": 1, "hp": 18 } ],
    "encounterId": "bureau-scout-x1"
  },
  "actions": [
    { "actor": 0, "type": "Cast", "abilityId": "bone-shard", "target": "enemy-0" },
    { "actor": "enemy-0", "type": "Attack", "target": 0 }
  ],
  "expected": {
    "result": "Win",
    "partyHp": [12],
    "rounds": 3,
    "logCount": 6
  }
}
```

### 3.2 快照測試執行器

```csharp
public class SnapshotTestRunner
{
    [Theory]
    [ClassData(typeof(CombatScenarioData))]
    public void CombatSnapshot(CombatScenario scenario)
    {
        var rng = new GameRandom(scenario.Seed);
        var engine = new CombatEngine(Content, rng);
        var state = engine.Enter(scenario.Initial.Party, scenario.Initial.Encounter);

        foreach (var action in scenario.Actions)
            state = engine.Resolve(state, action);

        Assert.Equal(scenario.Expected.Result, state.Result);
        Assert.Equal(scenario.Expected.Rounds, state.RoundNumber);
        for (int i = 0; i < scenario.Expected.PartyHp.Length; i++)
            Assert.Equal(scenario.Expected.PartyHp[i], state.Combatants[i].Hp);
    }
}
```

### 3.3 Phase 1 快照劇本

| ID | 情境 | 驗證點 |
|---|---|---|
| t01 | 1 級 Bonewarden vs 1 Scout | 基礎傷害、命中 |
| t02 | 4 人隊 vs 3 Spawnlings | 多目標、回合循環 |
| t03 | 全隊防禦 3 回合 | 防禦減傷公式 |
| t04 | Stillblade 對 necromantic buff | 免疫標籤 |
| t05 | Cauterist 治療瀕死隊友 | 治療公式、死亡邊界 |
| t06 | 逃跑成功 | 逃跑機率、狀態轉換 |
| t07 | 逃跑失敗 | 失敗懲罰 |
| t08 | Construct 弱點觸發 | Fieldwright 互動 |
| t09 | Soldier 撤退 | HP 閾值、距離帶變化 |
| t10 | 全隊滅團 | GameOver 狀態 |

## 4. 整合測試

### 4.1 內容管道測試

```csharp
[Fact]
public void ContentPipeline_RoundTrip()
{
    var compiler = new ContentPackCompiler("content/");
    var packPath = compiler.Compile("test.rpk");

    var reader = new ContentPackReader(packPath);
    var segment = reader.Load<RoomSegment>("broken-engine-entrance");

    Assert.Equal("broken-engine", segment.Template);
    Assert.Contains("corridor", segment.Tags);
}
```

### 4.2 儲存往返測試

```csharp
[Fact]
public void SaveLoad_RoundTrip()
{
    var state = GameStateFactory.CreateDefault();
    var manager = new SaveManager("test-saves/");
    manager.Save(0, state);

    var loaded = manager.Load(0);
    Assert.Equivalent(state, loaded); // deep equality
}
```

## 5. E2E 測試（Playwright）

### 5.1 測試環境

```typescript
// playwright.config.ts
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false, // 遊戲狀態共享，不可並行
  workers: 1,
  use: {
    baseURL: 'http://localhost:5000',
    headless: true,
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
});
```

### 5.2 測試用例

```typescript
// e2e/01-launch.spec.ts
test('app launches and connects', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('[data-testid="connection-dot"]'))
    .toHaveClass(/connected/);
});

// e2e/02-navigate.spec.ts
test('player can walk in dungeon', async ({ page }) => {
  await startNewGame(page); // helper
  await page.keyboard.press('ArrowUp');
  await expect(page.locator('[data-testid="player-coords"]'))
    .toHaveText('0,1');
});

// e2e/03-combat.spec.ts
test('enter combat and win', async ({ page }) => {
  await startNewGame(page);
  await walkToEncounter(page); // helper: 移動到遭遇格
  await expect(page.locator('[data-testid="combat-panel"]')).toBeVisible();

  // 戰鬥至結束
  while (await page.locator('[data-testid="combat-panel"]').isVisible()) {
    await page.click('[data-testid="action-attack"]');
    await page.click('[data-testid="target-first-enemy"]');
    await page.waitForTimeout(500); // 動畫
  }

  await expect(page.locator('[data-testid="dungeon-view"]')).toBeVisible();
});

// e2e/04-inventory.spec.ts
test('equip item changes stats', async ({ page }) => {
  await startNewGame(page);
  await openInventory(page);
  const before = await page.locator('[data-testid="stat-might-0"]').textContent();
  await page.dragAndDrop('[data-testid="item-sword-1"]', '[data-testid="slot-mainhand-0"]');
  const after = await page.locator('[data-testid="stat-might-0"]').textContent();
  expect(Number(after)).toBeGreaterThan(Number(before));
});

// e2e/05-save-load.spec.ts
test('save and load preserves state', async ({ page }) => {
  await startNewGame(page);
  await walkToCell(page, 2, 3);
  await saveGame(page, 0);
  await reloadPage(page);
  await loadGame(page, 0);
  await expect(page.locator('[data-testid="player-coords"]'))
    .toHaveText('2,3');
});
```

## 6. 確定性驗證

所有隨機行為必須種子化：

```csharp
[Theory]
[InlineData(0)]
[InlineData(42)]
[InlineData(1337)]
[InlineData(-1)]
public void Deterministic_RandomSameSeedSameResult(int seed)
{
    var rng1 = new GameRandom(seed);
    var rng2 = new GameRandom(seed);

    var rolls1 = Enumerable.Range(0, 100).Select(_ => rng1.Roll(1, 20)).ToArray();
    var rolls2 = Enumerable.Range(0, 100).Select(_ => rng2.Roll(1, 20)).ToArray();

    Assert.Equal(rolls1, rolls2);
}
```

## 7. 性能基準

```csharp
[Fact]
public void CombatResolve_Under500ms()
{
    var state = CreateLargeCombatState(6 players, 8 enemies);
    var sw = Stopwatch.StartNew();
    for (int i = 0; i < 100; i++)
        _ = _engine.Resolve(state, sampleAction);
    sw.Stop();
    Assert.True(sw.ElapsedMilliseconds < 50); // 100 turns < 50ms
}

[Fact]
public void DungeonAssembly_Under3s()
{
    var sw = Stopwatch.StartNew();
    var dungeon = Assembler.Assemble(template, pool, 5, config, rng);
    sw.Stop();
    Assert.True(sw.ElapsedMilliseconds < 3000);
}
```

## 8. CI 配置（GitHub Actions）

```yaml
# .github/workflows/ci.yml
name: CI
on: [push, pull_request]
jobs:
  backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet test src/engine/RPC.Tests --logger trx
      - run: dotnet run --project tools/content-pack -- validate content/

  frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '20' }
      - run: npm ci
      - run: npm run build
      - run: npm run check  # svelte-check

  e2e:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - uses: actions/setup-node@v4
        with: { node-version: '20' }
      - run: dotnet build src/engine/RPC.Host
      - run: dotnet run --project src/engine/RPC.Host &
      - run: npx playwright install --with-deps chromium
      - run: npx playwright test
```
