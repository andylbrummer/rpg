using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Commands;
using RPC.Engine.Protocol;

namespace RPC.Host.Web.Protocol;

/// <summary>
/// Parses and validates inbound protocol envelopes, dispatches actions into the command
/// handler under the shared game-state lock, and sends state/error responses. This is the
/// protocol seam extracted from <see cref="GameServer"/>; the game-state lock is injected by
/// reference so command execution stays serialized exactly as before.
/// </summary>
internal sealed class ProtocolMessageHandler
{
    private readonly StateBroadcaster _broadcaster;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly GameState _gameState;
    private readonly SemaphoreSlim _gameStateLock;
    private readonly GameCommandHandler _commandHandler;
    private readonly StatePresenter _statePresenter;
    private readonly CancellationTokenSource _cts;

    public ProtocolMessageHandler(
        StateBroadcaster broadcaster,
        JsonSerializerOptions jsonOptions,
        GameState gameState,
        SemaphoreSlim gameStateLock,
        GameCommandHandler commandHandler,
        StatePresenter statePresenter,
        CancellationTokenSource cts)
    {
        _broadcaster = broadcaster;
        _jsonOptions = jsonOptions;
        _gameState = gameState;
        _gameStateLock = gameStateLock;
        _commandHandler = commandHandler;
        _statePresenter = statePresenter;
        _cts = cts;
    }

    public async Task HandleMessage(ClientConnection client, string message)
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
            // Build the snapshot under the state lock: CreateStateMessage reads mutable game-state
            // collections, so a ready/reconnect arriving mid-action would otherwise observe a torn
            // snapshot or throw on concurrent enumeration. Publish the immutable snapshot outside.
            object snapshot;
            await _gameStateLock.WaitAsync(_cts.Token);
            try
            {
                snapshot = _statePresenter.CreateStateMessage(_gameState);
            }
            finally
            {
                _gameStateLock.Release();
            }
            await _broadcaster.SendState(client, payload: snapshot);
            return;
        }

        if (envelope.Type == "heartbeat.pong")
        {
            // TryGetInt32 rather than GetInt32: a pong carrying a non-numeric pingSeq is a client
            // bug, and throwing here would unwind the receive loop and drop an otherwise healthy
            // connection. Ignoring the malformed pong lets the heartbeat time it out normally.
            if (envelope.Payload is JsonElement json
                && json.ValueKind == JsonValueKind.Object
                && json.TryGetProperty("pingSeq", out var pingSeqEl)
                && pingSeqEl.ValueKind == JsonValueKind.Number
                && pingSeqEl.TryGetInt32(out var pingSeq))
            {
                client.LastPongSeq = pingSeq;
            }
            return;
        }

        if (envelope.Type == "analytics.request")
        {
            // Snapshot under the game-state lock: GetData() and the .ToArray() reads below
            // race against command-thread mutations (RecordSynergyDiscovered etc.) otherwise.
            object response;
            await _gameStateLock.WaitAsync(_cts.Token);
            try
            {
                var data = _gameState.Analytics.GetData();
                response = new
                {
                    campaignsStarted = data.CampaignsStarted,
                    campaignsCompleted = data.CampaignsCompleted,
                    mastermindsExposed = data.MastermindsExposed,
                    schemesStopped = data.SchemesStopped,
                    betrayals = data.Betrayals,
                    totalTurns = data.TotalTurns,
                    totalDeaths = data.TotalDeaths,
                    synergiesDiscovered = data.SynergiesDiscovered.ToArray(),
                    classesPlayed = data.ClassesPlayed.ToArray(),
                    branchesChosen = data.BranchesChosen.ToArray(),
                    optionalDungeonsUnlocked = data.OptionalDungeonsUnlocked.ToArray(),
                    factionEndStates = data.FactionEndStates
                };
            }
            finally
            {
                _gameStateLock.Release();
            }
            // Send via the broadcaster so the per-client SendLock serializes against
            // concurrent heartbeat/broadcast sends on the same socket.
            await _broadcaster.SendEnvelope(client, new ProtocolEnvelope
            {
                V = 2,
                Type = "analytics.data",
                Seq = client.NextServerSeq(),
                Payload = response
            });
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

            ICommand cmd;
            try
            {
                cmd = CommandDispatcher.Parse(action);
            }
            catch (ArgumentException ex)
            {
                await SendError(client, "invalid_action", ex.Message, recoverable: true, ackSeq: envelope.Seq);
                return;
            }

            object? snapshot = null;
            bool stateChanged = false;
            bool clearCombatResult = false;
            await _gameStateLock.WaitAsync(_cts.Token);
            try
            {
                var result = _commandHandler.Execute(cmd);
                stateChanged = result.StateChanged;
                clearCombatResult = result.ClearCombatResult;

                if (stateChanged)
                {
                    snapshot = _statePresenter.CreateStateMessage(_gameState);
                }

                if (clearCombatResult)
                {
                    _gameState.ClearCombatResult();
                }
            }
            finally
            {
                _gameStateLock.Release();
            }

            if (stateChanged && snapshot != null)
            {
                await _broadcaster.SendState(client, envelope.Seq, snapshot);
                await _broadcaster.BroadcastState(excludeClient: client, payload: snapshot);
            }
        }
        catch (OperationCanceledException)
        {
            // Server shutdown, not a command failure — let the receive loop unwind.
            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Protocol] Action failed: {ex}");
            await SendError(client, "internal_error", $"Internal error processing action: {ex.Message}", recoverable: true, ackSeq: envelope.Seq);
        }
    }

    private async Task SendError(ClientConnection client, string code, string message, bool recoverable, int? ackSeq = null)
    {
        await _broadcaster.SendEnvelope(client, new ProtocolEnvelope
        {
            V = 2,
            Type = "error",
            Seq = client.NextServerSeq(),
            AckSeq = ackSeq,
            Payload = new ErrorPayload { Code = code, Message = message, Recoverable = recoverable }
        });
    }
}
