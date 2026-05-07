using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Host.Web;

public class GameServer
{
    private readonly HttpListener _listener;
    private readonly List<WebSocket> _clients = new();
    private readonly GameState _gameState;
    private readonly CancellationTokenSource _cts = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly EncounterTableRegistry _encounterTables;
    private readonly ClassRegistry _classRegistry;

    public GameServer(int port = 8080)
    {
        _listener = new HttpListener();
        Port = port;
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _encounterTables = LoadEncounterTables();
        _classRegistry = LoadClassRegistry();
        _gameState = new GameState(encounterTables: _encounterTables, classRegistry: _classRegistry);
        _gameState.LoadGame();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
    }

    private static string? FindContentDir(params string[] subPath)
    {
        var baseDir = AppContext.BaseDirectory;
        for (int ups = 0; ups <= 8; ups++)
        {
            var parts = new List<string> { baseDir };
            for (int i = 0; i < ups; i++) parts.Add("..");
            parts.AddRange(subPath);
            var candidate = Path.GetFullPath(Path.Combine(parts.ToArray()));
            if (Directory.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static EncounterTableRegistry LoadEncounterTables()
    {
        var registry = new EncounterTableRegistry();
        var fullDir = FindContentDir("content", "encounters");
        if (fullDir != null)
        {
            foreach (var file in Directory.EnumerateFiles(fullDir, "*.json"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                var json = File.ReadAllText(file);
                registry.LoadFromJson(id, json);
            }
        }
        return registry;
    }

    private static ClassRegistry LoadClassRegistry()
    {
        var registry = new ClassRegistry();
        var fullDir = FindContentDir("content", "classes");
        if (fullDir != null)
        {
            foreach (var file in Directory.EnumerateFiles(fullDir, "*.json"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                var json = File.ReadAllText(file);
                registry.LoadFromJson(id, json);
            }
        }
        return registry;
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
        // Map /app to the built frontend directory
        // AppContext.BaseDirectory is bin/Debug/net9.0/, so we need to go up 5 levels to reach src/client/dist
        var clientDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "client", "dist");
        
        // Map URL path to file path
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
            // Handle /assets/, /vite.svg, etc. directly
            relativePath = path.TrimStart('/');
        }
        if (string.IsNullOrEmpty(relativePath)) relativePath = "index.html";
        
        var filePath = Path.Combine(clientDir, relativePath);
        
        // Security check - ensure we're not escaping the client dir
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
            // Try index.html for SPA routing
            filePath = Path.Combine(clientDir, "index.html");
            if (!File.Exists(filePath))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }
        }
        
        // Set content type
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
        
        // Inject SERVER_PORT into HTML files
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
        
        lock (_clients)
        {
            _clients.Add(socket);
        }

        // Send initial state
        await SendState(socket);

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
        catch (WebSocketException) { }
        finally
        {
            lock (_clients)
            {
                _clients.Remove(socket);
            }
            socket.Dispose();
        }
    }

    private async Task HandleMessage(WebSocket socket, string message)
    {
        try
        {
            var action = JsonSerializer.Deserialize<PlayerAction>(message, _jsonOptions);
            if (action == null) return;

            bool stateChanged = false;
            
            switch (action.Type)
            {
                case "move_forward":
                    stateChanged = _gameState.TryMoveForward();
                    break;
                case "turn_left":
                    _gameState.TurnLeft();
                    stateChanged = true;
                    break;
                case "turn_right":
                    _gameState.TurnRight();
                    stateChanged = true;
                    break;
                case "generate_dungeon":
                    GenerateDungeon("broken_engine");
                    stateChanged = true;
                    break;
                case "combat_action":
                    if (action.Action != null)
                    {
                        stateChanged = _gameState.SubmitCombatAction(action.Action);
                    }
                    break;
                case "flee_combat":
                    _gameState.FleeCombat();
                    stateChanged = true;
                    break;
                case "enter_combat":
                    Console.WriteLine("ENTER_COMBAT triggered");
                    _gameState.TriggerEncounter();
                    Console.WriteLine($"ENTER_COMBAT result: Mode={_gameState.Mode}, Combat={_gameState.Combat != null}");
                    stateChanged = true;
                    break;
                case "enter_dungeon":
                    var dungeonType = action.DungeonType ?? "broken_engine";
                    GenerateDungeon(dungeonType);
                    stateChanged = true;
                    break;
                case "rest":
                    _gameState.RestAtInn();
                    stateChanged = true;
                    break;
                case "return_to_town":
                    _gameState.ReturnToTown();
                    stateChanged = true;
                    break;
                case "save_game":
                    _gameState.SaveGame();
                    stateChanged = true;
                    break;
            }

            if (stateChanged)
            {
                await BroadcastState();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling message: {ex}");
        }
    }

    private void GenerateDungeon(string dungeonType)
    {
        var seed = dungeonType.GetHashCode();
        var builder = new DungeonBuilder(seed: seed);
        
        // Add some basic room segments
        builder.AddSegment(CreateEntranceRoom());
        builder.AddSegment(CreateCorridor());
        builder.AddSegment(CreateChamber());
        builder.AddSegment(CreateDeadEnd());
        
        var dungeonNames = new Dictionary<string, string>
        {
            ["broken_engine"] = "Broken Engine",
            ["crypt"] = "Crypt of Whispers",
            ["sewers"] = "Sewer Warrens"
        };
        
        var name = dungeonNames.GetValueOrDefault(dungeonType, dungeonType);
        var dungeon = builder.Build(name, 8);
        dungeon.EncounterTableId = dungeonType;
        _gameState.EnterDungeon(dungeon, dungeonType);
    }

    private static RoomSegment CreateEntranceRoom()
    {
        return new RoomSegment
        {
            Id = "entrance",
            Name = "Entrance Hall",
            Tags = new() { "entrance" },
            Tiles = new()
            {
                new() { X = 0, Y = 0, Type = TileType.Floor },
                new() { X = 1, Y = 0, Type = TileType.Floor },
                new() { X = 2, Y = 0, Type = TileType.Floor },
                new() { X = 0, Y = 1, Type = TileType.Floor },
                new() { X = 1, Y = 1, Type = TileType.Floor },
                new() { X = 2, Y = 1, Type = TileType.Floor },
                new() { X = 0, Y = 2, Type = TileType.Wall },
                new() { X = 1, Y = 2, Type = TileType.Wall },
                new() { X = 2, Y = 2, Type = TileType.Wall },
                new() { X = 1, Y = -1, Type = TileType.Floor, IsExit = true, ExitDirection = Direction.North },
            }
        };
    }

    private static RoomSegment CreateCorridor()
    {
        return new RoomSegment
        {
            Id = "corridor",
            Name = "Corridor",
            Tiles = new()
            {
                new() { X = 0, Y = 0, Type = TileType.Floor, IsExit = true, ExitDirection = Direction.South },
                new() { X = 0, Y = -1, Type = TileType.Floor },
                new() { X = 0, Y = -2, Type = TileType.Floor },
                new() { X = 0, Y = -3, Type = TileType.Floor, IsExit = true, ExitDirection = Direction.North },
            }
        };
    }

    private static RoomSegment CreateChamber()
    {
        return new RoomSegment
        {
            Id = "chamber",
            Name = "Small Chamber",
            Tiles = new()
            {
                new() { X = 0, Y = 1, Type = TileType.Floor, IsExit = true, ExitDirection = Direction.South },
                new() { X = -1, Y = 0, Type = TileType.Floor },
                new() { X = 0, Y = 0, Type = TileType.Floor },
                new() { X = 1, Y = 0, Type = TileType.Floor },
                new() { X = -1, Y = -1, Type = TileType.Floor },
                new() { X = 0, Y = -1, Type = TileType.Floor },
                new() { X = 1, Y = -1, Type = TileType.Floor },
                new() { X = -1, Y = -2, Type = TileType.Wall },
                new() { X = 0, Y = -2, Type = TileType.Wall },
                new() { X = 1, Y = -2, Type = TileType.Wall },
                new() { X = -2, Y = 0, Type = TileType.Wall },
                new() { X = -2, Y = -1, Type = TileType.Wall },
                new() { X = 2, Y = 0, Type = TileType.Wall },
                new() { X = 2, Y = -1, Type = TileType.Wall },
            }
        };
    }

    private static RoomSegment CreateDeadEnd()
    {
        return new RoomSegment
        {
            Id = "dead_end",
            Name = "Dead End",
            Tiles = new()
            {
                new() { X = 0, Y = 1, Type = TileType.Floor, IsExit = true, ExitDirection = Direction.South },
                new() { X = 0, Y = 0, Type = TileType.Floor },
                new() { X = -1, Y = 0, Type = TileType.Wall },
                new() { X = 1, Y = 0, Type = TileType.Wall },
                new() { X = 0, Y = -1, Type = TileType.Wall },
            }
        };
    }

    private async Task SendState(WebSocket socket)
    {
        var state = CreateStateMessage();
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        if (socket.State == WebSocketState.Open)
        {
            await socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cts.Token);
        }
    }

    private async Task BroadcastState()
    {
        var state = CreateStateMessage();
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        List<WebSocket> clients;
        lock (_clients)
        {
            clients = _clients.ToList();
        }

        foreach (var client in clients)
        {
            try
            {
                if (client.State == WebSocketState.Open)
                {
                    await client.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        true,
                        _cts.Token);
                }
            }
            catch { }
        }
    }

    private object CreateStateMessage()
    {
        var tiles = new List<object>();
        var explored = new List<object>();
        if (_gameState.CurrentDungeon != null)
        {
            // Only send visible tiles around player
            var px = _gameState.Player.Position.X;
            var py = _gameState.Player.Position.Y;
            var viewRadius = 8;
            
            for (int x = Math.Max(0, px - viewRadius); x < Math.Min(_gameState.CurrentDungeon.Width, px + viewRadius + 1); x++)
            {
                for (int y = Math.Max(0, py - viewRadius); y < Math.Min(_gameState.CurrentDungeon.Height, py + viewRadius + 1); y++)
                {
                    var tile = _gameState.CurrentDungeon.Tiles[x, y];
                    if (tile.Type != TileType.Empty)
                    {
                        tiles.Add(new { x, y, type = tile.Type.ToString() });
                    }
                }
            }

            // Send explored tiles for automap
            foreach (var key in _gameState.ExploredTiles)
            {
                var parts = key.Split(',');
                var x = int.Parse(parts[0]);
                var y = int.Parse(parts[1]);
                var tile = _gameState.CurrentDungeon.Tiles[x, y];
                explored.Add(new { x, y, type = tile.Type.ToString() });
            }
        }

        var party = _gameState.Party.Members.Select((c, i) => new
        {
            slot = i,
            name = c.Name,
            classId = c.ClassId,
            level = c.Level,
            hp = c.CurrentHp,
            maxHp = c.GetEffectiveStats().MaxHp,
            row = c.Row,
            alive = c.IsAlive
        }).ToArray();

        object? combat = null;
        if (_gameState.Mode == GameMode.Combat && _gameState.Combat != null)
        {
            var c = _gameState.Combat;
            combat = new
            {
                phase = c.Phase.ToString(),
                round = c.Round,
                combatants = c.Combatants.Select(x => new
                {
                    id = x.Id,
                    name = x.Name,
                    isPlayer = x.IsPlayer,
                    hp = x.Hp,
                    maxHp = x.MaxHp,
                    speed = x.Speed,
                    row = x.Row,
                    alive = x.IsAlive,
                    isCurrent = c.CurrentActor?.Id == x.Id
                }).ToArray(),
                initiativeOrder = c.InitiativeOrder,
                currentTurnIndex = c.CurrentTurnIndex,
                log = c.Log.Select(l => new { actor = l.ActorId, message = l.Message, round = l.Round }).ToArray(),
                isFinished = c.IsFinished
            };
        }

        object? combatResult = null;
        if (_gameState.LastCombatResult != null)
        {
            var r = _gameState.LastCombatResult;
            combatResult = new
            {
                victory = r.Victory,
                xpGained = r.XpGained,
                levelUps = r.LevelUps,
                roundCount = r.RoundCount
            };
        }

        var state = new
        {
            type = "state",
            mode = _gameState.Mode.ToString(),
            player = new
            {
                x = _gameState.Player.Position.X,
                y = _gameState.Player.Position.Y,
                facing = _gameState.Player.Facing.ToString()
            },
            tiles,
            explored,
            hasDungeon = _gameState.CurrentDungeon != null,
            party,
            combat,
            combatResult
        };

        // Clear one-shot combat result after including it in state
        _gameState.ClearCombatResult();

        return state;
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
        // Return a sample room segment for testing
        var segment = new
        {
            id = "test_room",
            name = "Test Chamber",
            width = 3,
            height = 3,
            tiles = new[]
            {
                new { x = 0, y = 0, type = "floor" },
                new { x = 1, y = 0, type = "floor" },
                new { x = 2, y = 0, type = "floor" },
                new { x = 0, y = 1, type = "floor" },
                new { x = 1, y = 1, type = "floor" },
                new { x = 2, y = 1, type = "floor" },
                new { x = 0, y = 2, type = "wall" },
                new { x = 1, y = 2, type = "wall" },
                new { x = 2, y = 2, type = "wall" },
            }
        };
        
        var json = JsonSerializer.Serialize(segment, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }
}

public class PlayerAction
{
    public string Type { get; set; } = "";
    public CombatAction? Action { get; set; }
    public string? DungeonType { get; set; }
}
