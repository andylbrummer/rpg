using RPC.Engine.Combat;

namespace RPC.Tests;

public class RangeBandTests
{
    private static Combatant Player(int row)
        => new(new Guid("11111111-1111-1111-1111-111111111111"), "Hero", true, 20, 20, 5, row, new List<StatusEffect>());

    private static Combatant Enemy(int row)
        => new(new Guid("22222222-2222-2222-2222-222222222222"), "Goblin", false, 10, 10, 4, row, new List<StatusEffect>());

    [Theory]
    [InlineData(0, 0, 0)] // same side, same row
    [InlineData(0, 1, 1)] // same side, front-back
    [InlineData(1, 1, 0)] // same side, both back
    [InlineData(0, 0, 1, false)] // front vs front (opposite sides)
    [InlineData(0, 1, 2, false)] // front vs back (opposite sides)
    [InlineData(1, 1, 3, false)] // back vs back (opposite sides)
    public void RangeBands_Distance(int rowA, int rowB, int expected, bool sameSide = true)
    {
        var a = Player(rowA);
        var b = sameSide ? new Combatant(new Guid("33333333-3333-3333-3333-333333333333"), "Ally", true, 20, 20, 5, rowB, new List<StatusEffect>()) : Enemy(rowB);
        Assert.Equal(expected, RangeBands.Distance(a, b));
    }

    [Theory]
    // Same side
    [InlineData("melee", 0, 0, true, true)]
    [InlineData("melee", 0, 1, false, true)]
    [InlineData("close", 0, 1, true, true)]
    // Opposite sides
    [InlineData("close", 0, 0, true, false)] // front-front = dist 1
    [InlineData("far", 0, 1, true, false)]   // front-back = dist 2
    [InlineData("extreme", 1, 1, true, false)] // back-back = dist 3
    [InlineData("melee", 0, 0, false, false)] // front-front is not melee
    [InlineData("any", 1, 1, true, false)]
    public void RangeBands_InRange(string range, int actorRow, int targetRow, bool expected, bool sameSide)
    {
        var actor = Player(actorRow);
        var target = sameSide ? new Combatant(new Guid("33333333-3333-3333-3333-333333333333"), "Ally", true, 20, 20, 5, targetRow, new List<StatusEffect>()) : Enemy(targetRow);
        Assert.Equal(expected, RangeBands.InRange(actor, target, range));
    }

    [Fact]
    public void RangeBands_ValidTargets_RespectsFilter()
    {
        var all = new[]
        {
            Player(0),
            new Combatant(new Guid("33333333-3333-3333-3333-333333333333"), "Ally", true, 20, 20, 5, 0, new List<StatusEffect>()),
            Enemy(0),
            Enemy(1)
        };

        var targets = RangeBands.ValidTargets(all[0], all, "any", "enemies");
        Assert.Equal(2, targets.Length);
        Assert.All(targets, t => Assert.False(t.IsPlayer));
    }

    [Fact]
    public void RangeBands_ValidTargets_RespectsRange()
    {
        var all = new[]
        {
            Player(0), // front
            Enemy(0),  // front, dist 1 (close)
            Enemy(1)   // back, dist 2 (far)
        };

        var meleeTargets = RangeBands.ValidTargets(all[0], all, "melee", "enemies");
        Assert.Empty(meleeTargets); // no same-row enemies

        var closeTargets = RangeBands.ValidTargets(all[0], all, "close", "enemies");
        Assert.Single(closeTargets);
        Assert.Equal("Goblin", closeTargets[0].Name); // front enemy at dist 1

        var farTargets = RangeBands.ValidTargets(all[0], all, "far", "enemies");
        Assert.Equal(2, farTargets.Length);
    }
}
