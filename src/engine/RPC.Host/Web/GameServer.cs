using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Content;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;

namespace RPC.Host.Web;

public class GameServer
{
    private readonly HttpListener _listener;
    private readonly List<ClientConnection> _clients = new();
    private readonly GameState _gameState;
    private readonly CancellationTokenSource _cts = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly EncounterTableRegistry _encounterTables;
    private readonly ClassRegistry _classRegistry;
    private readonly ItemRegistry _itemRegistry;

    public GameServer(int port = 8080)
    {
        _listener = new HttpListener();
        Port = port;
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _encounterTables = LoadEncounterTables();
        _classRegistry = LoadClassRegistry();
        _itemRegistry = LoadItemRegistry();
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

    private static ItemRegistry LoadItemRegistry()
    {
        var registry = new ItemRegistry();
        var fullDir = FindContentDir("content", "items");
        if (fullDir != null)
        {
            foreach (var file in Directory.EnumerateFiles(fullDir, "*.json"))
            {
                var json = File.ReadAllText(file);
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

    private static readonly Dictionary<string, string> ClassColors = new()
    {
        ["bonewarden"] = "#8B7355",
        ["stillblade"] = "#6B8E9F",
        ["cauterist"] = "#B85C38",
        ["hollow"] = "#6B6B6B",
    };

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

        lock (_clients)
        {
            _clients.Add(client);
        }

        try
        {
            await SendHello(client);
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
            lock (_clients)
            {
                _clients.Remove(client);
            }
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

    private async Task SendHello(ClientConnection client)
    {
        var envelope = new ProtocolEnvelope
        {
            V = 2,
            Type = "hello",
            Seq = client.NextServerSeq(),
            Payload = new HelloPayload { ProtocolVersion = 2, SessionId = client.SessionId }
        };
        await SendEnvelope(client.Socket, envelope);
    }

    private async Task SendPing(ClientConnection client, int pingSeq)
    {
        var envelope = new ProtocolEnvelope
        {
            V = 2,
            Type = "heartbeat.ping",
            Seq = client.NextServerSeq(),
            Payload = new HeartbeatPingPayload { PingSeq = pingSeq }
        };
        await SendEnvelope(client.Socket, envelope);
    }

    private async Task SendError(ClientConnection client, string code, string message, bool recoverable, int? ackSeq = null)
    {
        var envelope = new ProtocolEnvelope
        {
            V = 2,
            Type = "error",
            Seq = client.NextServerSeq(),
            AckSeq = ackSeq,
            Payload = new ErrorPayload { Code = code, Message = message, Recoverable = recoverable }
        };
        await SendEnvelope(client.Socket, envelope);
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
            await SendState(client);
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

            bool stateChanged = false;

            switch (action.Type)
            {
                case "move_forward":
                    stateChanged = _gameState.TryMoveForward();
                    break;
                case "move_back":
                    stateChanged = _gameState.TryMoveBack();
                    break;
                case "strafe_left":
                    stateChanged = _gameState.TryStrafeLeft();
                    break;
                case "strafe_right":
                    stateChanged = _gameState.TryStrafeRight();
                    break;
                case "turn_left":
                    _gameState.TurnLeft();
                    stateChanged = true;
                    break;
                case "turn_right":
                    _gameState.TurnRight();
                    stateChanged = true;
                    break;
                case "cancel":
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
                    _gameState.TriggerEncounter();
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
                case "reset_game":
                    _gameState.Reset();
                    stateChanged = true;
                    break;
                case "swap_row":
                    if (action.Slot is int slot)
                    {
                        _gameState.Party.SwapRows(slot);
                        stateChanged = true;
                    }
                    break;
                case "tavern_recruit":
                    if (action.TargetId is string recruitId)
                    {
                        stateChanged = _gameState.RecruitFromTavern(recruitId);
                    }
                    break;
                case "mission_accept":
                    if (action.TargetId is string missionId)
                    {
                        stateChanged = _gameState.AcceptMission(missionId);
                    }
                    break;
                case "vendor_purchase":
                    if (action.TargetId is string itemId)
                    {
                        stateChanged = _gameState.PurchaseVendorItem(itemId);
                    }
                    break;
                case "travel":
                    if (action.TargetId is string travelTarget)
                    {
                        stateChanged = _gameState.Travel(travelTarget);
                    }
                    break;
                case "resolve_travel_encounter":
                    var choice = action.TargetId ?? "default";
                    stateChanged = _gameState.ResolveTravelEncounter(choice);
                    break;
                case "set_reputation":
                    if (action.TargetId is string factionId && action.Value is int repValue)
                    {
                        _gameState.SetReputation(factionId, repValue);
                        stateChanged = true;
                    }
                    break;
                case "complete_mission":
                    if (action.TargetId is string completeMissionId)
                    {
                        stateChanged = _gameState.CompleteMission(completeMissionId);
                    }
                    break;
                default:
                    await SendError(client, "invalid_action", $"Unknown action type: {action.Type}", recoverable: true, ackSeq: envelope.Seq);
                    return;
            }

            if (stateChanged)
            {
                await BroadcastState(envelope.Seq);
            }
        }
        catch (Exception ex)
        {
            await SendError(client, "internal_error", $"Internal error processing action: {ex.Message}", recoverable: true, ackSeq: envelope.Seq);
        }
    }

    private void GenerateDungeon(string dungeonType)
    {
        var seed = dungeonType.GetHashCode();
        var builder = new DungeonBuilder(seed: seed);

        builder.AddSegment(CreateEntranceRoom());
        builder.AddSegment(CreateCorridor());
        builder.AddSegment(CreateChamber());
        builder.AddSegment(CreateDeadEnd());
        builder.AddSegment(CreateBossRoom());

        var dungeonNames = new Dictionary<string, string>
        {
            ["broken_engine"] = "Broken Engine",
            ["crypt"] = "Crypt of Whispers",
            ["sewers"] = "Sewer Warrens"
        };

        var name = dungeonNames.GetValueOrDefault(dungeonType, dungeonType);
        var dungeon = builder.Build(name, 8, _encounterTables, dungeonType);
        dungeon.WanderingTableId = dungeonType;
        dungeon.EncounterTableId = dungeonType;
        TagBossTile(dungeon);
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
                new() { X = 1, Y = 0, Type = TileType.Floor, North = BorderType.Door, IsExit = true, ExitDirection = Direction.North },
                new() { X = 2, Y = 0, Type = TileType.Floor },
                new() { X = 0, Y = 1, Type = TileType.Floor },
                new() { X = 1, Y = 1, Type = TileType.Floor },
                new() { X = 2, Y = 1, Type = TileType.Floor },
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
                new() { X = 0, Y = 0, Type = TileType.Floor, South = BorderType.Door, IsExit = true, ExitDirection = Direction.South },
                new() { X = 0, Y = -1, Type = TileType.Floor },
                new() { X = 0, Y = -2, Type = TileType.Floor },
                new() { X = 0, Y = -3, Type = TileType.Floor, North = BorderType.Door, IsExit = true, ExitDirection = Direction.North },
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
                new() { X = 0, Y = 1, Type = TileType.Floor, South = BorderType.Door, IsExit = true, ExitDirection = Direction.South },
                new() { X = -1, Y = 0, Type = TileType.Floor },
                new() { X = 0, Y = 0, Type = TileType.Floor },
                new() { X = 1, Y = 0, Type = TileType.Floor },
                new() { X = -1, Y = -1, Type = TileType.Floor },
                new() { X = 0, Y = -1, Type = TileType.Floor },
                new() { X = 1, Y = -1, Type = TileType.Floor },
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
                new() { X = 0, Y = 1, Type = TileType.Floor, South = BorderType.Door, IsExit = true, ExitDirection = Direction.South },
                new() { X = 0, Y = 0, Type = TileType.Floor },
            }
        };
    }

    private static RoomSegment CreateBossRoom()
    {
        return new RoomSegment
        {
            Id = "boss_room",
            Name = "Boss Room",
            Tags = new() { "encounter:boss-encounter-1" },
            Tiles = new()
            {
                new() { X = 0, Y = 0, Type = TileType.Floor, South = BorderType.Door, IsExit = true, ExitDirection = Direction.South },
                new() { X = -1, Y = -1, Type = TileType.Floor },
                new() { X = 0, Y = -1, Type = TileType.Floor },
                new() { X = 1, Y = -1, Type = TileType.Floor },
            }
        };
    }

    private static void TagBossTile(Dungeon dungeon)
    {
        for (int x = 0; x < dungeon.Width; x++)
        {
            for (int y = 0; y < dungeon.Height; y++)
            {
                if (dungeon.Tiles[x, y].Type == TileType.Floor)
                {
                    var entrance = new Position(x, y);
                    var neighbors = new[]
                    {
                        entrance.Move(Direction.South),
                        entrance.Move(Direction.North),
                        entrance.Move(Direction.East),
                        entrance.Move(Direction.West)
                    };
                    foreach (var n in neighbors)
                    {
                        if (dungeon.IsValidPosition(n) && dungeon.Tiles[n.X, n.Y].Type == TileType.Floor)
                        {
                            dungeon.Tiles[n.X, n.Y] = dungeon.Tiles[n.X, n.Y] with { EncounterId = "boss-encounter-1" };
                            return;
                        }
                    }
                }
            }
        }
    }

    private async Task SendState(ClientConnection client)
    {
        var state = CreateStateMessage();
        var envelope = new ProtocolEnvelope
        {
            V = 2,
            Type = "state",
            Seq = client.NextServerSeq(),
            Payload = state
        };
        await SendEnvelope(client.Socket, envelope);
    }

    private async Task BroadcastState(int? ackSeq = null)
    {
        var state = CreateStateMessage();
        List<ClientConnection> clients;
        lock (_clients)
        {
            clients = _clients.ToList();
        }

        foreach (var client in clients)
        {
            try
            {
                if (client.Socket.State == WebSocketState.Open && client.IsReady)
                {
                    var envelope = new ProtocolEnvelope
                    {
                        V = 2,
                        Type = "state",
                        Seq = client.NextServerSeq(),
                        AckSeq = ackSeq,
                        Payload = state
                    };
                    await SendEnvelope(client.Socket, envelope);
                }
            }
            catch { }
        }
    }

    private async Task SendEnvelope(WebSocket socket, ProtocolEnvelope envelope)
    {
        var json = JsonSerializer.Serialize(envelope, _jsonOptions);
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

    private object CreateStateMessage()
    {
        var tiles = new List<object>();
        var explored = new List<object>();
        if (_gameState.CurrentDungeon != null)
        {
            var px = _gameState.Player.Position.X;
            var py = _gameState.Player.Position.Y;
            var sendRadius = 8;

            for (int x = Math.Max(0, px - sendRadius); x < Math.Min(_gameState.CurrentDungeon.Width, px + sendRadius + 1); x++)
            {
                for (int y = Math.Max(0, py - sendRadius); y < Math.Min(_gameState.CurrentDungeon.Height, py + sendRadius + 1); y++)
                {
                    var tile = _gameState.CurrentDungeon.Tiles[x, y];
                    if (tile.Type != TileType.Empty)
                    {
                        tiles.Add(new { x, y, type = tile.Type.ToString(), north = tile.North.ToString(), south = tile.South.ToString(), east = tile.East.ToString(), west = tile.West.ToString() });
                    }
                }
            }

            foreach (var key in _gameState.ExploredTiles)
            {
                var parts = key.Split(',');
                var x = int.Parse(parts[0]);
                var y = int.Parse(parts[1]);
                var tile = _gameState.CurrentDungeon.Tiles[x, y];
                explored.Add(new { x, y, type = tile.Type.ToString(), north = tile.North.ToString(), south = tile.South.ToString(), east = tile.East.ToString(), west = tile.West.ToString() });
            }
        }

        var party = _gameState.Party.Members
            .Where(c => c.Id != Guid.Empty)
            .Select((c, i) =>
            {
                var effective = c.GetEffectiveStats(_itemRegistry);
                var classDef = _classRegistry.Get(c.ClassId);
                return new
                {
                    slot = i,
                    name = c.Name,
                    classId = c.ClassId,
                    className = classDef?.Name ?? c.ClassId,
                    color = ClassColors.GetValueOrDefault(c.ClassId, "#888888"),
                    level = c.Level,
                    xp = c.Xp,
                    hp = c.CurrentHp,
                    maxHp = effective.MaxHp,
                    row = c.Row,
                    alive = c.IsAlive,
                    stats = new
                    {
                        strength = c.BaseStats.Strength,
                        dexterity = c.BaseStats.Dexterity,
                        constitution = c.BaseStats.Constitution,
                        intelligence = c.BaseStats.Intelligence,
                        willpower = c.BaseStats.Willpower,
                        maxHp = effective.MaxHp,
                        speed = effective.Speed,
                        accuracy = effective.Accuracy,
                        evade = effective.Evade,
                        power = effective.Power,
                    },
                    equipment = new
                    {
                        mainHand = c.Equipment.MainHand,
                        offHand = c.Equipment.OffHand,
                        armor = c.Equipment.Armor,
                        accessory1 = c.Equipment.Accessory1,
                        accessory2 = c.Equipment.Accessory2,
                    },
                    knownAbilities = c.KnownAbilities,
                    availableAbilities = classDef?.Abilities
                        .Where(a => a.IsAvailableInRow(c.Row))
                        .Select(a => a.Id)
                        .ToArray() ?? Array.Empty<string>(),
                };
            }).ToArray();

        object? combat = null;
        if (_gameState.Mode == GameMode.Combat && _gameState.Combat != null)
        {
            var c = _gameState.Combat;
            combat = new
            {
                phase = c.Phase.ToString(),
                round = c.Round,
                combatants = c.Combatants.Select(x =>
                {
                    CharacterState? member = x.IsPlayer ? _gameState.Party.Members.FirstOrDefault(m => m.Id == x.Id) : (CharacterState?)null;
                    var classDef = member?.ClassId is not null ? _classRegistry.Get(member.Value.ClassId) : null;
                    return new
                    {
                        id = x.Id,
                        name = x.Name,
                        isPlayer = x.IsPlayer,
                        classId = member?.ClassId,
                        hp = x.Hp,
                        maxHp = x.MaxHp,
                        speed = x.Speed,
                        row = x.Row,
                        alive = x.IsAlive,
                        isCurrent = c.CurrentActor?.Id == x.Id,
                        abilities = classDef?.Abilities
                            .Where(a => member?.KnownAbilities.Contains(a.Id) == true && a.IsAvailableInRow(x.Row))
                            .Select(a => new
                            {
                                id = a.Id,
                                name = a.Name,
                                range = a.Effect.Range,
                                target = a.Effect.Target,
                                requiredRow = a.RequiredRow
                            }).ToArray() ?? Array.Empty<object>()
                    };
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

        var town = new
        {
            currentTownId = _gameState.Town.CurrentTownId,
            availableMissions = _gameState.Town.AvailableMissions.Select(m => new
            {
                id = m.Id,
                title = m.Title,
                description = m.Description,
                minLevel = m.MinLevel,
                rewards = m.Rewards,
                repReward = m.RepReward,
                factionId = m.FactionId
            }).ToArray(),
            vendorStock = _gameState.Town.VendorStock.Select(v => new
            {
                itemId = v.ItemId,
                name = v.Name,
                price = v.Price,
                quantity = v.Quantity
            }).ToArray(),
            factionContacts = _gameState.Town.FactionContacts.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                factionId = c.FactionId,
                portrait = c.Portrait
            }).ToArray(),
            tavernRoster = _gameState.Town.TavernRoster.Select(r => new
            {
                id = r.Id,
                name = r.Name,
                classId = r.ClassId,
                level = r.Level,
                baseStats = new
                {
                    strength = r.BaseStats.Strength,
                    dexterity = r.BaseStats.Dexterity,
                    constitution = r.BaseStats.Constitution,
                    intelligence = r.BaseStats.Intelligence,
                    willpower = r.BaseStats.Willpower
                },
                cost = r.Cost
            }).ToArray(),
            viewedMissions = _gameState.Town.ViewedMissions.ToArray(),
            questLog = _gameState.Town.QuestLog.Select(q => new
            {
                id = q.Id,
                title = q.Title,
                description = q.Description,
                repReward = q.RepReward,
                factionId = q.FactionId,
                status = q.Status
            }).ToArray()
        };

        var overworld = new
        {
            currentNodeId = _gameState.Overworld.CurrentNodeId,
            nodes = _gameState.Overworld.Nodes.Select(n => new { id = n.Id, name = n.Name, type = n.Type }).ToArray(),
            routes = _gameState.Overworld.Routes.Select(r => new { from = r.From, to = r.To, distance = r.Distance, dangerRating = r.DangerRating, terrain = r.Terrain }).ToArray(),
            turns = _gameState.Overworld.Turns
        };

        object? travelEncounter = null;
        if (_gameState.CurrentTravelEncounter != null)
        {
            var te = _gameState.CurrentTravelEncounter;
            travelEncounter = new
            {
                id = te.Id,
                name = te.Name,
                resolutionType = te.ResolutionType,
                statName = te.StatName,
                factionId = te.FactionId,
                reputationValue = te.ReputationValue,
                hasSurpriseRound = te.HasSurpriseRound,
                priceTier = te.PriceTier,
                options = te.Options
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
            combatResult,
            town,
            overworld,
            travelEncounter,
            reputation = _gameState.Reputation.ToDictionary(r => r.Key, r => r.Value),
            campaignEnded = _gameState.CampaignEnded
        };

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

public class PlayerAction
{
    public string Type { get; set; } = "";
    public CombatAction? Action { get; set; }
    public string? DungeonType { get; set; }
    public int? Slot { get; set; }
    public string? TargetId { get; set; }
    public int? Value { get; set; }
}

public class ProtocolEnvelope
{
    public int V { get; set; }
    public string Type { get; set; } = "";
    public int Seq { get; set; }
    public int? AckSeq { get; set; }
    public object? Payload { get; set; }
}

public class HelloPayload
{
    public int ProtocolVersion { get; set; }
    public string SessionId { get; set; } = "";
}

public class ErrorPayload
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public bool Recoverable { get; set; }
}

public class HeartbeatPingPayload
{
    public int PingSeq { get; set; }
}

public class ClientConnection : IDisposable
{
    public WebSocket Socket { get; }
    public string SessionId { get; }
    public bool IsReady { get; set; }
    private int _serverSeq = -1;
    private int _pingSeq = -1;
    public int LastPingSeq { get; set; } = -1;
    public DateTime LastPingTime { get; set; } = DateTime.MinValue;
    public int LastPongSeq { get; set; } = -1;

    public ClientConnection(WebSocket socket)
    {
        Socket = socket;
        SessionId = Guid.NewGuid().ToString("N");
    }

    public int NextServerSeq() => Interlocked.Increment(ref _serverSeq);
    public int NextPingSeq() => Interlocked.Increment(ref _pingSeq);

    public void Dispose()
    {
        Socket.Dispose();
    }
}
