using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Overworld;

namespace RPC.Tests;

/// <summary>
/// Snapshot-style coverage for whole-campaign behaviour. Three hand-authored campaign configs with
/// distinct six-roll combinations (patron / threat / mastermind / scheme / wild card / complication)
/// assert the campaign-wide invariants, plus a deterministic 35-turn simulation that drives the
/// campaign to its turn limit and checks the end-state. Mirrors the seeded, content-from-disk style
/// of EventSchedulerTests / WildCardTests.
/// </summary>
public class CampaignSnapshotTests
{
    private static GameState CreateState(int seed = 42)
    {
        var registry = new ClassRegistry();
        foreach (var classFile in Directory.GetFiles("../../../../../../content/classes", "*.json"))
        {
            var json = File.ReadAllText(classFile);
            var classDef = System.Text.Json.JsonSerializer.Deserialize<ClassDef>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true
            });
            if (classDef != null)
                registry.LoadFromJson(classDef.Id, json);
        }
        return new GameState(seed, null, registry);
    }

    // ---- Three hand-authored campaign configs, distinct six-roll combinations ----
    // FactionPool = bureau, convocation, cartography, stillness, inkblood.
    // Each picks a different patron/threat/mastermind/scheme/wildcard/complication tuple and a
    // distinct faction timeline + wildcard turn threshold, so the cases exercise different paths.

    private static CampaignConfig ConfigFor(string key) => key switch
    {
        "A" => BuildConfig("bureau", "convocation", "cartography",
            SchemeType.BloomHarvest, "stillness", ComplicationType.BloomSiege,
            preparing: 10, executing: 20, wildcardThreshold: 18),
        "B" => BuildConfig("convocation", "stillness", "inkblood",
            SchemeType.EngineSeizure, "bureau", ComplicationType.ErraticEngine,
            preparing: 12, executing: 24, wildcardThreshold: 20),
        "C" => BuildConfig("cartography", "inkblood", "bureau",
            SchemeType.TheResurrection, "convocation", ComplicationType.OpenWar,
            preparing: 8, executing: 18, wildcardThreshold: 15),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown campaign config key")
    };

    private static CampaignConfig BuildConfig(
        string patron, string threat, string mastermind,
        SchemeType scheme, string wildCard, ComplicationType complication,
        int preparing, int executing, int wildcardThreshold)
    {
        var timelines = new Dictionary<string, FactionTimeline>();
        foreach (var faction in CampaignConfig.FactionPool)
            timelines[faction] = new FactionTimeline(preparing, executing);

        return new CampaignConfig
        {
            Patron = patron,
            Threat = threat,
            Mastermind = mastermind,
            Scheme = scheme,
            WildCard = wildCard,
            Complication = complication,
            EvidenceChain = Enumerable.Range(0, 11).Select(i => $"evidence_{i}").ToList(),
            FactionTimelines = timelines,
            NpcCasting = new Dictionary<string, string>
            {
                { "patron_contact", $"npc_{patron}" },
                { "threat_contact", $"npc_{threat}" },
                { "mastermind_contact", $"npc_{mastermind}" },
                { "wildcard_contact", $"npc_{wildCard}" }
            },
            WildcardTrigger = new WildcardTrigger(wildCard, wildcardThreshold)
        };
    }

    public static IEnumerable<object[]> ConfigKeys => new[]
    {
        new object[] { "A" },
        new object[] { "B" },
        new object[] { "C" }
    };

    [Theory]
    [MemberData(nameof(ConfigKeys))]
    public void HandAuthoredConfig_IsValid(string key)
    {
        var config = ConfigFor(key);

        Assert.True(config.Validate(out var error), error);
        Assert.Equal("", error);
        // Six-roll fields are all populated and internally consistent.
        Assert.Contains(config.Patron, CampaignConfig.FactionPool);
        Assert.Contains(config.Threat, CampaignConfig.FactionPool);
        Assert.Contains(config.Mastermind, CampaignConfig.FactionPool);
        Assert.Contains(config.WildCard, CampaignConfig.FactionPool);
        Assert.DoesNotContain(config.WildCard, new[] { config.Patron, config.Threat, config.Mastermind });
    }

    [Theory]
    [MemberData(nameof(ConfigKeys))]
    public void HandAuthoredConfig_ProducesValidDungeonSequence(string key)
    {
        var gs = CreateState();
        var config = ConfigFor(key);

        gs.GenerateOverworld(config);

        var dungeons = gs.Overworld.Nodes.Values.Where(n => n.Type == NodeType.Dungeon).ToList();
        Assert.NotEmpty(dungeons);
        // The canonical first dungeon is always present in a generated overworld.
        Assert.Contains(dungeons, d => d.Id == "broken_engine");
        // Every dungeon carries a template id so it can be instantiated.
        Assert.All(dungeons, d => Assert.False(string.IsNullOrEmpty(d.DungeonTemplateId)));

        // Valid sequence == every dungeon is reachable from the starting town through the route
        // graph (BuildRoutes always lays a connecting chain over all nodes).
        var reachable = ReachableFrom(gs.Overworld, "the_reach");
        foreach (var dungeon in dungeons)
            Assert.Contains(dungeon.Id, reachable);
    }

    [Theory]
    [MemberData(nameof(ConfigKeys))]
    public void HandAuthoredConfig_EvidenceCountReachesTen(string key)
    {
        var gs = CreateState();
        var config = ConfigFor(key);
        gs.GenerateOverworld(config);

        for (int i = 0; i < 10; i++)
            gs.AddEvidence(config.Mastermind, "investigation");

        Assert.True(gs.Evidence.Counters[config.Mastermind] >= 10);
        Assert.Equal(10, gs.Evidence.GetThreshold(config.Mastermind));
        // Crossing the top threshold makes the mastermind accusable.
        Assert.True(gs.AccuseFaction(config.Mastermind));
    }

    [Theory]
    [MemberData(nameof(ConfigKeys))]
    public void HandAuthoredConfig_FactionTransitionsAtCorrectTurns(string key)
    {
        var gs = CreateState();
        var config = ConfigFor(key);
        gs.GenerateOverworld(config);

        var timeline = config.FactionTimelines[config.Threat];

        gs.Overworld.Turns = timeline.Preparing - 1;
        Assert.Equal(FactionState.Investigating, gs.GetFactionState(config.Threat));

        gs.Overworld.Turns = timeline.Preparing;
        Assert.Equal(FactionState.Preparing, gs.GetFactionState(config.Threat));

        gs.Overworld.Turns = timeline.Executing - 1;
        Assert.Equal(FactionState.Preparing, gs.GetFactionState(config.Threat));

        gs.Overworld.Turns = timeline.Executing;
        Assert.Equal(FactionState.Executing, gs.GetFactionState(config.Threat));
    }

    [Theory]
    [MemberData(nameof(ConfigKeys))]
    public void HandAuthoredConfig_WildCardTriggerConditionsReachable(string key)
    {
        var gs = CreateState();
        var config = ConfigFor(key);
        gs.GenerateOverworld(config);

        var trigger = config.WildcardTrigger!;

        // Below threshold OR below rep: no trigger.
        gs.Overworld.Turns = trigger.TurnThreshold - 1;
        gs.Reputation[trigger.FactionId] = 25;
        Assert.False(gs.CheckWildCardTrigger());
        Assert.Equal(WildCardAllianceStatus.None, gs.WildCardAllianceStatus);

        // Both conditions met: the alliance offer becomes reachable.
        gs.Overworld.Turns = trigger.TurnThreshold;
        gs.Reputation[trigger.FactionId] = 20;
        Assert.True(gs.CheckWildCardTrigger());
        Assert.Equal(WildCardAllianceStatus.Offered, gs.WildCardAllianceStatus);
    }

    [Theory]
    [InlineData("A", 1)]
    [InlineData("B", 7)]
    [InlineData("C", 99)]
    public void Campaign_ThirtyFiveTurnSimulation_DrivesToEndStateInvariants(string key, int seed)
    {
        var gs = CreateState(seed);
        var config = ConfigFor(key);
        gs.GenerateOverworld(config);

        // Active play so the event scheduler / faction interactions tick each turn.
        gs.Mode = GameMode.Exploration;
        gs.Overworld.Turns = 0;
        gs.CampaignEnded = false;

        // Deterministically advance one turn at a time until the campaign closes. Running to
        // completion without an exception is itself the "no crash" invariant.
        int safety = 0;
        while (!gs.CampaignEnded && gs.Overworld.Turns < 35)
        {
            gs.IncrementTurns(1);
            Assert.True(++safety <= 40, "Simulation failed to terminate within 40 iterations");
        }

        // End-state invariants.
        Assert.Equal(35, gs.Overworld.Turns);                // clamps exactly at the turn limit
        Assert.True(gs.CampaignEnded);                       // campaign concluded
        Assert.Equal(GameMode.Menu, gs.Mode);                // forced back to the hub
        Assert.Null(gs.CurrentDungeon);                      // no dungeon left active

        // Milestone: the threat faction reached its Executing phase (timeline executing <= 35) and
        // the transition was announced during play.
        Assert.Equal(FactionState.Executing, gs.GetFactionState(config.Threat));
        Assert.Contains(gs.ActionLog, e => e.Type == "state_executing" && e.Payload["factionId"] == config.Threat);

        // Consistent state: settlement fates were resolved (none left pending) as the campaign closed.
        Assert.NotEmpty(gs.WorldState.Settlements);
        Assert.DoesNotContain(gs.WorldState.Settlements.Values, v => v == "pending");
    }

    /// <summary>Undirected BFS over the overworld route graph from <paramref name="start"/>.</summary>
    private static HashSet<string> ReachableFrom(OverworldState overworld, string start)
    {
        var visited = new HashSet<string> { start };
        var queue = new Queue<string>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            foreach (var route in overworld.Routes)
            {
                var next = route.From == node ? route.To
                    : route.To == node ? route.From
                    : null;
                if (next != null && visited.Add(next))
                    queue.Enqueue(next);
            }
        }
        return visited;
    }
}
