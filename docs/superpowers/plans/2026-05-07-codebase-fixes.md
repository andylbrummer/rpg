# Codebase Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 20 issues found in Phase 1 review across security, quality, perf, and nit categories in four atomic commits.

**Architecture:** Sequential in-session edits on `build/kimi` branch. No new files except `.gitignore`. All engine changes covered by xUnit tests where testable; renderer/client changes verified by build. Four commits: security → quality → perf → nits.

**Tech Stack:** C# 9+ / .NET 9 (engine), TypeScript / Svelte 5 (client), xUnit (tests), Three.js (renderer)

**Baseline:** 78 tests passing. Run `dotnet test src/engine/RPC.Tests/RPC.Tests.csproj` before and after each commit.

---

## Task 1: Create .gitignore

**Files:**
- Create: `.gitignore`

- [ ] **Step 1: Create .gitignore**

```
# Agent config
.agnt
.agnt/
.agnt.kdl
static-server.cjs

# .NET build
bin/
obj/
*.user

# Node
node_modules/

# Playwright
test-results/
playwright-report/
```

- [ ] **Step 2: Verify untracked files now ignored**

```bash
git add .gitignore
git status
```

Expected: `.agnt.kdl`, `.agnt/`, `static-server.cjs` no longer listed as untracked.

---

## Task 2: WS frame accumulation

**Files:**
- Modify: `src/engine/RPC.Host/Web/GameServer.cs` — `HandleWebSocket` method

The buffer is 4096 bytes. `ReceiveAsync` sets `result.EndOfMessage = false` when a message spans multiple frames. Current code processes the partial buffer immediately. Fix: accumulate into `MemoryStream` until `EndOfMessage`.

- [ ] **Step 1: Replace receive loop in HandleWebSocket**

Locate `HandleWebSocket` (around line 225). Replace the buffer/receive block:

```csharp
// BEFORE (lines 238-255 approx):
var buffer = new byte[4096];
try
{
    while (socket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
        
        if (result.MessageType == WebSocketMessageType.Close)
        {
            break;
        }

        if (result.MessageType == WebSocketMessageType.Text)
        {
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            await HandleMessage(socket, message);
        }
    }
}
```

```csharp
// AFTER:
var buffer = new byte[4096];
try
{
    while (socket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
    {
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
            if (result.MessageType == WebSocketMessageType.Close) break;
            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        if (result.MessageType == WebSocketMessageType.Close) break;

        if (result.MessageType == WebSocketMessageType.Text)
        {
            var message = Encoding.UTF8.GetString(ms.ToArray());
            await HandleMessage(socket, message);
        }
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/engine/RPC.Host/RPC.Host.csproj
```

Expected: 0 errors.

- [ ] **Step 3: Run tests**

```bash
dotnet test src/engine/RPC.Tests/RPC.Tests.csproj
```

Expected: 78 passed.

---

## Task 3: SaveSystem — log exception + clamp values + version check

**Files:**
- Modify: `src/engine/RPC.Engine/Save/SaveSystem.cs` — `Load` method
- Modify: `src/engine/RPC.Tests/SaveSystemTests.cs` — add 3 tests

- [ ] **Step 1: Write failing tests**

Add to `src/engine/RPC.Tests/SaveSystemTests.cs`:

```csharp
[Fact]
public void SaveSystem_Load_ClampsNegativeLevel()
{
    // Write a save with Level = -5, Xp = -100, CurrentHp = -999
    var json = """
        {
          "version": "1",
          "party": [
            {
              "id": "11111111-1111-1111-1111-111111111111",
              "name": "Kael", "classId": "bonewarden",
              "level": -5, "xp": -100,
              "baseStats": {"strength":4,"agility":3,"endurance":5,"spirit":4,"cunning":4},
              "currentHp": -999, "equipment": {}, "knownAbilities": [], "row": 0
            }
          ],
          "player": { "x": 0, "y": 0, "facing": "North" },
          "exploredTiles": [], "mode": "Menu"
        }
        """;
    File.WriteAllText(_testSavePath, json);

    var gs = new GameState(seed: 1);
    var loaded = gs.LoadGame(_testSavePath);

    Assert.True(loaded);
    var member = gs.Party.Members[0];
    Assert.True(member.Level >= 1, $"Level should be >= 1, was {member.Level}");
    Assert.True(member.Xp >= 0, $"Xp should be >= 0, was {member.Xp}");
    Assert.True(member.CurrentHp >= 0, $"CurrentHp should be >= 0, was {member.CurrentHp}");
}

[Fact]
public void SaveSystem_Load_ClampsRowOutOfRange()
{
    var json = """
        {
          "version": "1",
          "party": [
            {
              "id": "11111111-1111-1111-1111-111111111111",
              "name": "Kael", "classId": "bonewarden",
              "level": 1, "xp": 0,
              "baseStats": {"strength":4,"agility":3,"endurance":5,"spirit":4,"cunning":4},
              "currentHp": 10, "equipment": {}, "knownAbilities": [], "row": 99
            }
          ],
          "player": { "x": 0, "y": 0, "facing": "North" },
          "exploredTiles": [], "mode": "Menu"
        }
        """;
    File.WriteAllText(_testSavePath, json);

    var gs = new GameState(seed: 1);
    var loaded = gs.LoadGame(_testSavePath);

    Assert.True(loaded);
    var member = gs.Party.Members[0];
    Assert.True(member.Row is 0 or 1, $"Row should be 0 or 1, was {member.Row}");
}

[Fact]
public void SaveSystem_Load_ReturnsFalse_OnVersionMismatch()
{
    var json = """{"version":"99","party":[],"player":{"x":0,"y":0,"facing":"North"},"exploredTiles":[],"mode":"Menu"}""";
    File.WriteAllText(_testSavePath, json);

    var gs = new GameState(seed: 1);
    var loaded = gs.LoadGame(_testSavePath);

    Assert.False(loaded);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test src/engine/RPC.Tests/RPC.Tests.csproj --filter "SaveSystem"
```

Expected: 3 new tests FAIL (clamping and version check not yet implemented).

- [ ] **Step 3: Implement clamping and version check in SaveSystem.Load**

In `src/engine/RPC.Engine/Save/SaveSystem.cs`, replace the `Load` method:

```csharp
public static bool Load(GameState state, string? path = null)
{
    path ??= SavePath;
    if (!File.Exists(path))
        return false;

    try
    {
        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<SaveData>(json, Options);
        if (data == null) return false;

        if (data.Version != "1")
        {
            Console.Error.WriteLine($"Save version {data.Version} not supported (expected 1)");
            return false;
        }

        // Restore party with clamped values
        for (int i = 0; i < 4; i++)
        {
            if (i < data.Party.Length)
            {
                var s = data.Party[i];
                var level = Math.Max(1, s.Level);
                var xp = Math.Max(0, s.Xp);
                var hp = Math.Max(0, s.CurrentHp);
                var row = Math.Clamp(s.Row, 0, 1);
                state.Party.SetMember(i, new CharacterState(
                    s.Id, s.Name, s.ClassId, level, xp,
                    s.BaseStats, hp, s.Equipment,
                    s.KnownAbilities, row));
            }
            else
            {
                state.Party.SetMember(i, default);
            }
        }

        // Restore player
        if (Enum.TryParse<Direction>(data.Player.Facing, out var facing))
        {
            state.Player = new Player(
                new Position(data.Player.X, data.Player.Y),
                facing);
        }

        // Restore explored tiles
        state.ExploredTiles.Clear();
        foreach (var tile in data.ExploredTiles)
            state.ExploredTiles.Add(tile);

        // Restore mode
        if (Enum.TryParse<GameMode>(data.Mode, out var mode))
            state.Mode = mode;

        return true;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to load save: {ex.Message}");
        return false;
    }
}
```

- [ ] **Step 4: Run all tests**

```bash
dotnet test src/engine/RPC.Tests/RPC.Tests.csproj
```

Expected: 81 passed (78 + 3 new).

---

## Task 4: Commit security/correctness

- [ ] **Step 1: Stage and commit**

```bash
git add .gitignore \
    src/engine/RPC.Host/Web/GameServer.cs \
    src/engine/RPC.Engine/Save/SaveSystem.cs \
    src/engine/RPC.Tests/SaveSystemTests.cs
git commit -m "fix(security): WS frame accumulation, save clamping, version check, gitignore"
```

---

## Task 5: Remove debug Console.WriteLine from enter_combat

**Files:**
- Modify: `src/engine/RPC.Host/Web/GameServer.cs` — `HandleMessage` switch case `enter_combat`

- [ ] **Step 1: Remove the two debug Console.WriteLine lines**

In `HandleMessage`, locate the `enter_combat` case (around line 304):

```csharp
// BEFORE:
case "enter_combat":
    Console.WriteLine("ENTER_COMBAT triggered");
    _gameState.TriggerEncounter();
    Console.WriteLine($"ENTER_COMBAT result: Mode={_gameState.Mode}, Combat={_gameState.Combat != null}");
    stateChanged = true;
    break;
```

```csharp
// AFTER:
case "enter_combat":
    _gameState.TriggerEncounter();
    stateChanged = true;
    break;
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/engine/RPC.Host/RPC.Host.csproj
```

Expected: 0 errors.

---

## Task 6: Remove generate_dungeon action (superseded by enter_dungeon)

**Files:**
- Modify: `src/engine/RPC.Host/Web/GameServer.cs` — `HandleMessage` switch

The `generate_dungeon` case hardcodes `"broken_engine"` regardless of any dungeonType parameter. The `enter_dungeon` case (which handles typed entry correctly) fully supersedes it.

- [ ] **Step 1: Remove the generate_dungeon case**

Locate and remove this block from the switch in `HandleMessage`:

```csharp
case "generate_dungeon":
    GenerateDungeon("broken_engine");
    stateChanged = true;
    break;
```

- [ ] **Step 2: Run tests**

```bash
dotnet test src/engine/RPC.Tests/RPC.Tests.csproj
```

Expected: 81 passed.

---

## Task 7: Delete Class1.cs

**Files:**
- Delete: `src/engine/RPC.Engine/Class1.cs`

- [ ] **Step 1: Delete the file**

```bash
rm src/engine/RPC.Engine/Class1.cs
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/engine/RPC.Engine/RPC.Engine.csproj
```

Expected: 0 errors.

---

## Task 8: TownMenu NaN guard on hp/maxHp

**Files:**
- Modify: `src/client/src/ui/TownMenu.svelte` — line 44

If `maxHp` is 0 (e.g., uninitialized member), `hp / maxHp` = `NaN`, and the HP bar renders as `NaN%` width.

- [ ] **Step 1: Fix the division**

In `TownMenu.svelte`, line 44:

```svelte
<!-- BEFORE: -->
<div class="hp-fill" style="width: {(member.hp / member.maxHp) * 100}%; background: {hpColor(member.hp, member.maxHp)}"></div>
```

```svelte
<!-- AFTER: -->
<div class="hp-fill" style="width: {(member.hp / (member.maxHp || 1)) * 100}%; background: {hpColor(member.hp, member.maxHp)}"></div>
```

- [ ] **Step 2: Verify TypeScript build**

```bash
cd src/client && npm run build 2>&1 | tail -5
```

Expected: build succeeds, no type errors.

---

## Task 9: Commit quality

- [ ] **Step 1: Stage and commit**

```bash
git add src/engine/RPC.Host/Web/GameServer.cs \
    src/engine/RPC.Engine/Class1.cs \
    src/client/src/ui/TownMenu.svelte
git commit -m "fix(quality): remove debug logs, drop generate_dungeon, delete Class1, NaN guard in TownMenu"
```

Note: `git add` on a deleted file stages the deletion. If `Class1.cs` deletion isn't staged automatically, run `git rm src/engine/RPC.Engine/Class1.cs`.

---

## Task 10: Fix DungeonRenderer shared texture disposal

**Files:**
- Modify: `src/client/src/renderer/DungeonRenderer.ts` — `clearTiles` method (line 254-265)

**Root cause:** `clearTiles` calls `(mat as any).map.dispose()` on each tile's material. Since `wallTexture`, `floorTexture`, and `doorTexture` are shared class fields, this disposes them on the first `clearTiles()` call. All subsequent renders use disposed textures.

**Fix:** Remove the `map.dispose()` line from `clearTiles`. Shared textures are owned by the instance and are disposed only in `dispose()`.

- [ ] **Step 1: Remove map.dispose() from clearTiles**

```typescript
// BEFORE (lines 254-265):
private clearTiles(): void {
  for (const [key, mesh] of this.tileMeshes) {
    this.scene.remove(mesh);
    mesh.geometry.dispose();
    const mat = mesh.material as THREE.Material;
    if ((mat as any).map) {
      (mat as any).map.dispose();
    }
    mat.dispose();
  }
  this.tileMeshes.clear();
}
```

```typescript
// AFTER:
private clearTiles(): void {
  for (const [key, mesh] of this.tileMeshes) {
    this.scene.remove(mesh);
    mesh.geometry.dispose();
    (mesh.material as THREE.Material).dispose();
  }
  this.tileMeshes.clear();
}
```

- [ ] **Step 2: Verify TypeScript build**

```bash
cd src/client && npm run build 2>&1 | tail -5
```

Expected: 0 errors.

---

## Task 11: Cap ExploredTiles at 4096 entries

**Files:**
- Modify: `src/engine/RPC.Engine/GameState.cs` — `ExploredTiles` field and `ExploreAroundPlayer` method
- Modify: `src/engine/RPC.Tests/SaveSystemTests.cs` — add 1 test (ExploredTiles round-trips after cap)

**Design:** Track insertion order via a `Queue<string>`. Before adding, if count >= 4096, dequeue the oldest and remove from the `HashSet`.

- [ ] **Step 1: Write failing test**

Add to `SaveSystemTests.cs`:

```csharp
[Fact]
public void ExploredTiles_DoesNotExceedCap()
{
    var gs = new GameState(seed: 1);
    // Add 5000 tiles directly
    for (int x = 0; x < 100; x++)
        for (int y = 0; y < 50; y++)
            gs.ExploredTiles.Add($"{x},{y}");

    Assert.True(gs.ExploredTiles.Count <= 4096, $"Expected <= 4096 tiles, got {gs.ExploredTiles.Count}");
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test src/engine/RPC.Tests/RPC.Tests.csproj --filter "ExploredTiles_DoesNotExceedCap"
```

Expected: FAIL (HashSet grows uncapped to 5000).

- [ ] **Step 3: Replace ExploredTiles with a capped set in GameState**

In `src/engine/RPC.Engine/GameState.cs`, replace the `ExploredTiles` property and add a backing queue. Add `using System.Collections.Generic;` if not already present (it is).

Replace:
```csharp
public HashSet<string> ExploredTiles { get; } = new();
```

With:
```csharp
private readonly HashSet<string> _exploredTilesSet = new();
private readonly Queue<string> _exploredTilesOrder = new();
private const int MaxExploredTiles = 4096;

public BoundedTileSet ExploredTiles { get; }
```

And add a nested struct (or inner class) at the bottom of the file, outside the `GameState` class:

```csharp
public class BoundedTileSet
{
    private readonly HashSet<string> _set;
    private readonly Queue<string> _order;
    private readonly int _max;

    public BoundedTileSet(HashSet<string> set, Queue<string> order, int max)
    {
        _set = set;
        _order = order;
        _max = max;
    }

    public int Count => _set.Count;

    public void Add(string key)
    {
        if (_set.Contains(key)) return;
        if (_set.Count >= _max)
        {
            var oldest = _order.Dequeue();
            _set.Remove(oldest);
        }
        _set.Add(key);
        _order.Enqueue(key);
    }

    public void Clear()
    {
        _set.Clear();
        _order.Clear();
    }

    public bool Contains(string key) => _set.Contains(key);

    public IEnumerable<string> AsEnumerable() => _set;

    // Allow foreach
    public IEnumerator<string> GetEnumerator() => _set.GetEnumerator();
}
```

Then in the `GameState` constructor, initialize:
```csharp
ExploredTiles = new BoundedTileSet(_exploredTilesSet, _exploredTilesOrder, MaxExploredTiles);
```

And remove the old field declaration.

- [ ] **Step 4: Fix SaveSystem usage of ExploredTiles.ToArray()**

In `SaveSystem.cs`, `Save` calls `state.ExploredTiles.ToArray()`. Since `BoundedTileSet` doesn't implement `IEnumerable<string>` with `ToArray`, fix by calling `.AsEnumerable().ToArray()`:

```csharp
// BEFORE:
ExploredTiles = state.ExploredTiles.ToArray(),

// AFTER:
ExploredTiles = state.ExploredTiles.AsEnumerable().ToArray(),
```

And `Load` calls `state.ExploredTiles.Add(tile)` — that still works since `BoundedTileSet` has `Add`.

Also fix `CreateStateMessage` in `GameServer.cs` which calls `foreach (var key in _gameState.ExploredTiles)` — `BoundedTileSet` has `GetEnumerator()`, so foreach works.

- [ ] **Step 5: Run all tests**

```bash
dotnet test src/engine/RPC.Tests/RPC.Tests.csproj
```

Expected: 82 passed (81 + 1 new).

---

## Task 12: Fix GameClient exponential reconnect backoff

**Files:**
- Modify: `src/client/src/net/GameClient.ts` — `attemptReconnect` method (line 57-65)

- [ ] **Step 1: Replace linear backoff**

```typescript
// BEFORE:
private attemptReconnect(): void {
  if (this.reconnectAttempts < this.maxReconnectAttempts) {
    this.reconnectAttempts++;
    console.log(`Reconnecting... attempt ${this.reconnectAttempts}`);
    setTimeout(() => this.connect(), 1000 * this.reconnectAttempts);
  } else {
    console.error('Max reconnect attempts reached');
  }
}
```

```typescript
// AFTER:
private attemptReconnect(): void {
  if (this.reconnectAttempts < this.maxReconnectAttempts) {
    this.reconnectAttempts++;
    const delay = Math.min(Math.pow(2, this.reconnectAttempts) * 1000, 30000);
    setTimeout(() => this.connect(), delay);
  } else {
    console.error('Max reconnect attempts reached');
  }
}
```

- [ ] **Step 2: Verify TypeScript build**

```bash
cd src/client && npm run build 2>&1 | tail -5
```

Expected: 0 errors.

---

## Task 13: Commit perf

- [ ] **Step 1: Run tests one more time**

```bash
dotnet test src/engine/RPC.Tests/RPC.Tests.csproj
```

Expected: 82 passed.

- [ ] **Step 2: Stage and commit**

```bash
git add src/engine/RPC.Engine/GameState.cs \
    src/engine/RPC.Engine/Save/SaveSystem.cs \
    src/engine/RPC.Host/Web/GameServer.cs \
    src/engine/RPC.Tests/SaveSystemTests.cs \
    src/client/src/renderer/DungeonRenderer.ts \
    src/client/src/net/GameClient.ts
git commit -m "fix(perf): cap ExploredTiles, fix texture disposal, exponential reconnect backoff"
```

---

## Task 14: Remove DungeonRenderer console.log calls

**Files:**
- Modify: `src/client/src/renderer/DungeonRenderer.ts`

Remove these 9 lines (keep no logs — these fire on every frame/tile update):

| Line (approx) | Content |
|---|---|
| 34 | `console.log('DungeonRenderer: Initializing with container'...)` |
| 62 | `console.log('DungeonRenderer: Canvas appended to container')` |
| 90 | `console.log('DungeonRenderer: Initialization complete')` |
| 205 | `console.log('DungeonRenderer: updateState called...')` |
| 224 | `console.log('DungeonRenderer: Rendering default scene')` |
| 251 | `console.log('DungeonRenderer: Default scene rendered')` |
| 268 | `console.log('DungeonRenderer: Rendering', tiles.length, 'tiles')` |
| 299 | `console.log('DungeonRenderer: Added', added, 'new tiles')` |
| 445 | `console.log('DungeonRenderer: Resizing to'...)` |

- [ ] **Step 1: Remove all 9 console.log lines**

Remove each line. The surrounding code is not affected — the logs are standalone statements.

- [ ] **Step 2: Verify TypeScript build**

```bash
cd src/client && npm run build 2>&1 | tail -5
```

Expected: 0 errors.

---

## Task 15: Remove GameClient debug console.log calls

**Files:**
- Modify: `src/client/src/net/GameClient.ts`

Remove these lines (keep `console.error` calls on lines 39 and 63):

| Line (approx) | Content |
|---|---|
| 15 | `console.log(\`GameClient using server port: ${this.serverPort}\`)` |
| 20 | `console.log('Connecting to WebSocket:', wsUrl)` |
| 26 | `console.log('WebSocket connected successfully')` |
| 34 | `console.log('Received message:', data.type)` |
| 45 | `console.log('WebSocket closed:', event.code, event.reason)` |
| 74 | `console.log('Sending action:', action.type)` |
| 78 | `console.warn('Cannot send action, WebSocket not open')` |

- [ ] **Step 1: Remove the listed console.log/warn lines**

- [ ] **Step 2: Verify TypeScript build**

```bash
cd src/client && npm run build 2>&1 | tail -5
```

Expected: 0 errors.

---

## Task 16: Rename viewRadius → sendRadius in GameServer

**Files:**
- Modify: `src/engine/RPC.Host/Web/GameServer.cs` — `CreateStateMessage` method (line ~500)

`viewRadius = 8` in `CreateStateMessage` is the tile send window. `viewRadius = 3` in `GameState.ExploreAroundPlayer` is the exploration reveal radius. Same name, different semantics. Rename the server variable.

- [ ] **Step 1: Rename in CreateStateMessage**

```csharp
// BEFORE:
var viewRadius = 8;

for (int x = Math.Max(0, px - viewRadius); x < Math.Min(_gameState.CurrentDungeon.Width, px + viewRadius + 1); x++)
{
    for (int y = Math.Max(0, py - viewRadius); y < Math.Min(_gameState.CurrentDungeon.Height, py + viewRadius + 1); y++)
```

```csharp
// AFTER:
var sendRadius = 8;

for (int x = Math.Max(0, px - sendRadius); x < Math.Min(_gameState.CurrentDungeon.Width, px + sendRadius + 1); x++)
{
    for (int y = Math.Max(0, py - sendRadius); y < Math.Min(_gameState.CurrentDungeon.Height, py + sendRadius + 1); y++)
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/engine/RPC.Host/RPC.Host.csproj
```

Expected: 0 errors.

---

## Task 17: Remove dead fetchContent method

**Files:**
- Modify: `src/client/src/net/GameClient.ts`

`fetchContent<T>` (lines 93-99) has zero call sites in the codebase.

- [ ] **Step 1: Delete the method**

Remove lines 93-99:

```typescript
async fetchContent<T>(endpoint: string): Promise<T> {
  const response = await fetch(`/api/${endpoint}`);
  if (!response.ok) {
    throw new Error(`HTTP error! status: ${response.status}`);
  }
  return response.json() as Promise<T>;
}
```

- [ ] **Step 2: Verify TypeScript build**

```bash
cd src/client && npm run build 2>&1 | tail -5
```

Expected: 0 errors.

---

## Task 18: Commit nits

- [ ] **Step 1: Run final test suite**

```bash
dotnet test src/engine/RPC.Tests/RPC.Tests.csproj
```

Expected: 82 passed.

- [ ] **Step 2: Stage and commit**

```bash
git add src/client/src/renderer/DungeonRenderer.ts \
    src/client/src/net/GameClient.ts \
    src/engine/RPC.Host/Web/GameServer.cs
git commit -m "chore(nits): remove debug logs, rename sendRadius, drop dead fetchContent"
```

---

## Verification

- [ ] Final test run: `dotnet test src/engine/RPC.Tests/RPC.Tests.csproj` → 82 passed
- [ ] Client build: `cd src/client && npm run build` → 0 errors
- [ ] `git status` → clean working tree
