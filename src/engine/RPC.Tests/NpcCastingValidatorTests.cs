using System.Linq;
using RPC.Engine.Campaign;
using RPC.Engine.Combat;

namespace RPC.Tests;

public class NpcCastingValidatorTests
{
    private static CampaignConfig Config() => new()
    {
        Patron = "bureau",
        Threat = "convocation",
        Mastermind = "inkblood",
        WildCard = "stillness",
    };

    [Fact]
    public void ValidCasting_HasNoIssues()
    {
        var casting = new Dictionary<string, string> { ["npc_b"] = "questGiver", ["npc_i"] = "boss" };
        var factions = new Dictionary<string, string> { ["npc_b"] = "bureau", ["npc_i"] = "inkblood" };

        Assert.True(NpcCastingValidator.IsValid(casting, Config(), factions));
    }

    [Fact]
    public void RoleConflict_WhenTwoNpcsShareARole()
    {
        var casting = new Dictionary<string, string> { ["a"] = "boss", ["b"] = "boss" };
        var issues = NpcCastingValidator.Validate(casting, Config());
        // boss filled twice (conflict) + questGiver missing
        Assert.Contains(issues, i => i.Kind == "role_conflict" && i.Role == "boss");
    }

    [Fact]
    public void MissingRequiredRole_IsReported()
    {
        var casting = new Dictionary<string, string> { ["a"] = "questGiver" };
        var issues = NpcCastingValidator.Validate(casting, Config());
        Assert.Contains(issues, i => i.Kind == "missing_role" && i.Role == "boss");
    }

    [Fact]
    public void FactionMismatch_IsReported()
    {
        var casting = new Dictionary<string, string> { ["npc_x"] = "boss", ["npc_b"] = "questGiver" };
        var factions = new Dictionary<string, string> { ["npc_x"] = "bureau", ["npc_b"] = "bureau" };

        var issues = NpcCastingValidator.Validate(casting, Config(), factions);
        // boss must be mastermind (inkblood); npc_x is bureau -> mismatch.
        Assert.Contains(issues, i => i.Kind == "faction_mismatch" && i.Role == "boss" && i.NpcId == "npc_x");
        // questGiver bureau matches Patron bureau -> no mismatch for it.
        Assert.DoesNotContain(issues, i => i.Kind == "faction_mismatch" && i.NpcId == "npc_b");
    }

    [Fact]
    public void ExpectedFaction_MapsRolesToCampaignFactions()
    {
        var c = Config();
        Assert.Equal("bureau", NpcCastingValidator.ExpectedFaction("questGiver", c));
        Assert.Equal("inkblood", NpcCastingValidator.ExpectedFaction("boss", c));
        Assert.Equal("convocation", NpcCastingValidator.ExpectedFaction("threat_lieutenant", c));
        Assert.Null(NpcCastingValidator.ExpectedFaction("random_role", c));
    }

    [Fact]
    public void Repair_FillsRequiredRolesFromCorrectFactions()
    {
        var pool = new Dictionary<string, string>
        {
            ["p1"] = "bureau",
            ["m1"] = "inkblood",
            ["m2"] = "inkblood",
            ["other"] = "stillness",
        };

        var (casting, complete) = NpcCastingValidator.Repair(Config(), pool, new GameRandom(1));

        Assert.True(complete);
        Assert.Equal("questGiver", casting["p1"]);                 // only bureau NPC
        Assert.Contains(casting, kv => kv.Value == "boss" && (kv.Key == "m1" || kv.Key == "m2"));
        Assert.True(NpcCastingValidator.IsValid(casting, Config(), pool));
    }

    [Fact]
    public void Repair_IncompleteWhenNoCandidateForRole()
    {
        var pool = new Dictionary<string, string> { ["p1"] = "bureau" }; // no mastermind faction NPC
        var (casting, complete) = NpcCastingValidator.Repair(Config(), pool, new GameRandom(1));

        Assert.False(complete);
        Assert.DoesNotContain(casting, kv => kv.Value == "boss");
    }

    [Fact]
    public void Repair_NeverDoubleCastsSameNpc()
    {
        // Two roles expecting the same faction must draw two distinct NPCs.
        var pool = new Dictionary<string, string> { ["p1"] = "bureau", ["p2"] = "bureau" };
        var (casting, complete) = NpcCastingValidator.Repair(
            Config(), pool, new GameRandom(2), new[] { "questGiver", "patron_contact" });

        Assert.True(complete);
        Assert.Equal(2, casting.Count);
        Assert.Equal(2, casting.Keys.Distinct().Count());
    }
}
