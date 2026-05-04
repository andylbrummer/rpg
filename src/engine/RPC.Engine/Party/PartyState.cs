using RPC.Engine.Character;

namespace RPC.Engine.Party;

public class PartyState
{
    public CharacterState[] Members { get; } = new CharacterState[4];

    public IEnumerable<CharacterState> FrontRow => Members.Take(2).Where(c => c.IsAlive);
    public IEnumerable<CharacterState> BackRow => Members.Skip(2).Where(c => c.IsAlive);
    public IEnumerable<CharacterState> Active => Members.Where(c => c.IsAlive);

    public bool IsFull => Members.All(c => c.Id != Guid.Empty);

    public void SetMember(int slot, CharacterState character)
    {
        if (slot is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(slot));
        Members[slot] = character;
    }

    public void SwapRows(int slot)
    {
        if (slot is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(slot));

        var partnerSlot = slot < 2 ? slot + 2 : slot - 2;
        (Members[slot], Members[partnerSlot]) = (Members[partnerSlot], Members[slot]);
    }

    public void RebalanceDead()
    {
        // Move dead characters out of front row by swapping with living back row members
        for (int front = 0; front < 2; front++)
        {
            if (!Members[front].IsAlive)
            {
                for (int back = 2; back < 4; back++)
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
