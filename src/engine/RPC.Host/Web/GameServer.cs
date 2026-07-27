using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using RPC.Content;
using RPC.Engine;
using RPC.Engine.Analytics;
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
    private Task? _acceptLoop;
    private int _stopped;

    /// <summary>
    /// How often this server pings a ready client. Defaults to the production interval; tests that
    /// are about heartbeat behaviour shorten it so they do not have to wait out real seconds.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; }

    public GameServer(int port = 8080, bool isDev = false, bool loadSave = true, TimeSpan? heartbeatInterval = null)
    {
        _listener = new HttpListener();
        Port = port;
        HeartbeatInterval = heartbeatInterval ?? WebSocketConnectionHandler.DefaultPingInterval;
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");

        var content = ContentBootstrap.Load();
        _catalog = content.Catalog;
        _segments = content.Segments;
        _dungeonContent = content.DungeonContent;

        var factionRepo = new FactionContentRepository(content.FactionContent);
        var rumorRepo = new RumorRepository(_catalog);
        _gameState = new GameState(encounterTables: content.EncounterTables, classRegistry: content.ClassRegistry, synergies: content.Synergies, factionContent: factionRepo, rumors: rumorRepo, dungeonTemplates: content.DungeonTemplates, campaignContent: content.CampaignContent, secrets: content.Secrets, archives: content.Archives, enemies: content.Enemies, items: content.ItemRegistry);
        _gameState.ContentHash = content.ContentHash;
        // Real game sessions persist cross-campaign meta-progression to disk; campaign start loads
        // and biases the run, campaign end folds the result back and saves it.
        _gameState.MetaPersistenceEnabled = true;
        // Likewise for anonymized aggregates: only a real session records them to the per-user
        // file. The engine's default tracker is in-memory, which keeps parallel headless tests off
        // this shared path.
        _gameState.Analytics = new AnalyticsTracker(AnalyticsTracker.DefaultPath);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        var dungeonGenerator = new DungeonGenerator(_segments, content.DungeonTemplates, content.EncounterTables, content.LootTables);
        var commandHandler = new GameCommandHandler(_gameState, dungeonGenerator, content.ItemRegistry);
        var statePresenter = new StatePresenter(content.ClassRegistry, content.ItemRegistry);
        _broadcaster = new StateBroadcaster(_registry, jsonOptions, _cts);

        var protocolHandler = new ProtocolMessageHandler(_broadcaster, jsonOptions, _gameState, _gameStateLock, commandHandler, statePresenter, _cts);
        var webSocketHandler = new WebSocketConnectionHandler(_registry, _broadcaster, protocolHandler, _cts, HeartbeatInterval);
        _router = new HttpRequestRouter(jsonOptions, _gameState, _gameStateLock, _cts, webSocketHandler);

        if (loadSave)
        {
            // Say so when an existing save did not load. LoadGame also returns false when there
            // simply is no save yet, which is the ordinary first-run case and not worth reporting —
            // but a save that exists and fails to load has just been quarantined, and the player is
            // about to start a fresh campaign without being told why.
            var hadSave = RPC.Engine.Save.SaveSystem.HasSave();
            if (!_gameState.LoadGame(dungeonGenerator: dungeonGenerator) && hadSave)
            {
                Console.Error.WriteLine(
                    "[Save] An existing save could not be loaded; starting a new campaign. " +
                    "The unreadable file has been set aside — see the messages above.");
            }
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
        _acceptLoop = Task.Run(AcceptLoop);
    }

    /// <summary>
    /// Accepts requests until shutdown. Stop() disposes the listener out from under
    /// GetContextAsync, so the terminal exceptions are expected control flow rather than faults;
    /// any other accept failure is reported and the loop ends rather than leaving the task
    /// faulted and unobserved.
    /// </summary>
    private async Task AcceptLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (InvalidOperationException) { break; }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Server] Accept loop stopped: {ex.Message}");
                break;
            }

            _ = Task.Run(() => DispatchRequest(context));
        }
    }

    /// <summary>
    /// Runs one request to completion. A client that disconnects mid-response makes the write
    /// throw; containing it here keeps a dropped connection from surfacing as an unobserved
    /// task exception and taking the process down under ThrowUnobservedTaskExceptions.
    /// </summary>
    private async Task DispatchRequest(HttpListenerContext context)
    {
        try
        {
            await _router.HandleRequest(context);
        }
        catch (HttpListenerException) { }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Server] Request {context.Request.Url?.AbsolutePath} failed: {ex}");
            TryFailRequest(context);
        }
    }

    private static void TryFailRequest(HttpListenerContext context)
    {
        try
        {
            context.Response.StatusCode = 500;
            context.Response.Close();
        }
        catch (Exception) { /* response already committed or the peer is gone */ }
    }

    /// <summary>
    /// Shuts the server down and releases everything it owns. Repeated Start/Stop cycles happen
    /// once per integration test, so leaking watchers or listener handles here compounds across
    /// a suite run.
    /// </summary>
    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 1) return;

        // Analytics accumulate in memory between campaign milestones, so a shutdown mid-campaign
        // would otherwise drop everything discovered since the run began.
        _gameState.Analytics.Flush();

        _cts.Cancel();

        foreach (var watcher in _segmentWatchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _segmentWatchers.Clear();

        _listener.Stop();
        _listener.Close();

        try
        {
            _acceptLoop?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException) { /* accept loop already ended on the disposed listener */ }
        _acceptLoop = null;
        // _cts is deliberately not disposed: in-flight WebSocket and broadcast tasks still read
        // its Token as they unwind, and disposing it would turn an orderly shutdown into a
        // shower of ObjectDisposedExceptions on those threads.
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
            var reloaded = ContentBootstrap.LoadSegments(_catalog, _dungeonContent.SegmentDirectories);
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
