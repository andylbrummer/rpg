using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Protocol;

namespace RPC.Host.Web;

public class StateBroadcaster
{
    private readonly ClientRegistry _registry;
    private readonly StatePresenter _presenter;
    private readonly GameState _gameState;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly CancellationTokenSource _cts;

    public StateBroadcaster(ClientRegistry registry, StatePresenter presenter, GameState gameState, JsonSerializerOptions jsonOptions, CancellationTokenSource cts)
    {
        _registry = registry;
        _presenter = presenter;
        _gameState = gameState;
        _jsonOptions = jsonOptions;
        _cts = cts;
    }

    public async Task SendState(ClientConnection client, int? ackSeq = null, object? payload = null)
    {
        var state = payload ?? _presenter.CreateStateMessage(_gameState);
        var envelope = new ProtocolEnvelope
        {
            V = 2,
            Type = "state",
            Seq = client.NextServerSeq(),
            AckSeq = ackSeq,
            Payload = state
        };
        await SendEnvelope(client, envelope);
    }

    public async Task BroadcastState(ClientConnection? excludeClient = null, object? payload = null)
    {
        var state = payload ?? _presenter.CreateStateMessage(_gameState);
        var clients = _registry.Snapshot();

        foreach (var client in clients)
        {
            if (client == excludeClient) continue;
            try
            {
                if (client.Socket.State == WebSocketState.Open && client.IsReady)
                {
                    var envelope = new ProtocolEnvelope
                    {
                        V = 2,
                        Type = "state",
                        Seq = client.NextServerSeq(),
                        Payload = state
                    };
                    await SendEnvelope(client, envelope);
                }
            }
            catch { }
        }
    }

    public async Task BroadcastContentReload()
    {
        var clients = _registry.Snapshot();

        foreach (var client in clients)
        {
            try
            {
                if (client.Socket.State == WebSocketState.Open)
                {
                    var envelope = new ProtocolEnvelope
                    {
                        V = 2,
                        Type = "content.reload",
                        Seq = client.NextServerSeq(),
                        Payload = new { category = "segments" }
                    };
                    await SendEnvelope(client, envelope);
                }
            }
            catch { }
        }
    }

    public async Task SendEnvelope(ClientConnection client, ProtocolEnvelope envelope)
    {
        var json = JsonSerializer.Serialize(envelope, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        await client.SendLock.WaitAsync(_cts.Token);
        try
        {
            if (client.Socket.State == WebSocketState.Open)
            {
                await client.Socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cts.Token);
            }
        }
        finally
        {
            client.SendLock.Release();
        }
    }
}
