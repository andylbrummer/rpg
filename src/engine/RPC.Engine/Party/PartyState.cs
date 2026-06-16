using RPC.Engine.Character;

namespace RPC.Engine.Party;

public class PartyState
{
    public CharacterState[] Members { get; } = new CharacterState[6];
    public List<CharacterState> Bench { get; } = new();
    public List<CharacterState> DeadCharacters { get; } = new();
    public ComponentStack[] ExpeditionCache { get; set; } = Array.Empty<ComponentStack>();
    public const int MaxExpeditionCacheSlots = 12;

    /// <summary>
    /// Town-only unlimited component store. Unlike the expedition cache (12 slots) and a
    /// character bag (8 slots), town storage has no slot cap — it is the long-term stash the
    /// party leaves behind in town. Access is gated to town (<see cref="GameMode.Menu"/>) by
    /// the command handler.
    /// </summary>
    public ComponentStack[] TownStorage { get; set; } = Array.Empty<ComponentStack>();

    /// <summary>Maximum total roster size (active + bench). Recruiting past this requires a dismissal.</summary>
    public const int MaxRosterSize = 12;

    /// <summary>Living active members plus benched members. Dead characters are not counted.</summary>
    public int RosterCount => Members.Count(c => c.Id != Guid.Empty) + Bench.Count;

    public IEnumerable<CharacterState> FrontRow => Members.Take(3).Where(c => c.IsAlive);
    public IEnumerable<CharacterState> BackRow => Members.Skip(3).Where(c => c.IsAlive);
    public IEnumerable<CharacterState> Active => Members.Where(c => c.IsAlive);

    public bool IsFull => Members.All(c => c.Id != Guid.Empty);

    public void SetMember(int slot, CharacterState character)
    {
        if (slot is < 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(slot));
        Members[slot] = character;
    }

    public void SwapRows(int slot)
    {
        if (slot is < 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(slot));

        var partnerSlot = slot < 3 ? slot + 3 : slot - 3;

        var a = Members[slot];
        var b = Members[partnerSlot];

        if (a.Id != Guid.Empty)
            a = a with { Row = partnerSlot < 3 ? 0 : 1 };
        if (b.Id != Guid.Empty)
            b = b with { Row = slot < 3 ? 0 : 1 };

        Members[slot] = b;
        Members[partnerSlot] = a;
    }

    /// <summary>
    /// Move a benched character into an active slot, sending the active occupant (if any) to the
    /// bench. When <paramref name="benchCharacterId"/> is null/empty this is a bench-out: the active
    /// occupant moves to the bench and the slot is emptied, which is rejected if it would leave the
    /// active party with no living members. Pure roster bookkeeping — town/combat gating lives in the
    /// caller. Returns false on any validation failure.
    /// </summary>
    public bool SwapActiveBench(int activeSlot, Guid? benchCharacterId)
    {
        if (activeSlot is < 0 or > 5)
            return false;

        var active = Members[activeSlot];

        if (benchCharacterId is { } id && id != Guid.Empty)
        {
            var benchIdx = Bench.FindIndex(c => c.Id == id);
            if (benchIdx < 0)
                return false;

            var benchChar = Bench[benchIdx];
            Members[activeSlot] = benchChar with { Row = activeSlot < 3 ? 0 : 1 };
            Bench.RemoveAt(benchIdx);
            if (active.Id != Guid.Empty)
                Bench.Add(active);
            return true;
        }

        // Bench-out only: move the active occupant to the bench, leaving the slot empty.
        if (active.Id == Guid.Empty)
            return false;
        if (Members.Count(c => c.Id != Guid.Empty) <= 1)
            return false;

        Members[activeSlot] = default;
        Bench.Add(active);
        return true;
    }

    /// <summary>
    /// Remove a character from the roster entirely (active or bench) — not the dead list. Refuses to
    /// dismiss the last living active member. Returns false if the id is not on the active roster or
    /// bench. Pure bookkeeping; town gating lives in the caller.
    /// </summary>
    public bool DismissCharacter(Guid characterId)
    {
        var benchIdx = Bench.FindIndex(c => c.Id == characterId);
        if (benchIdx >= 0)
        {
            Bench.RemoveAt(benchIdx);
            return true;
        }

        var activeSlot = Array.FindIndex(Members, m => m.Id == characterId);
        if (activeSlot < 0)
            return false;
        if (Members.Count(c => c.Id != Guid.Empty) <= 1)
            return false;

        Members[activeSlot] = default;
        return true;
    }

    public void RebalanceDead()
    {
        // Move dead characters out of front row by swapping with living back row members
        for (int front = 0; front < 3; front++)
        {
            if (!Members[front].IsAlive)
            {
                for (int back = 3; back < 6; back++)
                {
                    if (Members[back].IsAlive)
                    {
                        (Members[front], Members[back]) = (Members[back], Members[front]);
                        break;
                    }
                }
            }
        }
    }
}
