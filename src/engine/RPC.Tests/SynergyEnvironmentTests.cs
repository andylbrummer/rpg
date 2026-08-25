using System.Linq;
using System.Text.Json;
using RPC.Engine.Combat;
using RPC.Engine.Content;

namespace RPC.Tests;

public class SynergyEnvironmentTests
{
    [Fact]
    public void EnvironmentGatedSynergy_OnlyTriggersInMatchingEnvironment()
    {
        var reg = new SynergyRegistry();
        reg.Register("skirmish", "shiv", new SynergyEffect("bonus_damage", 6), "env_test", hidden: true, environment: "bloom_site");

        Assert.NotNull(reg.LookupWithId("skirmish", "shiv", "bloom_site"));        // matching env
        Assert.Null(reg.LookupWithId("skirmish", "shiv", "broken_engine"));        // wrong env
        Assert.Null(reg.LookupWithId("skirmish", "shiv", null));                   // no env
        Assert.Null(reg.LookupWithId("skirmish", "shiv"));                         // default (no env)
    }

    [Fact]
    public void UngatedSynergy_TriggersInAnyEnvironment()
    {
        var reg = new SynergyRegistry();
        reg.Register("a", "b", new SynergyEffect("bonus_damage", 5), "plain");

        Assert.NotNull(reg.LookupWithId("a", "b", "bloom_site"));
        Assert.NotNull(reg.LookupWithId("a", "b", null));
        Assert.NotNull(reg.LookupWithId("a", "b"));
    }

    [Fact]
    public void EnvironmentSynergyFiles_LoadWithEnvironmentGate()
    {
        // Confirm the authored environmental synergy content loads and stays gated.
        var dir = "../../../../../../content/synergies";
        var envFiles = Directory.EnumerateFiles(dir, "env_*.json").ToList();
        Assert.NotEmpty(envFiles);

        var reg = new SynergyRegistry();
        var anyGated = false;
        foreach (var f in envFiles)
        {
            var json = File.ReadAllText(f);
            var def = JsonSerializer.Deserialize<SynergyDef>(json, ContentJsonOptions.Standard);
            Assert.NotNull(def);
            Assert.False(string.IsNullOrEmpty(def!.Environment), $"{Path.GetFileName(f)} must set environment");
            reg.LoadFromJson(json);

            var a = def.Abilities[0];
            var b = def.Abilities[1];
            // Triggers in its environment, not in a foreign one.
            Assert.NotNull(reg.LookupWithId(a, b, def.Environment));
            Assert.Null(reg.LookupWithId(a, b, "nowhere_dungeon"));
            anyGated = true;
        }
        Assert.True(anyGated);
    }
}
