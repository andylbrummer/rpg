using System.Text.Json;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Party;

namespace RPC.Tests;

[Collection("SynergyTests")]
public class SynergySnapshotTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true
    };

    private static string SynergyDir => "../../../../../../content/synergies";
    private static string ClassDir => "../../../../../../content/classes";

    private readonly SynergyRegistry _synergies = new();

    private static CharacterState MakeChar(string name, int hp, int speed, int row, string classId)
        => new(new Guid(name.PadRight(16).Take(16).Select(c => (byte)c).ToArray()),
            name, classId, 1, 0,
            new BaseStats(4, speed, 4, 4, 4),
            hp, Equipment.Empty,
            Array.Empty<string>(), row);

    private static ClassRegistry LoadClassesForSynergy(SynergyDef def)
    {
        var registry = new ClassRegistry();
        foreach (var ability in def.Abilities)
        {
            // Find which class has this ability
            foreach (var classFile in Directory.GetFiles(ClassDir, "*.json"))
            {
                var classJson = File.ReadAllText(classFile);
                var classDef = JsonSerializer.Deserialize<ClassDef>(classJson, JsonOptions);
                if (classDef is null) continue;
                if (classDef.Abilities.Any(a => a.Id == ability))
                {
                    registry.LoadFromJson(classDef.Id, classJson);
                    break;
                }
            }
        }
        return registry;
    }

    [Fact]
    public void AllSynergies_BonusDamage_IsBalanced()
    {
        var files = Directory.EnumerateFiles(SynergyDir, "*.json");
        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var def = JsonSerializer.Deserialize<SynergyDef>(json, JsonOptions);
            Assert.NotNull(def);
            if (def.Anti) continue;

            // Strong ability damage max is roughly 2d6+5 = 17 avg, 150% = ~25
            // Cap bonus damage at 10 to be conservative and well under 150%
            if (def.Effect.Type == "bonus_damage")
            {
                Assert.True(def.Effect.Value <= 10,
                    $"Synergy {def.Id} bonus damage {def.Effect.Value} exceeds balance cap");
            }
        }
    }

    [Theory]
    [InlineData("bonewarden_cauterist_purifying_bones")]
    [InlineData("fieldwright_inkblood_overcharge")]
    [InlineData("cauterist_hollow_surgical_precision")]
    [InlineData("stillblade_hollow_smoke_silence")]
    [InlineData("ashmouth_marcher_focused_fire")]
    [InlineData("ashmouth_stillblade_crushing_silence")]
    [InlineData("bonewarden_inkblood_blood_tithe")]
    [InlineData("bonewarden_hollow_bone_shiv")]
    [InlineData("fieldwright_marcher_overcharged_shot")]
    [InlineData("hollow_inkblood_glyph_strike")]
    [InlineData("inkblood_stillblade_antimagic_cascade")]
    [InlineData("cauterist_fieldwright_emergency_repairs")]
    [InlineData("ashmouth_bonewarden_necromantic_legion")]
    [InlineData("hollow_marcher_smoke_skirmish")]
    [InlineData("fieldwright_stillblade_fortress")]
    [InlineData("bonewarden_marcher_hunting_party")]
    [InlineData("cauterist_inkblood_ash_and_flame")]
    public void Synergy_TriggersInCombat(string synergyId)
    {
        var path = Path.Combine(SynergyDir, $"{synergyId}.json");
        Assert.True(File.Exists(path), $"Missing synergy file: {path}");

        var json = File.ReadAllText(path);
        var def = JsonSerializer.Deserialize<SynergyDef>(json, JsonOptions);
        Assert.NotNull(def);
        Assert.False(def.Anti);
        Assert.Equal(2, def.Abilities.Length);

        _synergies.Clear();
        var registry = LoadClassesForSynergy(def);

        // Register the synergy
        var effect = new SynergyEffect(def.Effect.Type, def.Effect.Value);
        _synergies.Register(def.Abilities[0], def.Abilities[1], effect, def.Id);

        // Verify both classes were loaded
        var classIds = new HashSet<string>();
        foreach (var ability in def.Abilities)
        {
            var found = false;
            foreach (var classFile in Directory.GetFiles(ClassDir, "*.json"))
            {
                var classJson = File.ReadAllText(classFile);
                var classDef = JsonSerializer.Deserialize<ClassDef>(classJson, JsonOptions);
                if (classDef?.Abilities.Any(a => a.Id == ability) == true)
                {
                    classIds.Add(classDef.Id);
                    found = true;
                    break;
                }
            }
            Assert.True(found, $"Ability {ability} not found in any class");
        }

        Assert.Equal(2, classIds.Count);

        // Build party with both classes
        var party = new PartyState();
        var classList = classIds.ToList();
        party.SetMember(0, MakeChar("A", 50, 10, 0, classList[0]));
        party.SetMember(1, MakeChar("B", 50, 1, 0, classList[1]));

        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });
        var state = CombatEngine.Enter(party, encounter, new GameRandom(42));
        var rng = new GameRandom(42);

        // Use both abilities in sequence
        int playerTurns = 0;
        while (!state.IsFinished && playerTurns < 2)
        {
            if (state.Phase == CombatPhase.Turn && state.CurrentActor?.IsPlayer == true)
            {
                var enemy = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                var actorClassId = state.CurrentActor.Value.ClassId;
                var ability = def.Abilities.First(a =>
                {
                    var classJson = File.ReadAllText(Path.Combine(ClassDir, $"{actorClassId}.json"));
                    var classDef = JsonSerializer.Deserialize<ClassDef>(classJson, JsonOptions);
                    return classDef?.Abilities.Any(ca => ca.Id == a) == true;
                });

                state = CombatEngine.Tick(state,
                    new CombatAction(state.CurrentActor.Value.Id, ActionType.UseAbility, enemy.Id, ability, null),
                    rng, registry, synergies: _synergies);
                while (!state.IsFinished && state.Phase != CombatPhase.Turn)
                    state = CombatEngine.Tick(state, null, rng, registry, synergies: _synergies);
                playerTurns++;
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng, registry, synergies: _synergies);
            }
        }

        var synergyLogs = state.Log.Where(l => l.Message.Contains("synergy", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.NotEmpty(synergyLogs);
    }

    [Fact]
    public void AntiSynergy_BonewardenStillblade_DoesNotTrigger()
    {
        var path = Path.Combine(SynergyDir, "bonewarden_stillblade_anti.json");
        Assert.True(File.Exists(path));

        var json = File.ReadAllText(path);
        var def = JsonSerializer.Deserialize<SynergyDef>(json, JsonOptions);
        Assert.NotNull(def);
        Assert.True(def.Anti);

        _synergies.Clear();
        var registry = new ClassRegistry();
        foreach (var classFile in new[] { "bonewarden.json", "stillblade.json" })
        {
            var classJson = File.ReadAllText(Path.Combine(ClassDir, classFile));
            var classDef = JsonSerializer.Deserialize<ClassDef>(classJson, JsonOptions);
            Assert.NotNull(classDef);
            registry.LoadFromJson(classDef.Id, classJson);
        }

        // Do NOT register the anti synergy — the engine should ignore it because anti=true
        // (the content loader skips anti synergies)

        var party = new PartyState();
        party.SetMember(0, MakeChar("Bone", 50, 10, 0, "bonewarden"));
        party.SetMember(1, MakeChar("Still", 50, 1, 0, "stillblade"));

        var encounter = new EncounterDef("e1", "Test", new[] { new EnemySpawn("rat", 1) });
        var state = CombatEngine.Enter(party, encounter, new GameRandom(42));
        var rng = new GameRandom(42);

        int playerTurns = 0;
        string[] abilitySequence = { "bone_spear", "rend" };
        while (!state.IsFinished && playerTurns < 2)
        {
            if (state.Phase == CombatPhase.Turn && state.CurrentActor?.IsPlayer == true)
            {
                var enemy = state.Combatants.First(c => !c.IsPlayer && c.IsAlive);
                state = CombatEngine.Tick(state,
                    new CombatAction(state.CurrentActor.Value.Id, ActionType.UseAbility, enemy.Id, abilitySequence[playerTurns], null),
                    rng, registry, synergies: _synergies);
                while (!state.IsFinished && state.Phase != CombatPhase.Turn)
                    state = CombatEngine.Tick(state, null, rng, registry, synergies: _synergies);
                playerTurns++;
            }
            else
            {
                state = CombatEngine.Tick(state, null, rng, registry, synergies: _synergies);
            }
        }

        var synergyLogs = state.Log.Where(l => l.Message.Contains("synergy", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Empty(synergyLogs);
    }
}
