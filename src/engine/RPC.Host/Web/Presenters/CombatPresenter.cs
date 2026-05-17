using RPC.Engine;
using RPC.Engine.Character;

namespace RPC.Host.Web.Presenters;

public class CombatPresenter
{
    private readonly ClassRegistry _classRegistry;

    public CombatPresenter(ClassRegistry classRegistry)
    {
        _classRegistry = classRegistry;
    }

    public object? PresentCombat(GameState state)
    {
        if (state.Mode != GameMode.Combat || state.Combat == null)
            return null;

        var c = state.Combat;
        return new
        {
            phase = c.Phase.ToString(),
            round = c.Round,
            combatants = c.Combatants.Select(x =>
            {
                var member = x.IsPlayer ? state.Party.Members.FirstOrDefault(m => m.Id == x.Id) : (CharacterState?)null;
                var classDef = member?.ClassId is not null ? _classRegistry.Get(member.Value.ClassId) : null;
                return new
                {
                    id = x.Id,
                    name = x.Name,
                    isPlayer = x.IsPlayer,
                    classId = member?.ClassId,
                    hp = x.Hp,
                    maxHp = x.MaxHp,
                    speed = x.Speed,
                    row = x.Row,
                    alive = x.IsAlive,
                    isCurrent = c.CurrentActor?.Id == x.Id,
                    abilities = classDef?.Abilities
                        .Where(a => member?.KnownAbilities.Contains(a.Id) == true && a.IsAvailableInRow(x.Row))
                        .Select(a => new
                        {
                            id = a.Id,
                            name = a.Name,
                            range = a.Effect.Range,
                            target = a.Effect.Target,
                            requiredRow = a.RequiredRow
                        }).ToArray() ?? Array.Empty<object>(),
                    tempModifiers = x.TempModifiers.Select(m => new { stat = m.Stat, delta = m.Delta, duration = m.Duration, source = m.Source }).ToArray(),
                };
            }).ToArray(),
            initiativeOrder = c.InitiativeOrder,
            currentTurnIndex = c.CurrentTurnIndex,
            log = c.Log.Select(l => new { actor = l.ActorId, message = l.Message, round = l.Round }).ToArray(),
            isFinished = c.IsFinished
        };
    }

    public object? PresentCombatResult(GameState state)
    {
        if (state.LastCombatResult == null)
            return null;

        var r = state.LastCombatResult;
        return new
        {
            victory = r.Victory,
            xpGained = r.XpGained,
            levelUps = r.LevelUps,
            roundCount = r.RoundCount
        };
    }
}
