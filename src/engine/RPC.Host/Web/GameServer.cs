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
    private readonly DungeonContentSet _dungeonContent;
    private readonly List<FileSystemWatcher> _segmentWatchers = new();
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
        _dungeonContent = content.DungeonContent;

        var factionRepo = new FactionContentRepository(content.FactionContent);
        var rumorRepo = new RumorRepository(_catalog);
        var dialogueRepo = new DialogueRepository(_catalog);
        _gameState = new GameState(encounterTables: content.EncounterTables, classRegistry: content.ClassRegistry, synergies: content.Synergies, factionContent: factionRepo, rumors: rumorRepo, dungeonTemplates: content.DungeonTemplates, campaignContent: content.CampaignContent, dialogue: dialogueRepo);
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
        var commandHandler = new GameCommandHandler(_gameState, dungeonGenerator, content.ItemRegistry);
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
            StartSegmentWatchers();
        }
    }

    public int Port { get; private set; }

    /// <summary>
    /// Number of live WebSocket client connections currently registered. Exposed as a
    /// server-side leak signal: after every client disconnects this MUST return to its
    /// baseline (0). A monotonically growing value across connect/disconnect cycles is a
    /// connection/session leak. Asserted by the session-leak stress test.
    /// </summary>
    public int ClientCount => _registry.Count;

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

    /// <summary>
    /// Watch every content-defined segment directory the dungeon templates declare (not a single
    /// hard-coded broken-engine path). Each watcher reports the directory that changed so the reload
    /// can name the affected dungeon templates.
    /// </summary>
    private void StartSegmentWatchers()
    {
        if (_catalog is not FileSystemCatalog fs) return;

        foreach (var relativeDir in _dungeonContent.SegmentDirectories)
        {
            // relativeDir is content-relative (e.g. "segments/ossuary"); resolve under the catalog base.
            var dir = Path.Combine(fs.BaseDirectory, relativeDir.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(dir)) continue;

            var watcher = new FileSystemWatcher(dir, "*.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };
            watcher.Changed += (_, _) => ReloadSegments(relativeDir);
            watcher.Created += (_, _) => ReloadSegments(relativeDir);
            watcher.Deleted += (_, _) => ReloadSegments(relativeDir);
            watcher.EnableRaisingEvents = true;
            _segmentWatchers.Add(watcher);
        }
    }

    private void ReloadSegments(string changedDirectory)
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
            var affected = _dungeonContent.TemplatesForDirectory(changedDirectory)
                .Select(t => (t.Id, t.Name))
                .ToList();
            _ = _broadcaster.BroadcastContentReload(affected);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"[Content] Segment hot-reload failed: {ex.Message}");
        }
    }
}
