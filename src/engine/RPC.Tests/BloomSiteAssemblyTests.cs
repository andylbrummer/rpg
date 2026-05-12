using System.Text.Json;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Party;

namespace RPC.Tests;

public class BloomSiteAssemblyTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    private static string ContentPath(string relative)
        => $"../../../../../../content/{relative}";

    private static EnemyRegistry LoadBloomEnemies()
    {
        var registry = new EnemyRegistry();
        foreach (var id in new[] { "bloom_mite", "bloom_wretch", "bloom_sporeling" })
        {
            var path = ContentPath($"enemies/{id}.json");
            var json = File.ReadAllText(path);
            registry.LoadFromJson(id, json);
        }
        return registry;
    }

    private static CharacterState MakeChar(string name, int hp, int speed, int row = 0)
        => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
            name, "test", 1, 0,
            new BaseStats(4, speed, 4, 4, 4),
            hp, Equipment.Empty,
            Array.Empty<string>(), row);

    [Fact]
    public void BloomSite_SegmentsLoad()
    {
        var dir = ContentPath("segments/bloom-site");
        Assert.True(Directory.Exists(dir), "Missing bloom-site segments directory");

        var segments = SegmentLoader.LoadFromDirectory(dir);
        Assert.Equal(5, segments.Count);

        var expectedIds = new[]
        {
            "bloom_entrance",
            "spore_corridor",
            "bloom_chamber",
            "decay_lab",
            "spore_nest"
        };
        foreach (var id in expectedIds)
        {
            Assert.Contains(segments, s => s.Id == id);
        }
    }

    [Fact]
    public void BloomSite_EncounterTable_HasDangerRatings()
    {
        var path = ContentPath("encounters/bloom_site.json");
        var json = File.ReadAllText(path);
        var registry = new EncounterTableRegistry();
        registry.LoadFromJson("bloom_site", json);

        var table = registry.Get("bloom_site");
        Assert.NotNull(table);

        var groups = table.Entries
            .GroupBy(e => e.DangerRating)
            .ToDictionary(g => g.Key, g => g.ToArray());

        for (int dr = 1; dr <= 5; dr++)
        {
            Assert.True(groups.ContainsKey(dr), $"Missing dangerRating {dr} entries");
            Assert.NotEmpty(groups[dr]);
        }
    }

    [Fact]
    public void BloomSite_EnemiesHaveAiBehavior()
    {
        var registry = LoadBloomEnemies();

        var mite = registry.Get("bloom_mite");
        Assert.NotNull(mite);
        Assert.Equal("pack_hunter", mite.Ai);

        var wretch = registry.Get("bloom_wretch");
        Assert.NotNull(wretch);
        Assert.Equal("aggressive", wretch.Ai);

        var sporeling = registry.Get("bloom_sporeling");
        Assert.NotNull(sporeling);
        Assert.Equal("ranged_priority", sporeling.Ai);
    }

    [Fact]
    public void BloomSite_EnemySpawn_UsesCorrectBehavior()
    {
        var registry = LoadBloomEnemies();
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 20, 5));

        var encounter = new EncounterDef(
            "bs-d3-wretch-sporeling",
            "Bloom Wretches and Sporeling",
            new[]
            {
                new EnemySpawn("bloom_wretch", 2, 0),
                new EnemySpawn("bloom_sporeling", 1, 1)
            });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(42), registry);

        var wretches = state.Combatants
            .Where(c => !c.IsPlayer && c.Name.StartsWith("Bloom Wretch"))
            .ToArray();
        var sporeling = state.Combatants
            .First(c => !c.IsPlayer && c.Name.StartsWith("Bloom Sporeling"));

        Assert.Equal(2, wretches.Length);
        Assert.All(wretches, w => Assert.Equal("aggressive", w.AiBehavior));
        Assert.Equal("ranged_priority", sporeling.AiBehavior);
    }

    [Fact]
    public void BloomSite_PackHunter_TargetsDenseRow()
    {
        var registry = LoadBloomEnemies();
        var party = new PartyState();
        party.SetMember(0, MakeChar("FrontA", 20, 5, 0));
        party.SetMember(1, MakeChar("FrontB", 20, 5, 0));
        party.SetMember(2, MakeChar("BackA", 20, 5, 1));

        var encounter = new EncounterDef(
            "test",
            "Test",
            new[] { new EnemySpawn("bloom_mite", 1) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(1), registry);

        while (state.Phase != CombatPhase.Resolve)
        {
            state = CombatEngine.Tick(state, null, new GameRandom(1));
        }

        Assert.NotNull(state.PendingAction);
        var target = state.Combatants.First(c => c.Id == state.PendingAction.TargetId);
        Assert.True(target.Row == 0, "pack_hunter should target front row with more enemies");
    }

    [Fact]
    public void BloomSite_Combat_ResolvesWithBloomCreatures()
    {
        var registry = LoadBloomEnemies();
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 50, 10));

        var encounter = new EncounterDef(
            "bs-d1-mites-pair",
            "Bloom Mite Pair",
            new[] { new EnemySpawn("bloom_mite", 2) });

        var state = CombatEngine.Enter(party, encounter, new GameRandom(42), registry);

        Assert.Equal(3, state.Combatants.Length);
        Assert.Contains(state.Combatants, c => !c.IsPlayer && c.Name.StartsWith("Bloom Mite"));

        int steps = 0;
        while (!state.IsFinished && steps < 100)
        {
            var actor = state.CurrentActor;
            if (actor?.IsPlayer == true)
            {
                var enemy = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                state = CombatEngine.Tick(
                    state,
                    new CombatAction(actor.Value.Id, ActionType.Attack, enemy.Id, null, null),
                    new GameRandom(42));
            }
            else
            {
                state = CombatEngine.Tick(state, null, new GameRandom(42));
            }
            steps++;
        }

        Assert.Equal(CombatPhase.Ended, state.Phase);
        Assert.Contains("Victory", state.Log.Last().Message);
    }
}
