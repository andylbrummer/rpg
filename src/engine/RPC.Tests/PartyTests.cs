using RPC.Engine.Character;
using RPC.Engine.Party;

namespace RPC.Tests;

public class PartyTests
{
    private static CharacterState MakeChar(string name, int hp, int row = 0)
        => new(
            Guid.NewGuid(), name, "test", 1, 0,
            new BaseStats(4, 4, 4, 4, 4),
            hp, Equipment.Empty,
            Array.Empty<string>(), row);

    [Fact]
    public void Party_Create6Member()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("A", 10));
        party.SetMember(1, MakeChar("B", 10));
        party.SetMember(2, MakeChar("C", 10));
        party.SetMember(3, MakeChar("D", 10));
        party.SetMember(4, MakeChar("E", 10));
        party.SetMember(5, MakeChar("F", 10));

        Assert.Equal(6, party.Active.Count());
        Assert.True(party.IsFull);
    }

    [Fact]
    public void Party_FrontRowIndices_0To2()
    {
        var party = new PartyState();
        var front1 = MakeChar("Front1", 10);
        var front2 = MakeChar("Front2", 10);
        var front3 = MakeChar("Front3", 10);
        var back1 = MakeChar("Back1", 10);
        var back2 = MakeChar("Back2", 10);
        var back3 = MakeChar("Back3", 10);

        party.SetMember(0, front1);
        party.SetMember(1, front2);
        party.SetMember(2, front3);
        party.SetMember(3, back1);
        party.SetMember(4, back2);
        party.SetMember(5, back3);

        Assert.Equal(3, party.FrontRow.Count());
        Assert.Contains(front1, party.FrontRow);
        Assert.Contains(front2, party.FrontRow);
        Assert.Contains(front3, party.FrontRow);
        Assert.Equal(3, party.BackRow.Count());
    }

    [Fact]
    public void Party_SwapRows()
    {
        var party = new PartyState();
        var front = MakeChar("Front", 10);
        var back = MakeChar("Back", 10);

        party.SetMember(0, front);
        party.SetMember(3, back);

        party.SwapRows(0);

        Assert.Equal(back, party.Members[0]);
        Assert.Equal(front, party.Members[3]);
    }

    [Fact]
    public void Party_SwapRows_BackToFront()
    {
        var party = new PartyState();
        var front = MakeChar("Front", 10);
        var back = MakeChar("Back", 10);

        party.SetMember(2, front);
        party.SetMember(5, back);

        party.SwapRows(5);

        Assert.Equal(front, party.Members[5]);
        Assert.Equal(back, party.Members[2]);
    }

    [Fact]
    public void Party_DeadAutoMovedFromFront()
    {
        var party = new PartyState();
        var deadFront = MakeChar("Dead", 0);
        var livingBack = MakeChar("Living", 10);

        party.SetMember(0, deadFront);
        party.SetMember(3, livingBack);

        party.RebalanceDead();

        Assert.Equal(livingBack, party.Members[0]);
        Assert.Equal(deadFront, party.Members[3]);
    }

    [Fact]
    public void Party_RebalanceOnlySwapsWithLiving()
    {
        var party = new PartyState();
        var deadFront = MakeChar("DeadFront", 0);
        var deadBack = MakeChar("DeadBack", 0);

        party.SetMember(0, deadFront);
        party.SetMember(3, deadBack);

        party.RebalanceDead();

        // No living back-row to swap with; stays in place
        Assert.Equal(deadFront, party.Members[0]);
    }

    [Fact]
    public void Party_RebalanceFillsAllFrontSlots()
    {
        var party = new PartyState();
        var dead1 = MakeChar("Dead1", 0);
        var dead2 = MakeChar("Dead2", 0);
        var living1 = MakeChar("Living1", 10);
        var living2 = MakeChar("Living2", 10);
        var living3 = MakeChar("Living3", 10);

        party.SetMember(0, dead1);
        party.SetMember(1, dead2);
        party.SetMember(2, MakeChar("Dead3", 0));
        party.SetMember(3, living1);
        party.SetMember(4, living2);
        party.SetMember(5, living3);

        party.RebalanceDead();

        Assert.Equal(living1, party.Members[0]);
        Assert.Equal(living2, party.Members[1]);
        Assert.Equal(dead1, party.Members[3]);
    }

    [Fact]
    public void Party_SetMember_OutOfRange_Throws()
    {
        var party = new PartyState();
        Assert.Throws<ArgumentOutOfRangeException>(() => party.SetMember(6, MakeChar("X", 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() => party.SetMember(-1, MakeChar("X", 10)));
    }

    [Fact]
    public void Party_SwapRows_OutOfRange_Throws()
    {
        var party = new PartyState();
        Assert.Throws<ArgumentOutOfRangeException>(() => party.SwapRows(6));
        Assert.Throws<ArgumentOutOfRangeException>(() => party.SwapRows(-1));
    }
}
