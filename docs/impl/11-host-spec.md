# 殼之詳 — Photino Host Specification

> Photino 為舟，載核與形。舟穩則遠航。

## 1. Photino 啟動流程

```
Main()
  ├── 載入設定 (appsettings.json + KDL 覆蓋)
  ├── 啟動 ASP.NET Core (Kestrel, localhost:0)
  ├── 初始化 ContentLoader (dev=JSON, release=.rpk)
  ├── 初始化 GameEngine
  ├── 建立 PhotinoWindow
  │     ├── 標題: "The Reach"
  │     ├── 尺寸: 1280×720 (可調)
  │     ├── 最小: 1024×768
  │     ├── 圖示: assets/icon.ico
  │     └── 載入: http://localhost:{port}/
  ├── 註冊視窗事件 (Closing → 存檔提示)
  └── window.WaitForClose()
```

## 2. 視窗配置

```csharp
var window = new PhotinoWindow()
    .SetTitle("The Reach")
    .SetIconFile("assets/icon.ico")
    .SetSize(1280, 720)
    .SetUseOsDefaultSize(false)
    .Center()
    .SetResizable(true)
    .SetDevToolsEnabled(_env.IsDevelopment)
    .RegisterWindowClosingHandler((sender, e) =>
    {
        if (_engine.HasUnsavedChanges)
        {
            var result = PhotinoDialog.Confirm("有未儲存進度，是否儲存？");
            if (result == DialogResult.Yes) _saveManager.Save(0, _engine.State);
        }
        return false; // 允許關閉
    });
```

## 3. ASP.NET 服務配置

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0"); // 隨機端口，僅本地

builder.Services.AddSingleton<GameEngine>();
builder.Services.AddSingleton<ContentLoader>();
builder.Services.AddSingleton<WebSocketManager>();
builder.Services.AddSingleton<SaveManager>();

// 開發模式：啟用內容熱重載
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<ContentWatchService>();
}

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(5)
});

app.MapGet("/", () => Results.File("wwwroot/index.html", "text/html"));
app.MapStaticAssets(); // 前端打包輸出
app.MapContentEndpoints("/content");
app.MapWebSocketHandler("/ws");
app.MapSaveEndpoints("/save");
```

## 4. WebSocket 管理器

```csharp
public class WebSocketManager
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new();
    private readonly GameEngine _engine;

    public async Task HandleConnection(HttpContext ctx)
    {
        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        var id = Guid.NewGuid();
        _clients[id] = ws;

        // 發送當前完整狀態
        await SendState(ws, _engine.State);

        var buffer = new byte[4096];
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) break;

            var message = Decode(buffer, result.Count);
            var (newState, responses) = _engine.Process(message);

            foreach (var resp in responses)
                await Broadcast(resp);
        }

        _clients.TryRemove(id, out _);
    }

    public async Task Broadcast(ServerMessage msg)
    {
        var bytes = Encode(msg);
        foreach (var ws in _clients.Values)
        {
            if (ws.State == WebSocketState.Open)
                await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
```

## 5. 開發 vs 發布模式

| 功能 | Development | Release |
|---|---|---|
| 內容載入 | 直讀 JSON | .rpk 二進位 |
| 熱重載 | 檔案監視 + WS 通知 | 無 |
| DevTools | F12 可開 | 禁用 |
| 日誌 | Console + Debug | File |
| WS 協議 | JSON 可讀 | JSON（同） |
| 前端來源 | Vite dev server (`npm run dev`) | 靜態檔案 |

### 開發模式啟動腳本

```bash
# Terminal 1
cd src/client && npm run dev

# Terminal 2
cd src/engine/RPC.Host && dotnet run --environment Development
# Host 會自動探測 http://localhost:5173 (Vite 預設) 並載入
```

### 發布模式建置

```bash
# 1. 編譯內容
dotnet run --project tools/content-pack -- compile content/ src/engine/RPC.Host/Content/

# 2. 建置前端
cd src/client && npm run build
# 輸出至 src/engine/RPC.Host/wwwroot/

# 3. 發布 Host
cd src/engine/RPC.Host && dotnet publish -c Release -r win-x64 --self-contained
# 輸出：RPC.Host.exe + .rpk + wwwroot/
```

## 6. 跨平台注意

Photino 支援 Windows、macOS、Linux。

```csharp
var platform = RuntimeInformation.OSDescription switch
{
    var d when d.Contains("Windows") => "win",
    var d when d.Contains("Linux") => "linux",
    var d when d.Contains("Darwin") or d.Contains("macOS") => "osx",
    _ => "unknown"
};

// 存檔路徑依平台
var saveDir = platform switch
{
    "win" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TheReach"),
    "linux" => Path.Combine(Environment.GetEnvironmentVariable("HOME")!, ".local/share/TheReach"),
    "osx" => Path.Combine(Environment.GetEnvironmentVariable("HOME")!, "Library/Application Support/TheReach"),
    _ => "saves/"
};
```

## 7. 錯誤處理與崩潰恢復

```csharp
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    var ex = (Exception)e.ExceptionObject;
    File.AppendAllText("crash.log", $"[{DateTime.Now}] {ex}\n");

    if (_engine?.HasUnsavedChanges == true)
    {
        _saveManager.Save(999, _engine.State); // 緊急存檔槽
    }
};
```

## 8. 瀏覽器模式（無 Photino）

雖主要目標為 Photino 桌面，架構允許純瀏覽器運行：

```bash
# 獨立啟動 Host
dotnet run --project src/engine/RPC.Host
# 用任何瀏覽器開 http://localhost:5000
```

此模式用於：
- 快速測試（不開 Photino）
- 遠端除錯（手機、其他電腦）
- 未來可能的純瀏覽器發布

前端無需區分 Photino / 瀏覽器，僅 WS URL 不同：
```typescript
const WS_URL = (window as any).photino
  ? `ws://${location.host}/ws`
  : `ws://${location.host}/ws`;
// 實際相同，Photino 注入的 window.photino 僅供原生 API 呼叫用
```
