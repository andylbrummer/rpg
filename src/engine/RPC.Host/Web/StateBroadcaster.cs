using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Protocol;

namespace RPC.Host.Web;

/// <summary>
/// Serializes protocol envelopes onto client sockets, one client at a time or fanned out to all
/// of them. Deliberately knows nothing about the game state: snapshots are built by callers under
/// the game-state lock and arrive here already immutable, which is what lets a send proceed
/// without holding that lock.
/// </summary>
public class StateBroadcaster
{
    private readonly ClientRegistry _registry;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly CancellationTokenSource _cts;

    public StateBroadcaster(ClientRegistry registry, JsonSerializerOptions jsonOptions, CancellationTokenSource cts)
    {
        _registry = registry;
        _jsonOptions = jsonOptions;
        _cts = cts;
    }

    /// <summary>
    /// Sends a state snapshot to one client.
    /// <para>
    /// The snapshot is a required argument rather than something this class can build on demand.
    /// Building one reads the game state's mutable collections, so it is only correct under the
    /// game-state lock, and this class does not hold that lock — it is on the send path, where
    /// taking it would serialize every socket write behind gameplay. Callers already build their
    /// snapshot inside the lock and publish the immutable result out here; making that the only
    /// option means a future caller cannot silently reintroduce a torn or concurrently-enumerated
    /// snapshot by leaving the argument off.
    /// </para>
    /// </summary>
    public async Task SendState(ClientConnection client, object payload, int? ackSeq = null)
    {
        var envelope = new ProtocolEnvelope
        {
            V = 2,
            Type = "state",
            Seq = client.NextServerSeq(),
            AckSeq = ackSeq,
            Payload = payload
        };
        await SendEnvelope(client, envelope);
    }

    /// <summary>
    /// Fans a state snapshot out to every ready client except <paramref name="excludeClient"/>.
    /// The snapshot is required for the reason given on <see cref="SendState"/>.
    /// </summary>
    public Task BroadcastState(object payload, ClientConnection? excludeClient = null)
    {
        return BroadcastEach(
            "state",
            client => client != excludeClient && client.IsReady,
            client => new ProtocolEnvelope
            {
                V = 2,
                Type = "state",
                Seq = client.NextServerSeq(),
                Payload = payload
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
    /// Bounded by the connection's <see cref="ClientConnection.SendTimeout"/> and by the connection's own lifetime
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

        try
        {
            // Queueing behind this socket's other senders is deliberately not on the clock: under a
            // broadcast burst a healthy client can have several sends outstanding, and charging a
            // send for the time it spent waiting its turn would retire clients for being popular.
            // The lock wait is bounded instead by the connection's lifetime — whoever holds it is
            // itself on the clock below, so the queue always drains or the connection dies.
            await client.SendLock.WaitAsync(client.Token);
            try
            {
                if (client.Socket.State == WebSocketState.Open)
                {
                    using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(client.Token, _cts.Token);
                    writeCts.CancelAfter(client.SendTimeout);
                    await client.Socket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        true,
                        writeCts.Token);
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
