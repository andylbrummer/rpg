namespace RPC.Engine.Combat;

public class CombatState
{
    public List<Combatant> Combatants { get; } = new();
    public int Round { get; set; }
    public CombatPhase Phase { get; set; }
    public List<CombatAction> ActionQueue { get; } = new();
    
    public void RollInitiative()
    {
        foreach (var c in Combatants)
        {
            c.Initiative = c.Speed + Random.Shared.Next(1, 7); // d6
        }
        Combatants.Sort((a, b) => b.Initiative.CompareTo(a.Initiative));
    }
}

public enum CombatPhase
{
    Initiative,
    PlayerTurn,
    EnemyTurn,
    Resolution,
    Ended
}

public class Combatant
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsPlayer { get; set; }
    public int HP { get; set; }
    public int MaxHP { get; set; }
    public int Speed { get; set; }
    public int Initiative { get; set; }
    public int Row { get; set; } // 0 = front, 1 = back
    public List<StatusEffect> StatusEffects { get; } = new();
}

public class StatusEffect
{
    public string Type { get; set; } = "";
    public int Duration { get; set; }
    public int Magnitude { get; set; }
}

public class CombatAction
{
    public string ActorId { get; set; } = "";
    public ActionType Type { get; set; }
    public string? TargetId { get; set; }
    public string? AbilityId { get; set; }
}

public enum ActionType
{
    Attack,
    Defend,
    Cast,
    UseItem,
    Flee,
    Wait
}
