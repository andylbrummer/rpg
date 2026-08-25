namespace RPC.Engine.Combat;

/// <summary>
/// Builds the sink CombatEngine reports combat events to. The engine emits through a delegate so
/// it stays free of game-state knowledge; this is the one place that decides what those events
/// mean to a run — stamping the encounter, recording a synergy as discovered, and appending to the
/// action log.
/// <para>
/// It exists as a shared factory because the four places that drive combat used to differ: the
/// player-action path built this closure inline, while the three that open combat passed null and
/// so reported nothing at all. Nothing observable was lost — no event the engine emits can occur
/// before a player's first turn — but the asymmetry meant any event added to the engine later
/// would be silently dropped on three of its four callers.
/// </para>
/// </summary>
internal static class CombatActionLog
{
    public static Action<string, string, Dictionary<string, string>> EmitterFor(GameState state)
        => (category, type, payload) =>
        {
            if (type == "synergy_triggered")
            {
                if (state.CurrentEncounterId != null)
                    payload["encounterId"] = state.CurrentEncounterId;

                if (payload.TryGetValue("synergyId", out var synergyId) && !string.IsNullOrEmpty(synergyId))
                {
                    state.Journal.Discover(synergyId);
                    state.Analytics.RecordSynergyDiscovered(synergyId);
                }
            }

            state.EmitActionLog(category, type, payload);
        };
}
