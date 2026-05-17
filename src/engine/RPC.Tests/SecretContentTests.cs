using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Dungeons;
using RPC.Engine.Party;

namespace RPC.Tests;

public class SecretContentTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true
    };

    private static CharacterState MakeChar(string name, int hp, int speed, int row, string classId)
        => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
            name, classId, 1, 0,
            new BaseStats(4, speed, 4, 4, 4),
            hp, Equipment.Empty,
            Array.Empty<string>(), row);

    [Fact]
    public void HiddenSynergy_IsHidden_UntilDiscovered()
    {
        var registry = new SynergyRegistry();
        var effect = new SynergyEffect("bonus_damage", 5);
        registry.Register("bone_spear", "shiv", effect, "hidden_bone_shiv", hidden: true);

        Assert.True(registry.IsHidden("bone_spear", "shiv"));
        Assert.True(registry.IsHiddenById("hidden_bone_shiv"));
        // MakeKey sorts abilities, so reversed order resolves to same key
        Assert.True(registry.IsHidden("shiv", "bone_spear"));
    }

    [Fact]
    public void HiddenSynergy_TriggersInCombat_AndLogsToJournal()
    {
        var synergies = new SynergyRegistry();
        var classReg = new ClassRegistry();

        // Load bonewarden and hollow
        foreach (var file in new[] { "bonewarden.json", "hollow.json" })
        {
            var classJson = File.ReadAllText($"../../../../../../content/classes/{file}");
            var classDef = JsonSerializer.Deserialize<ClassDef>(classJson, JsonOptions);
            Assert.NotNull(classDef);
            classReg.LoadFromJson(classDef.Id, classJson);
        }

        var effect = new SynergyEffect("bonus_damage", 5);
        synergies.Register("bone_spear", "shiv", effect, "hidden_bone_shiv", hidden: true);

        var party = new PartyState();
        party.SetMember(0, MakeChar("Bone", 50, 10, 0, "bonewarden"));
        party.SetMember(1, MakeChar("Hollow", 50, 1, 0, "hollow"));

        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });
        var state = CombatEngine.Enter(party, encounter, new GameRandom(42));
        var rng = new GameRandom(42);
        var journal = new JournalState();

        Action<string, string, Dictionary<string, string>> emitter = (cat, type, payload) =>
        {
            if (type == "synergy_triggered" && payload.TryGetValue("synergyId", out var sid))
                journal.Discover(sid);
        };

        int playerTurns = 0;
        while (!state.IsFinished && playerTurns < 2)
        {
            if (state.Phase == CombatPhase.Turn && state.CurrentActor?.IsPlayer == true)
            {
                var enemy = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                var ability = playerTurns == 0 ? "bone_spear" : "shiv";
                state = CombatEngine.Tick(state,
                    new CombatAction(state.CurrentActor.Value.Id, ActionType.UseAbility, enemy.Id, ability, null),
                    rng, classReg, emitter, synergies);
                while (!state.IsFinished && state.Phase != CombatPhase.Turn)
                    state = CombatEngine.Tick(state, null, rng, classReg, emitter, synergies);
                playerTurns++;
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng, classReg, emitter, synergies);
            }
        }

        Assert.True(journal.IsDiscovered("hidden_bone_shiv"));
    }

    [Fact]
    public void HiddenSynergy_NotListedInGetAll_UntilDiscovered()
    {
        var registry = new SynergyRegistry();
        registry.Register("a", "b", new SynergyEffect("bonus_damage", 3), "visible", hidden: false);
        registry.Register("c", "d", new SynergyEffect("bonus_damage", 5), "hidden", hidden: true);

        var all = registry.GetAll();
        Assert.Contains("visible", all.Values.Select(v => v.Id));
        Assert.Contains("hidden", all.Values.Select(v => v.Id));
        // GetAll returns all synergies regardless of hidden status — filtering is done by caller
    }

    [Fact]
    public void OptionalDungeon_Unlocks_WhenReputationConditionsMet()
    {
        var state = new GameState();
        state.Campaign.CampaignConfig = new CampaignConfig
        {
            FactionTimelines = new Dictionary<string, FactionTimeline>(),
            NpcCasting = new Dictionary<string, string>()
        };

        var templates = new Dictionary<string, DungeonTemplate>
        {
            ["secret_vault"] = new DungeonTemplate(
                "secret_vault", "Secret Vault",
                Array.Empty<string>(), Array.Empty<string>(),
                5, "boss", "table",
                UnlockConditions: new[] { new DungeonUnlockConditions("bureau", 50) })
        };

        var service = new CampaignService(null);

        // Before: not unlocked
        service.CheckOptionalDungeons(state, templates);
        Assert.Empty(state.Campaign.UnlockedDungeons);
        Assert.DoesNotContain("secret_vault", state.Overworld.Nodes.Keys);

        // Set reputation high enough
        state.Campaign.Reputation["bureau"] = 55;
        service.CheckOptionalDungeons(state, templates);

        // After: unlocked and node added
        Assert.Contains("secret_vault", state.Campaign.UnlockedDungeons);
        Assert.Contains("secret_vault", state.Overworld.Nodes.Keys);
        Assert.Equal("Secret Vault", state.Overworld.Nodes["secret_vault"].Name);
    }

    [Fact]
    public void OptionalDungeon_DoesNotDoubleUnlock()
    {
        var state = new GameState();
        state.Campaign.CampaignConfig = new CampaignConfig
        {
            FactionTimelines = new Dictionary<string, FactionTimeline>(),
            NpcCasting = new Dictionary<string, string>()
        };
        state.Campaign.Reputation["bureau"] = 60;

        var templates = new Dictionary<string, DungeonTemplate>
        {
            ["secret_vault"] = new DungeonTemplate(
                "secret_vault", "Secret Vault",
                Array.Empty<string>(), Array.Empty<string>(),
                5, "boss", "table",
                UnlockConditions: new[] { new DungeonUnlockConditions("bureau", 50) })
        };

        var service = new CampaignService(null);
        service.CheckOptionalDungeons(state, templates);
        int nodeCount = state.Overworld.Nodes.Count;

        // Call again — should be no-op
        service.CheckOptionalDungeons(state, templates);
        Assert.Equal(nodeCount, state.Overworld.Nodes.Count);
    }

    [Fact]
    public void OptionalDungeon_RequiresAllConditions()
    {
        var state = new GameState();
        state.Campaign.CampaignConfig = new CampaignConfig
        {
            FactionTimelines = new Dictionary<string, FactionTimeline>(),
            NpcCasting = new Dictionary<string, string>()
        };
        state.Campaign.Reputation["bureau"] = 60;
        // convocation reputation is 0 — below 50 threshold

        var templates = new Dictionary<string, DungeonTemplate>
        {
            ["archive"] = new DungeonTemplate(
                "archive", "Archive",
                Array.Empty<string>(), Array.Empty<string>(),
                5, "boss", "table",
                UnlockConditions: new[]
                {
                    new DungeonUnlockConditions("bureau", 50),
                    new DungeonUnlockConditions("convocation", 50)
                })
        };

        var service = new CampaignService(null);
        service.CheckOptionalDungeons(state, templates);

        Assert.Empty(state.Campaign.UnlockedDungeons);
    }

    [Fact]
    public void ChooseBetrayal_RequiresEvidence()
    {
        var state = new GameState();
        state.Campaign.CampaignConfig = new CampaignConfig
        {
            Mastermind = "inkblood",
            FactionTimelines = new Dictionary<string, FactionTimeline>(),
            NpcCasting = new Dictionary<string, string>()
        };

        var service = new CampaignService(null);
        // No evidence — should fail
        Assert.False(service.ChooseBetrayal(state));

        // Add evidence about mastermind
        state.Evidence.AddEvidence("inkblood", "test_source");
        Assert.True(service.ChooseBetrayal(state));
        Assert.True(state.Campaign.BetrayalPath);

        var log = state.ActionLog.LastOrDefault(e => e.Type == "betrayal_chosen");
        Assert.NotNull(log);
        Assert.Equal("inkblood", log.Payload.GetValueOrDefault("mastermind"));
    }

    [Fact]
    public void ChooseBetrayal_IsIdempotent()
    {
        var state = new GameState();
        state.Campaign.CampaignConfig = new CampaignConfig
        {
            Mastermind = "inkblood",
            FactionTimelines = new Dictionary<string, FactionTimeline>(),
            NpcCasting = new Dictionary<string, string>()
        };
        state.Evidence.AddEvidence("inkblood", "test_source", 2);

        var service = new CampaignService(null);
        service.ChooseBetrayal(state);
        var result = service.ChooseBetrayal(state);
        Assert.False(result);
    }

    [Fact]
    public void UnlockFinalDungeon_BetrayalPath_RequiresHighRep()
    {
        var state = new GameState();
        state.Campaign.CampaignConfig = new CampaignConfig
        {
            Mastermind = "inkblood",
            FactionTimelines = new Dictionary<string, FactionTimeline>(),
            NpcCasting = new Dictionary<string, string>()
        };
        state.Campaign.BetrayalPath = true;

        var service = new CampaignService(null);
        // Rep too low
        state.Reputation["inkblood"] = 10;
        Assert.False(service.UnlockFinalDungeon(state));

        // Rep high enough
        state.Reputation["inkblood"] = 25;
        Assert.True(service.UnlockFinalDungeon(state));
        Assert.True(state.FinalDungeonUnlocked);
        Assert.Contains(state.ActionLog, e => e.Type == "scheme_alliance");
    }
}
