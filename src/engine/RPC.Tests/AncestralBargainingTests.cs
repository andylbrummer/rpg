using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Party;

namespace RPC.Tests;

/// <summary>
/// Ancestral Bargaining — a Compact (inkblood) signature mechanic. When the party fields a
/// Bonewarden AND holds Compact reputation >= 25, an "AncestralBargain" option is offered against
/// a tithe-construct encounter, letting the party settle the construct's tithe with a reputation
/// payment instead of fighting. Gating is strict: both the class AND the reputation floor are
/// required.
/// </summary>
public class AncestralBargainingTests
{
    private const int RepFloor = 25;

    private static CharacterState MakeChar(string name, string classId, int level = 1, int hp = 30)
        => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
            name, classId, level, 0,
            new BaseStats(4, 4, 4, 4, 4),
            hp, Equipment.Empty,
            Array.Empty<string>(), 0);

    private static EncounterTableRegistry TitheConstructRegistry()
    {
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("test", @"{
            ""id"": ""test"",
            ""name"": ""Test"",
            ""entries"": [
                { ""id"": ""tithe_construct"", ""weight"": 100, ""factionId"": ""inkblood"", ""enemies"": [{""enemyId"": ""tithe_construct_guardian"", ""count"": 1}] }
            ]
        }");
        return registry;
    }

    private static GameState MakeEncounter(int inkbloodRep, bool withBonewarden)
    {
        var gs = new GameState(seed: 1, encounterTables: TitheConstructRegistry());
        // Clear the seeded default party (which includes Bonewardens) so the test controls class composition.
        for (int i = 0; i < 6; i++) gs.Party.SetMember(i, default);
        gs.Party.SetMember(0, MakeChar("Hero", withBonewarden ? "bonewarden" : "stillblade"));
        gs.Reputation["inkblood"] = inkbloodRep;
        var dungeon = new Dungeon(5, 5, "test") { WanderingTableId = "test" };
        gs.EnterDungeon(dungeon, "test");
        gs.TriggerEncounter();
        return gs;
    }

    [Fact]
    public void Bargain_Option_Offered_With_Bonewarden_And_Rep_At_Floor()
    {
        var gs = MakeEncounter(RepFloor, withBonewarden: true);
        Assert.NotNull(gs.CurrentParley);
        Assert.Contains("AncestralBargain", gs.CurrentParley!.Options);
    }

    [Fact]
    public void Bargain_Option_NotOffered_Without_Bonewarden()
    {
        var gs = MakeEncounter(RepFloor + 20, withBonewarden: false);
        // No Bonewarden: bargaining is unavailable even at high Compact reputation.
        Assert.DoesNotContain("AncestralBargain", gs.CurrentParley?.Options ?? Array.Empty<string>());
    }

    [Fact]
    public void Bargain_Option_NotOffered_Below_Rep_Floor()
    {
        var gs = MakeEncounter(RepFloor - 1, withBonewarden: true);
        Assert.DoesNotContain("AncestralBargain", gs.CurrentParley?.Options ?? Array.Empty<string>());
    }

    [Fact]
    public void Bargain_Resolves_Peacefully_And_Pays_Tithe_In_Reputation()
    {
        var gs = MakeEncounter(RepFloor + 10, withBonewarden: true);
        var startingRep = gs.Reputation["inkblood"];

        var result = gs.ResolveParley("ancestralbargain");

        Assert.True(result);
        Assert.Null(gs.CurrentParley);
        Assert.Equal(GameMode.Exploration, gs.Mode);
        Assert.Null(gs.Combat);
        Assert.Equal(startingRep - 5, gs.Reputation["inkblood"]);
        Assert.Contains(gs.ActionLog, e => e.Type == "ancestral_bargain_struck"
            && e.Payload.GetValueOrDefault("factionId") == "inkblood");
        Assert.Contains(gs.ActionLog, e => e.Type == "encounter_resolved_peacefully");
    }

    [Fact]
    public void Bargain_Choice_Without_Eligibility_Degrades_To_Plain_Parley()
    {
        // A client could send the choice even when the option was never offered (no Bonewarden).
        // The engine must degrade safely to plain parley rather than grant the bargain.
        var gs = MakeEncounter(RepFloor + 10, withBonewarden: false);
        if (gs.CurrentParley is null) return; // parley not presented — nothing to assert
        var startingRep = gs.Reputation["inkblood"];

        var result = gs.ResolveParley("ancestralbargain");

        Assert.True(result);
        Assert.Null(gs.CurrentParley);
        Assert.Equal(startingRep + 2, gs.Reputation["inkblood"]);
        Assert.DoesNotContain(gs.ActionLog, e => e.Type == "ancestral_bargain_struck");
    }
}
