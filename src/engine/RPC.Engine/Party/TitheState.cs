namespace RPC.Engine.Party;

/// <summary>
/// Tracks the party's standing bone-tithe obligation to the Ossuary Compact (the "inkblood"
/// faction). Tithe is billed on town entry at the campaign-act milestones (turns 1, 15, 25);
/// unpaid tokens accumulate as <see cref="Debt"/> and drive the ongoing tithe penalties until the
/// debt is cleared at the Bone Clerk. Mirrors the <see cref="EconomyState"/> feature-aggregate
/// pattern: <see cref="GameState"/> exposes a thin facade and the town service owns the logic.
/// </summary>
public class TitheState
{
    /// <summary>Milestone turns (1/15/25) already billed, so re-entering town never double-bills.</summary>
    public List<int> BilledMilestones { get; set; } = new();

    /// <summary>Outstanding unpaid tithe tokens owed to the Compact.</summary>
    public int Debt { get; set; }

    /// <summary>
    /// The earliest unpaid milestone turn (the turn the current debt was first billed), or null when
    /// there is no debt. Used to detect late payment — paying on a later turn than the milestone.
    /// </summary>
    public int? OutstandingSinceTurn { get; set; }

    /// <summary>True while the party owes tithe — the source of every ongoing tithe penalty.</summary>
    public bool HasDebt => Debt > 0;

    /// <summary>
    /// While in debt the Compact restricts fragment supply, raising component/vendor cost by 50%.
    /// 1.0 (no surcharge) once the debt is cleared.
    /// </summary>
    public double ComponentCostMultiplier => HasDebt ? 1.5 : 1.0;

    /// <summary>While in debt, Compact faction contacts refuse interaction until the debt is cleared.</summary>
    public bool ContactsRefuse => HasDebt;
}
