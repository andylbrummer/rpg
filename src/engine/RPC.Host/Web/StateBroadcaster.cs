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

    public Task BroadcastState(ClientConnection? excludeClient = null, object? payload = null)
    {
        var state = payload ?? _presenter.CreateStateMessage(_gameState);

        return BroadcastEach(
            "state",
            client => client != excludeClient && client.IsReady,
            client => new ProtocolEnvelope
            {
                V = 2,
                Type = "state",
                Seq = client.NextServerSeq(),
                Payload = state
            });
    }

    public Task BroadcastContentReload(IReadOnlyList<(string TemplateId, string Name)>? affectedDungeons = null)
    {
        var dungeons = (affectedDungeons ?? Array.Empty<(string, string)>())
            .Select(d => new { templateId = d.TemplateId, name = d.Name })
            .ToArray();

        return BroadcastEach(
            "content.reload",
            _ => true,
            client => new ProtocolEnvelope
            {
                V = 2,
                Type = "content.reload",
                Seq = client.NextServerSeq(),
                Payload = new { category = "segments", dungeons }
            });
    }

    /// <summary>
    /// Fans one envelope out to every open, eligible client concurrently. Sends run in parallel
    /// because each socket has its own send lock: awaiting them in sequence would let a single
    /// backpressured client hold up delivery to everyone else. One client's failure is reported
    /// and skipped rather than aborting the fan-out.
    /// </summary>
    private Task BroadcastEach(
        string label,
        Func<ClientConnection, bool> eligible,
        Func<ClientConnection, ProtocolEnvelope> buildEnvelope)
    {
        var sends = new List<Task>();

        foreach (var client in _registry.Snapshot())
        {
            if (client.Socket.State != WebSocketState.Open || !eligible(client)) continue;
            sends.Add(SendGuarded(client, buildEnvelope(client), label));
        }

        return sends.Count == 0 ? Task.CompletedTask : Task.WhenAll(sends);
    }

    private async Task SendGuarded(ClientConnection client, ProtocolEnvelope envelope, string label)
    {
        try
        {
            await SendEnvelope(client, envelope);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Broadcast] {label} send to a client failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends one envelope on a client's socket, serialized against that socket's other senders.
    /// <para>
    /// Bounded by <see cref="ClientConnection.SendTimeout"/> and by the connection's own lifetime
    /// rather than only by server shutdown. A peer that stops draining its socket leaves
    /// SendAsync parked with the socket still Open, which would otherwise hold the send lock and
    /// wedge the heartbeat for the life of the process — and, because a broadcast is awaited by
    /// the acting client before its next action, stall every other player behind one dead peer.
    /// An expired send declares the connection finished so its receive loop unwinds and
    /// deregisters it.
    /// </para>
    /// </summary>
    public async Task SendEnvelope(ClientConnection client, ProtocolEnvelope envelope)
    {
        var json = JsonSerializer.Serialize(envelope, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(client.Token, _cts.Token);
        sendCts.CancelAfter(ClientConnection.SendTimeout);

        try
        {
            await client.SendLock.WaitAsync(sendCts.Token);
            try
            {
                if (client.Socket.State == WebSocketState.Open)
                {
                    await client.Socket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        true,
                        sendCts.Token);
                }
            }
            finally
            {
                client.SendLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Server shutdown cancels every send at once and is not this client's fault; anything
            // else means this peer stopped draining, so retire the connection.
            if (!_cts.IsCancellationRequested) client.Abort();
            throw;
        }
    }
}
