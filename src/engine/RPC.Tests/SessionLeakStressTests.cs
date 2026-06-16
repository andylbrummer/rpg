using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RPC.Host.Web;

namespace RPC.Tests;

/// <summary>
/// Leak-hunt regression guard for the reused local RPC.Host.
///
/// The local Playwright suite reuses one long-lived dotnet host (reuseExistingServer when
/// !CI). The failure report was per-test time ballooning + intermittent failures after many
/// back-to-back full runs. These tests pin the two server-side growth signals so a genuine
/// leak cannot silently regress:
///
///  1. <see cref="ClientCount_Returns_To_Baseline_After_Many_Connect_Disconnect_Cycles"/> —
///     opens then closes N WebSocket sessions and asserts the server-side live-client counter
///     returns to its baseline. A counter that never returns to baseline = a connection /
///     session registry leak.
///
/// The companion snapshot-payload growth signal (the unbounded ActionLog serialized into
/// every state frame) is pinned deterministically by
/// <c>StatePresenterTests.CreateStateMessage_ActionLog_Is_Capped_To_Recent_Tail</c>.
/// </summary>
public class SessionLeakStressTests : IDisposable
{
    private readonly GameServer _server;
    private readonly CancellationTokenSource _cts = new();

    public SessionLeakStressTests()
    {
        var port = GetFreePort();
        _server = new GameServer(port: port, loadSave: false);
        _server.Start();
    }

    public void Dispose()
    {
        _server.Stop();
        _cts.Cancel();
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task<ClientWebSocket> ConnectAsync()
    {
        var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://localhost:{_server.Port}/"), _cts.Token);
        return ws;
    }

    private static async Task<JsonElement> ReceiveAsync(ClientWebSocket ws)
    {
        var buffer = new byte[16384];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(ms.ToArray()));
    }

    private async Task WaitForClientCountAsync(int expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_server.ClientCount == expected) return;
            await Task.Delay(20);
        }
        Assert.Fail($"ClientCount did not reach {expected} within {timeout.TotalSeconds:0.#}s; last observed {_server.ClientCount}");
    }

    [Fact]
    public async Task ClientCount_Returns_To_Baseline_After_Many_Connect_Disconnect_Cycles()
    {
        // Baseline before any connection.
        Assert.Equal(0, _server.ClientCount);

        const int cycles = 200;
        for (int i = 0; i < cycles; i++)
        {
            var ws = await ConnectAsync();
            await ReceiveAsync(ws); // hello (proves the connection registered)
            // Abort rather than a graceful CloseAsync handshake: it models a browser page-close
            // (abrupt socket drop, the dominant disconnect in the e2e suite) and the server tears
            // the connection down via the receive loop's WebSocketException -> finally cleanup.
            ws.Abort();
            ws.Dispose();
        }

        // The disconnect cleanup (ClientRegistry.Remove in the handler's finally) runs
        // asynchronously after the close frame; allow it to drain, then assert we are back
        // at baseline. If connections leaked, the counter would sit at ~cycles, not 0.
        await WaitForClientCountAsync(0, TimeSpan.FromSeconds(30));
        Assert.Equal(0, _server.ClientCount);
    }
}
