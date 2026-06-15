using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using RPC.Engine;

namespace RPC.Host.Web;

/// <summary>
/// Routes inbound HttpListener requests: WebSocket upgrades go to the
/// <see cref="WebSocketConnectionHandler"/>, static client files are served from the build
/// output, and the debug JSON endpoints (status, dungeon, action-log) are answered here.
/// Extracted from <see cref="GameServer"/> as the HTTP transport seam.
/// </summary>
internal sealed class HttpRequestRouter
{
    private readonly int _port;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly GameState _gameState;
    private readonly SemaphoreSlim _gameStateLock;
    private readonly CancellationTokenSource _cts;
    private readonly WebSocketConnectionHandler _webSocketHandler;

    public HttpRequestRouter(
        int port,
        JsonSerializerOptions jsonOptions,
        GameState gameState,
        SemaphoreSlim gameStateLock,
        CancellationTokenSource cts,
        WebSocketConnectionHandler webSocketHandler)
    {
        _port = port;
        _jsonOptions = jsonOptions;
        _gameState = gameState;
        _gameStateLock = gameStateLock;
        _cts = cts;
        _webSocketHandler = webSocketHandler;
    }

    public async Task HandleRequest(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";

        if (context.Request.IsWebSocketRequest)
        {
            await _webSocketHandler.Handle(context);
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

        // Append the separator so a sibling like "dist-evil" can't prefix-match "dist".
        var fullClientDir = Path.GetFullPath(clientDir);
        var clientDirPrefix = fullClientDir.EndsWith(Path.DirectorySeparatorChar)
            ? fullClientDir
            : fullClientDir + Path.DirectorySeparatorChar;
        var fullFilePath = Path.GetFullPath(filePath);
        if (!fullFilePath.StartsWith(clientDirPrefix, StringComparison.Ordinal))
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
                $"<script>window.SERVER_PORT = {_port};</script></head>"
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
        object response;
        // ActionLog is a List<> appended under the game-state lock during command execution;
        // enumerate it under the same lock to avoid a "Collection was modified" throw.
        await _gameStateLock.WaitAsync(_cts.Token);
        try
        {
            response = new
            {
                events = _gameState.ActionLog.Select(e => new
                {
                    turn = e.Turn,
                    category = e.Category,
                    type = e.Type,
                    payload = e.Payload
                }).ToArray()
            };
        }
        finally
        {
            _gameStateLock.Release();
        }
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }
}
