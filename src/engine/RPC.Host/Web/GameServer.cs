using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using RPC.Content;
using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Commands;
using RPC.Engine.Combat;
using RPC.Engine.Content;
using RPC.Engine.Dungeons;
using RPC.Engine.Inventory;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Protocol;
using RPC.Engine.Town;

namespace RPC.Host.Web;

public class GameServer
{
    private readonly HttpListener _listener;
    private readonly ClientRegistry _registry = new();
    private readonly GameState _gameState;
    private readonly CancellationTokenSource _cts = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly EncounterTableRegistry _encounterTables;
    private readonly ClassRegistry _classRegistry;
    private readonly ItemRegistry _itemRegistry;
    private readonly SynergyRegistry _synergies;
    private readonly List<RoomSegment> _segments;
    private readonly FileSystemWatcher? _segmentWatcher;
    private readonly GameCommandHandler _commandHandler;
    private readonly StatePresenter _statePresenter;
    private readonly StateBroadcaster _broadcaster;
    private readonly IDungeonGenerator _dungeonGenerator;

    private readonly IContentCatalog _catalog;
    private readonly Dictionary<string, DungeonTemplate> _dungeonTemplates;
    private readonly SemaphoreSlim _gameStateLock = new(1, 1);

    public GameServer(int port = 8080, bool isDev = false)
    {
        _listener = new HttpListener();
        Port = port;
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");

        var rpkPath = FindRpkPath();
        _catalog = rpkPath != null ? new RpkCatalog(rpkPath) : new FileSystemCatalog();
        var contentHash = ReadContentHash(rpkPath);
        LogContentPackInfo(rpkPath, contentHash);

        _encounterTables = LoadEncounterTables(_catalog);
        _classRegistry = LoadClassRegistry(_catalog);
        _itemRegistry = LoadItemRegistry(_catalog);
        _synergies = LoadSynergies(_catalog);
        var factionContent = LoadFactionContent(_catalog);
        var factionRepo = new FactionContentRepository(factionContent);
        var rumorRepo = new RumorRepository(_catalog);
        _gameState = new GameState(encounterTables: _encounterTables, classRegistry: _classRegistry, synergies: _synergies, factionContent: factionRepo, rumors: rumorRepo, dungeonTemplates: _dungeonTemplates);
        _gameState.ContentHash = contentHash;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        _dungeonTemplates = LoadDungeonTemplates(_catalog);
        _segments = LoadSegments(_catalog);
        _dungeonGenerator = new DungeonGenerator(_segments, _dungeonTemplates, _encounterTables);
        _commandHandler = new GameCommandHandler(_gameState, _dungeonGenerator);
        _statePresenter = new StatePresenter(_classRegistry, _itemRegistry);
        _broadcaster = new StateBroadcaster(_registry, _statePresenter, _gameState, _jsonOptions, _cts);
        _gameState.LoadGame(dungeonGenerator: (string type, int? seed) => _dungeonGenerator.Generate(type, seed));
        if (isDev)
        {
            _segmentWatcher = StartSegmentWatcher();
        }
    }

    private static string? FindRpkPath()
    {
        var baseDir = AppContext.BaseDirectory;
        for (int ups = 0; ups <= 8; ups++)
        {
            var parts = new List<string> { baseDir };
            for (int i = 0; i < ups; i++) parts.Add("..");
            parts.Add("content.rpk");
            var candidate = Path.GetFullPath(Path.Combine(parts.ToArray()));
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static string? ReadContentHash(string? rpkPath)
    {
        if (rpkPath == null) return null;
        var manifestPath = Path.Combine(Path.GetDirectoryName(rpkPath)!, "manifest.json");
        if (!File.Exists(manifestPath)) return null;
        try
        {
            var json = File.ReadAllText(manifestPath);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("contentHash").GetString();
        }
        catch
        {
            return null;
        }
    }

    private static void LogContentPackInfo(string? rpkPath, string? contentHash)
    {
        if (rpkPath == null)
        {
            Console.WriteLine("[Content] Running from loose files (no .rpk found)");
            return;
        }

        var manifestPath = Path.Combine(Path.GetDirectoryName(rpkPath)!, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            Console.WriteLine($"[Content] Loaded pack: {rpkPath} (no manifest found)");
            return;
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var version = root.GetProperty("version").GetInt32();
            var hash = root.GetProperty("contentHash").GetString();
            var fileCount = root.GetProperty("files").GetArrayLength();
            Console.WriteLine($"[Content] Loaded pack v{version}, hash {hash?[..16]}.., {fileCount} files ({rpkPath})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Content] Loaded pack: {rpkPath} (manifest read failed: {ex.Message})");
        }
    }

    private static EncounterTableRegistry LoadEncounterTables(IContentCatalog catalog)
    {
        var registry = new EncounterTableRegistry();
        foreach (var file in catalog.EnumerateFiles("encounters", "*.json"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var json = catalog.GetString(file) ?? catalog.GetString($"encounters/{Path.GetFileName(file)}");
            if (json != null)
                registry.LoadFromJson(id, json);
        }
        return registry;
    }

    private static ClassRegistry LoadClassRegistry(IContentCatalog catalog)
    {
        var registry = new ClassRegistry();
        foreach (var file in catalog.EnumerateFiles("classes", "*.json"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var json = catalog.GetString(file) ?? catalog.GetString($"classes/{Path.GetFileName(file)}");
            if (json != null)
                registry.LoadFromJson(id, json);
        }
        return registry;
    }

    private static SynergyRegistry LoadSynergies(IContentCatalog catalog)
    {
        var registry = new SynergyRegistry();
        foreach (var file in catalog.EnumerateFiles("synergies", "*.json"))
        {
            var json = catalog.GetString(file) ?? catalog.GetString($"synergies/{Path.GetFileName(file)}");
            if (json != null)
                registry.LoadFromJson(json);
        }
        return registry;
    }

    private static ItemRegistry LoadItemRegistry(IContentCatalog catalog)
    {
        var registry = new ItemRegistry();
        foreach (var file in catalog.EnumerateFiles("items", "*.json"))
        {
            var json = catalog.GetString(file) ?? catalog.GetString($"items/{Path.GetFileName(file)}");
            if (json != null)
            {
                var items = JsonSerializer.Deserialize<ItemDef[]>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (items != null)
                {
                    foreach (var item in items)
                        registry.Register(item);
                }
            }
        }
        return registry;
    }

    private static List<FactionContentDef> LoadFactionContent(IContentCatalog catalog)
    {
        var defs = new List<FactionContentDef>();
        foreach (var file in catalog.EnumerateFiles("factions", "*.json"))
        {
            var json = catalog.GetString(file) ?? catalog.GetString($"factions/{Path.GetFileName(file)}");
            if (json != null)
            {
                var def = JsonSerializer.Deserialize<FactionContentDef>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true });
                if (def != null)
                    defs.Add(def);
            }
        }
        return defs;
    }

    public int Port { get; private set; }

    public void Start()
    {
        _listener.Start();

        Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context));
            }
        });
    }

    public void Stop()
    {
        _cts.Cancel();
        _listener.Stop();
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";

        if (context.Request.IsWebSocketRequest)
        {
            await HandleWebSocket(context);
        }
        else if (path == "/")
        {
            context.Response.StatusCode = 302;
            context.Response.Headers.Add("Location", "/app");
            context.Response.Close();
        }
        else if (path == "/api/status")
        {
            await HandleStatus(context);
        }
        else if (path == "/api/dungeon")
        {
            await HandleDungeon(context);
        }
        else if (path == "/api/action-log")
        {
            await HandleActionLog(context);
        }
        else if (path.StartsWith("/app") || path.StartsWith("/assets") || path == "/vite.svg" || path == "/favicon.svg")
        {
            await HandleStaticFile(context, path);
        }
        else
        {
            context.Response.StatusCode = 404;
            context.Response.Close();
        }
    }

    private async Task HandleStaticFile(HttpListenerContext context, string path)
    {
        var clientDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "client", "dist");

        string relativePath;
        if (path == "/app" || path == "/app/")
        {
            relativePath = "index.html";
        }
        else if (path.StartsWith("/app/"))
        {
            relativePath = path.Substring(5).TrimStart('/');
        }
        else
        {
            relativePath = path.TrimStart('/');
        }
        if (string.IsNullOrEmpty(relativePath)) relativePath = "index.html";

        var filePath = Path.Combine(clientDir, relativePath);

        var fullClientDir = Path.GetFullPath(clientDir);
        var fullFilePath = Path.GetFullPath(filePath);
        if (!fullFilePath.StartsWith(fullClientDir))
        {
            context.Response.StatusCode = 403;
            context.Response.Close();
            return;
        }

        if (!File.Exists(filePath))
        {
            filePath = Path.Combine(clientDir, "index.html");
            if (!File.Exists(filePath))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        context.Response.ContentType = extension switch
        {
            ".html" => "text/html",
            ".js" => "application/javascript",
            ".css" => "text/css",
            ".json" => "application/json",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };

        if (extension == ".html")
        {
            var content = await File.ReadAllTextAsync(filePath);
            content = content.Replace(
                "</head>",
                $"<script>window.SERVER_PORT = {Port};</script></head>"
            );
            var bytes = Encoding.UTF8.GetBytes(content);
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
        }
        else
        {
            var bytes = await File.ReadAllBytesAsync(filePath);
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
        }

        context.Response.Close();
    }

    private async Task HandleWebSocket(HttpListenerContext context)
    {
        var wsContext = await context.AcceptWebSocketAsync(null);
        var socket = wsContext.WebSocket;
        var client = new ClientConnection(socket);
        _registry.Add(client);

        try
        {
            await _broadcaster.SendEnvelope(client, new ProtocolEnvelope
        {
            V = 2,
            Type = "hello",
            Seq = client.NextServerSeq(),
            Payload = new HelloPayload { ProtocolVersion = 2, SessionId = client.SessionId }
        });
            _ = Task.Run(() => RunHeartbeatLoop(client));

            var buffer = new byte[4096];
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
                    await HandleMessage(client, message);
                }
            }
        }
        catch (WebSocketException) { }
        finally
        {
            client.Dispose();
            _registry.Remove(client);
        }
    }

    private async Task RunHeartbeatLoop(ClientConnection client)
    {
        try
        {
            while (client.Socket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token);
                if (client.Socket.State != WebSocketState.Open) break;
                if (!client.IsReady) continue;

                var pingSeq = client.NextPingSeq();
                client.LastPingSeq = pingSeq;
                client.LastPingTime = DateTime.UtcNow;
                await SendPing(client, pingSeq);

                // Wait up to 2s for pong
                var pongCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try
                {
                    while (client.Socket.State == WebSocketState.Open && !pongCts.Token.IsCancellationRequested)
                    {
                        if (client.LastPongSeq >= pingSeq)
                            break;
                        await Task.Delay(100, pongCts.Token);
                    }
                }
                catch (TaskCanceledException) { }

                if (client.LastPongSeq < pingSeq && client.Socket.State == WebSocketState.Open)
                {
                    await client.Socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Heartbeat timeout", CancellationToken.None);
                    break;
                }
            }
        }
        catch (WebSocketException) { }
        catch (OperationCanceledException) { }
    }

    private async Task SendPing(ClientConnection client, int pingSeq)
    {
        await _broadcaster.SendEnvelope(client, new ProtocolEnvelope
        {
            V = 2,
            Type = "heartbeat.ping",
            Seq = client.NextServerSeq(),
            Payload = new HeartbeatPingPayload { PingSeq = pingSeq }
        });
    }

    private async Task SendError(ClientConnection client, string code, string message, bool recoverable, int? ackSeq = null)
    {
        await _broadcaster.SendEnvelope(client, new ProtocolEnvelope
        {
            V = 2,
            Type = "error",
            Seq = client.NextServerSeq(),
            AckSeq = ackSeq,
            Payload = new ErrorPayload { Code = code, Message = message, Recoverable = recoverable }
        });
    }

    private async Task HandleMessage(ClientConnection client, string message)
    {
        ProtocolEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<ProtocolEnvelope>(message, _jsonOptions);
        }
        catch (JsonException)
        {
            await SendError(client, "malformed_payload", "Malformed JSON envelope", recoverable: true);
            return;
        }

        if (envelope == null || envelope.V != 2 || string.IsNullOrEmpty(envelope.Type))
        {
            await SendError(client, "malformed_payload", "Invalid envelope: missing version or type", recoverable: true);
            return;
        }

        if (envelope.Type == "ready")
        {
            client.IsReady = true;
            await _broadcaster.SendState(client);
            return;
        }

        if (envelope.Type == "heartbeat.pong")
        {
            if (envelope.Payload is JsonElement json && json.TryGetProperty("pingSeq", out var pingSeqEl))
            {
                client.LastPongSeq = pingSeqEl.GetInt32();
            }
            return;
        }

        if (envelope.Type == "analytics.request")
        {
            var data = _gameState.Analytics.GetData();
            var response = new
            {
                campaignsStarted = data.CampaignsStarted,
                campaignsCompleted = data.CampaignsCompleted,
                mastermindsExposed = data.MastermindsExposed,
                schemesStopped = data.SchemesStopped,
                betrayals = data.Betrayals,
                totalTurns = data.TotalTurns,
                totalDeaths = data.TotalDeaths,
                synergiesDiscovered = data.SynergiesDiscovered.ToArray(),
                classesPlayed = data.ClassesPlayed.ToArray(),
                branchesChosen = data.BranchesChosen.ToArray(),
                optionalDungeonsUnlocked = data.OptionalDungeonsUnlocked.ToArray()
            };
            var responseEnvelope = new ProtocolEnvelope { V = 2, Type = "analytics.data", Payload = response, Seq = 0 };
            var json = JsonSerializer.Serialize(responseEnvelope, _jsonOptions);
            await client.Socket.SendAsync(
                new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes(json)),
                WebSocketMessageType.Text,
                true,
                _cts.Token);
            return;
        }

        if (!client.IsReady)
        {
            await SendError(client, "not_ready", "Client must send ready before other messages", recoverable: true, ackSeq: envelope.Seq);
            return;
        }

        if (envelope.Type != "action")
        {
            await SendError(client, "invalid_action", $"Unknown message type: {envelope.Type}", recoverable: true, ackSeq: envelope.Seq);
            return;
        }

        try
        {
            if (envelope.Payload is not JsonElement payloadJson)
            {
                await SendError(client, "malformed_payload", "Action payload missing", recoverable: true, ackSeq: envelope.Seq);
                return;
            }

            var action = JsonSerializer.Deserialize<PlayerAction>(payloadJson.GetRawText(), _jsonOptions);
            if (action == null)
            {
                await SendError(client, "malformed_payload", "Unable to parse action payload", recoverable: true, ackSeq: envelope.Seq);
                return;
            }

            ICommand cmd;
            try
            {
                cmd = CommandDispatcher.Parse(action);
            }
            catch (ArgumentException ex)
            {
                await SendError(client, "invalid_action", ex.Message, recoverable: true, ackSeq: envelope.Seq);
                return;
            }

            object? snapshot = null;
            bool stateChanged = false;
            bool clearCombatResult = false;
            await _gameStateLock.WaitAsync(_cts.Token);
            try
            {
                var result = _commandHandler.Execute(cmd);
                stateChanged = result.StateChanged;
                clearCombatResult = result.ClearCombatResult;

                if (stateChanged)
                {
                    snapshot = _statePresenter.CreateStateMessage(_gameState);
                }

                if (clearCombatResult)
                {
                    _gameState.ClearCombatResult();
                }
            }
            finally
            {
                _gameStateLock.Release();
            }

            if (stateChanged && snapshot != null)
            {
                await _broadcaster.SendState(client, envelope.Seq, snapshot);
                await _broadcaster.BroadcastState(excludeClient: client, payload: snapshot);
            }
        }
        catch (Exception ex)
        {
            await SendError(client, "internal_error", $"Internal error processing action: {ex.Message}", recoverable: true, ackSeq: envelope.Seq);
        }
    }

    private static readonly JsonSerializerOptions _segmentOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static List<RoomSegment> LoadSegments(IContentCatalog catalog)
    {
        var segments = new List<RoomSegment>();
        foreach (var dir in new[] { "segments", "segments/broken-engine", "segments/bloom-site", "segments/boneyard", "segments/sealed-vault", "segments/settlement-gone-wrong", "segments/ossuary" })
        {
            foreach (var file in catalog.EnumerateFiles(dir, "*.json"))
            {
                var json = catalog.GetString(file) ?? catalog.GetString($"{dir.TrimEnd('/')}/{Path.GetFileName(file)}");
                if (json != null)
                {
                    var segment = JsonSerializer.Deserialize<RoomSegment>(json, _segmentOptions);
                    if (segment != null)
                        segments.Add(segment);
                }
            }
        }
        return segments;
    }

    private static Dictionary<string, DungeonTemplate> LoadDungeonTemplates(IContentCatalog catalog)
    {
        var templates = new Dictionary<string, DungeonTemplate>();
        foreach (var file in catalog.EnumerateFiles("campaigns/dungeons", "*.json"))
        {
            var json = catalog.GetString(file);
            if (json != null)
            {
                var template = JsonSerializer.Deserialize<DungeonTemplate>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true });
                if (template != null)
                    templates[template.Id] = template;
            }
        }
        return templates;
    }

    private FileSystemWatcher? StartSegmentWatcher()
    {
        if (_catalog is not FileSystemCatalog fs) return null;
        var dir = Path.Combine(fs.BaseDirectory, "segments", "broken-engine");
        if (!Directory.Exists(dir)) return null;

        var watcher = new FileSystemWatcher(dir, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };
        watcher.Changed += (_, _) => ReloadSegments();
        watcher.Created += (_, _) => ReloadSegments();
        watcher.Deleted += (_, _) => ReloadSegments();
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private void ReloadSegments()
    {
        try
        {
            var reloaded = LoadSegments(_catalog);
            _segments.Clear();
            _segments.AddRange(reloaded);
            _ = _broadcaster.BroadcastContentReload();
        }
        catch { }
    }

    private async Task HandleStatus(HttpListenerContext context)
    {
        var response = new { status = "ok", timestamp = DateTime.UtcNow };
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private async Task HandleDungeon(HttpListenerContext context)
    {
        var segment = new
        {
            id = "test_room",
            name = "Test Chamber",
            width = 3,
            height = 3,
            tiles = new[]
            {
                new { x = 0, y = 0, type = "floor", north = "wall", south = "none", east = "none", west = "wall" },
                new { x = 1, y = 0, type = "floor", north = "wall", south = "none", east = "none", west = "none" },
                new { x = 2, y = 0, type = "floor", north = "wall", south = "none", east = "wall", west = "none" },
                new { x = 0, y = 1, type = "floor", north = "none", south = "wall", east = "none", west = "wall" },
                new { x = 1, y = 1, type = "floor", north = "none", south = "wall", east = "none", west = "none" },
                new { x = 2, y = 1, type = "floor", north = "none", south = "wall", east = "wall", west = "none" },
            }
        };

        var json = JsonSerializer.Serialize(segment, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private async Task HandleActionLog(HttpListenerContext context)
    {
        var response = new
        {
            events = _gameState.ActionLog.Select(e => new
            {
                turn = e.Turn,
                category = e.Category,
                type = e.Type,
                payload = e.Payload
            }).ToArray()
        };
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }
}
