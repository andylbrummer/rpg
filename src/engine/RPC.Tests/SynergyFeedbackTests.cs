using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Party;

namespace RPC.Tests;

public class SynergyFeedbackTests
{
    private static CharacterState MakeChar(string name, int hp, int speed, int row = 0, string classId = "test")
        => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
            name, classId, 1, 0,
            new BaseStats(4, speed, 4, 4, 4),
            hp, Equipment.Empty,
            new[] { "ability_a", "ability_b" }, row);

    public SynergyFeedbackTests()
    {
        SynergyRegistry.Clear();
    }

    [Fact]
    public void SynergyTrigger_EmitsActionLog_WithCorrectPayload()
    {
        SynergyRegistry.Register("ability_a", "ability_b", new SynergyEffect("bonus_damage", 5), "test_synergy");

        var registry = new ClassRegistry();
        var json = """
            {
              "id": "synergy_test",
              "name": "Synergy Test",
              "description": "Test",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 5, "intelligence": 3, "willpower": 3 },
              "abilities": [
                { "id": "ability_a", "name": "A", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] },
                { "id": "ability_b", "name": "B", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["ability_a", "ability_b"] }
              ]
            }
            """;
        registry.LoadFromJson("synergy_test", json);

        var gs = new GameState(seed: 42, classRegistry: registry);
        var party = new PartyState();
        party.SetMember(0, MakeChar("Hero", 20, 10, 0, "synergy_test"));
        party.SetMember(1, MakeChar("Ally", 20, 1, 0, "synergy_test"));
        gs.Party = party;

        var encounter = new EncounterDef("test_enc", "Test", new[]
        {
            new EnemySpawn("rat", 2)
        });

        gs.TriggerEncounter(encounter);
        Assert.Equal(GameMode.Combat, gs.Mode);

        // Clear action log to ignore encounter_started
        gs.ActionLog.Clear();

        // Submit abilities on player turns until synergy triggers or max actions reached
        int actionsSubmitted = 0;
        int maxActions = 10;
        while (gs.Mode == GameMode.Combat && actionsSubmitted < maxActions)
        {
            var currentActor = gs.Combat!.CurrentActor;
            Assert.NotNull(currentActor);
            Assert.True(currentActor.Value.IsPlayer, "Expected player turn");

            var target = gs.Combat.Combatants.First(c => !c.IsPlayer && c.IsAlive);
            var abilityId = actionsSubmitted % 2 == 0 ? "ability_a" : "ability_b";
            var action = new CombatAction(currentActor.Value.Id, ActionType.UseAbility, target.Id, abilityId, null);
            gs.SubmitCombatAction(action);
            actionsSubmitted++;
        }

        var synergyEntry = gs.ActionLog.FirstOrDefault(e => e.Type == "synergy_triggered");
        Assert.NotNull(synergyEntry);
        Assert.Equal("combat", synergyEntry.Category);
        Assert.Equal("test_synergy", synergyEntry.Payload["synergyId"]);
        Assert.False(string.IsNullOrEmpty(synergyEntry.Payload["encounterId"]));
        Assert.False(string.IsNullOrEmpty(synergyEntry.Payload["targetId"]));
    }

    [Fact]
    public void AntiSynergy_NoTrigger_NoActionLog()
    {
        // Anti-synergy pairs are not registered in SynergyRegistry
        var registry = new ClassRegistry();
        var bwJson = """
            {
              "id": "bonewarden",
              "name": "Bonewarden",
              "description": "Test",
              "baseStats": { "strength": 4, "dexterity": 3, "constitution": 5, "intelligence": 4, "willpower": 4 },
              "abilities": [
                { "id": "bone_spear", "name": "Bone Spear", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["bone_spear"] }
              ]
            }
            """;
        var sbJson = """
            {
              "id": "stillblade",
              "name": "Stillblade",
              "description": "Test",
              "baseStats": { "strength": 5, "dexterity": 5, "constitution": 4, "intelligence": 3, "willpower": 4 },
              "abilities": [
                { "id": "rend", "name": "Rend", "cost": { "type": "none" }, "effect": { "type": "damage", "value": "1d4", "range": "melee" }, "tags": ["test"] }
              ],
              "levelTable": [
                { "level": 1, "hpGain": 0, "statGain": { "strength": 0, "dexterity": 0, "constitution": 0, "intelligence": 0, "willpower": 0 }, "newAbilities": ["rend"] }
              ]
            }
            """;
        registry.LoadFromJson("bonewarden", bwJson);
        registry.LoadFromJson("stillblade", sbJson);

        var gs = new GameState(seed: 42, classRegistry: registry);
        var party = new PartyState();
        party.SetMember(0, new CharacterState(
            new Guid("11111111-1111-1111-1111-111111111111"), "Bone", "bonewarden", 1, 0,
            new BaseStats(4, 3, 5, 4, 4), 20, Equipment.Empty,
            new[] { "bone_spear" }, 0));
        party.SetMember(1, new CharacterState(
            new Guid("22222222-2222-2222-2222-222222222222"), "Still", "stillblade", 1, 0,
            new BaseStats(5, 5, 4, 3, 4), 20, Equipment.Empty,
            new[] { "rend" }, 0));
        gs.Party = party;

        var encounter = new EncounterDef("test_enc", "Test", new[]
        {
            new EnemySpawn("rat", 2)
        });

        gs.TriggerEncounter(encounter);
        gs.ActionLog.Clear();

        int actionsSubmitted = 0;
        int maxActions = 6;
        while (gs.Mode == GameMode.Combat && actionsSubmitted < maxActions)
        {
            var currentActor = gs.Combat!.CurrentActor;
            if (currentActor is null || !currentActor.Value.IsPlayer) break;

            var target = gs.Combat.Combatants.First(c => !c.IsPlayer && c.IsAlive);
            var abilityId = actionsSubmitted % 2 == 0 ? "bone_spear" : "rend";
            var action = new CombatAction(currentActor.Value.Id, ActionType.UseAbility, target.Id, abilityId, null);
            gs.SubmitCombatAction(action);
            actionsSubmitted++;
        }

        Assert.DoesNotContain(gs.ActionLog, e => e.Type == "synergy_triggered");
    }
}
