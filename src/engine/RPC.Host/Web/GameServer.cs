using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using RPC.Content;
using RPC.Engine;
using RPC.Engine.Commands;
using RPC.Engine.Content;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Town;
using RPC.Host.Web.Protocol;

namespace RPC.Host.Web;

/// <summary>
/// Composition root for the host's web layer. Loads content, wires the game state and the
/// transport/protocol collaborators (<see cref="HttpRequestRouter"/>,
/// <see cref="WebSocketConnectionHandler"/>, <see cref="ProtocolMessageHandler"/>,
/// <see cref="StateBroadcaster"/>), and owns the HttpListener accept loop plus the optional
/// segment hot-reload watcher. Behaviour lives in the collaborators; this class only builds
/// and starts/stops them.
/// </summary>
public class GameServer
{
    private readonly HttpListener _listener;
    private readonly ClientRegistry _registry = new();
    private readonly GameState _gameState;
    private readonly CancellationTokenSource _cts = new();
    private readonly StateBroadcaster _broadcaster;

    private readonly IContentCatalog _catalog;
    private readonly List<RoomSegment> _segments;
    private readonly FileSystemWatcher? _segmentWatcher;
    private readonly SemaphoreSlim _gameStateLock = new(1, 1);
    private readonly HttpRequestRouter _router;

    public GameServer(int port = 8080, bool isDev = false, bool loadSave = true)
    {
        _listener = new HttpListener();
        Port = port;
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");

        var content = ContentBootstrap.Load();
        _catalog = content.Catalog;
        _segments = content.Segments;

        var factionRepo = new FactionContentRepository(content.FactionContent);
        var rumorRepo = new RumorRepository(_catalog);
        _gameState = new GameState(encounterTables: content.EncounterTables, classRegistry: content.ClassRegistry, synergies: content.Synergies, factionContent: factionRepo, rumors: rumorRepo, dungeonTemplates: content.DungeonTemplates);
        _gameState.ContentHash = content.ContentHash;
        // Real game sessions persist cross-campaign meta-progression to disk; campaign start loads
        // and biases the run, campaign end folds the result back and saves it.
        _gameState.MetaPersistenceEnabled = true;

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        var dungeonGenerator = new DungeonGenerator(_segments, content.DungeonTemplates, content.EncounterTables, content.LootTables);
        var commandHandler = new GameCommandHandler(_gameState, dungeonGenerator);
        var statePresenter = new StatePresenter(content.ClassRegistry, content.ItemRegistry);
        _broadcaster = new StateBroadcaster(_registry, statePresenter, _gameState, jsonOptions, _cts);

        var protocolHandler = new ProtocolMessageHandler(_broadcaster, jsonOptions, _gameState, _gameStateLock, commandHandler, statePresenter, _cts);
        var webSocketHandler = new WebSocketConnectionHandler(_registry, _broadcaster, protocolHandler, _cts);
        _router = new HttpRequestRouter(Port, jsonOptions, _gameState, _gameStateLock, _cts, webSocketHandler);

        if (loadSave)
        {
            _gameState.LoadGame(dungeonGenerator: dungeonGenerator);
        }
        if (isDev)
        {
            _segmentWatcher = StartSegmentWatcher();
        }
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
                _ = Task.Run(() => _router.HandleRequest(context));
            }
        });
    }

    public void Stop()
    {
        _cts.Cancel();
        _listener.Stop();
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
            // Load outside the lock (file I/O), then swap under the game-state lock so the
            // mutation can't tear a concurrent DungeonGenerator read (generation runs under
            // the same lock via the command handler).
            var reloaded = ContentBootstrap.LoadSegments(_catalog);
            _gameStateLock.Wait(_cts.Token);
            try
            {
                _segments.Clear();
                _segments.AddRange(reloaded);
            }
            finally
            {
                _gameStateLock.Release();
            }
            _ = _broadcaster.BroadcastContentReload();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"[Content] Segment hot-reload failed: {ex.Message}");
        }
    }
}
