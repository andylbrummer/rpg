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
    public void Party_Create4Member()
    {
        var party = new PartyState();
        party.SetMember(0, MakeChar("A", 10));
        party.SetMember(1, MakeChar("B", 10));
        party.SetMember(2, MakeChar("C", 10));
        party.SetMember(3, MakeChar("D", 10));

        Assert.Equal(4, party.Active.Count());
        Assert.True(party.IsFull);
    }

    [Fact]
    public void Party_FrontRowIndices_0And1()
    {
        var party = new PartyState();
        var front1 = MakeChar("Front1", 10);
        var front2 = MakeChar("Front2", 10);
        var back1 = MakeChar("Back1", 10);
        var back2 = MakeChar("Back2", 10);

        party.SetMember(0, front1);
        party.SetMember(1, front2);
        party.SetMember(2, back1);
        party.SetMember(3, back2);

        Assert.Equal(2, party.FrontRow.Count());
        Assert.Contains(front1, party.FrontRow);
        Assert.Contains(front2, party.FrontRow);
        Assert.Equal(2, party.BackRow.Count());
    }

    [Fact]
    public void Party_SwapRows()
    {
        var party = new PartyState();
        var front = MakeChar("Front", 10);
        var back = MakeChar("Back", 10);

        party.SetMember(0, front);
        party.SetMember(2, back);

        party.SwapRows(0);

        Assert.Equal(back, party.Members[0]);
        Assert.Equal(front, party.Members[2]);
    }

    [Fact]
    public void Party_DeadAutoMovedFromFront()
    {
        var party = new PartyState();
        var deadFront = MakeChar("Dead", 0);
        var livingBack = MakeChar("Living", 10);

        party.SetMember(0, deadFront);
        party.SetMember(2, livingBack);

        party.RebalanceDead();

        Assert.Equal(livingBack, party.Members[0]);
        Assert.Equal(deadFront, party.Members[2]);
    }

    [Fact]
    public void Party_RebalanceOnlySwapsWithLiving()
    {
        var party = new PartyState();
        var deadFront = MakeChar("DeadFront", 0);
        var deadBack = MakeChar("DeadBack", 0);

        party.SetMember(0, deadFront);
        party.SetMember(2, deadBack);

        party.RebalanceDead();

        // No living back-row to swap with; stays in place
        Assert.Equal(deadFront, party.Members[0]);
    }
}
