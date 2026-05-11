using RPC.Engine.Character;

namespace RPC.Engine.Party;

public class PartyState
{
    public CharacterState[] Members { get; } = new CharacterState[6];

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
