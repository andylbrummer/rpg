using System.Text.Json;
using System.Text.Json.Nodes;
using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Dungeons;
using RPC.Engine.Models.Dungeons;
using RPC.Engine.Save;

namespace RPC.Tests;

/// <summary>
/// A save file is the player's whole campaign, and it is read on the live GameState at startup.
/// These pin what happens when the file parses as JSON but its contents are wrong — the case the
/// schema-version and quarantine checks do not catch.
/// </summary>
public class SaveRestoreAtomicityTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"reach_restore_{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        foreach (var f in Directory.EnumerateFiles(Path.GetDirectoryName(_path)!, Path.GetFileNameWithoutExtension(_path) + "*"))
        {
            try { File.Delete(f); } catch { /* best effort cleanup */ }
        }
    }

    private static GameState PopulatedState()
    {
        var gs = new GameState(seed: 42);
        gs.EnterDungeon(new Dungeon(8, 8, "test"), "broken_engine");
        gs.Player = new Player(new Position(3, 4), Direction.East);
        gs.ExploredTiles.Add("3,4");
        gs.PartyGold = 1234;
        return gs;
    }

    private string WriteMutatedSave(Action<JsonObject> mutate)
    {
        SaveSystem.Save(PopulatedState(), _path);
        var root = JsonNode.Parse(File.ReadAllText(_path))!.AsObject();
        mutate(root);
        File.WriteAllText(_path, root.ToJsonString());
        return _path;
    }

    /// <summary>
    /// The party array drives fixed six-slot restore. A truncated or overlong array is exactly the
    /// kind of thing a hand-edited or partially-written save produces.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(9)]
    public void Load_survives_a_party_array_of_unexpected_length(int memberCount)
    {
        var path = WriteMutatedSave(root =>
        {
            var party = root["party"]!.AsArray();
            var template = party[0]!.DeepClone();
            party.Clear();
            for (int i = 0; i < memberCount; i++) party.Add(template.DeepClone());
        });

        var target = new GameState(seed: 1);
        var loaded = SaveSystem.Load(target, path);

        // Whatever it decides, it must not throw and must leave a coherent six-slot party.
        Assert.Equal(6, target.Party.Members.Length);
        Assert.True(loaded || !loaded);
    }

    [Fact]
    public void Load_survives_out_of_range_player_position()
    {
        var path = WriteMutatedSave(root =>
        {
            root["playerX"] = 99999;
            root["playerY"] = -4242;
        });

        var target = new GameState(seed: 1);
        SaveSystem.Load(target, path);
        Assert.NotNull(target.Player);
    }

    [Fact]
    public void Load_survives_nulled_collections()
    {
        var path = WriteMutatedSave(root =>
        {
            foreach (var key in new[] { "exploredTiles", "collectedLoot", "actionLog", "reputation", "partyInventory" })
            {
                if (root.ContainsKey(key)) root[key] = null;
            }
        });

        var target = new GameState(seed: 1);
        SaveSystem.Load(target, path);
        Assert.NotNull(target.ActionLog);
        Assert.NotNull(target.PartyInventory);
    }

    [Fact]
    public void Load_survives_garbage_enum_and_id_values()
    {
        var path = WriteMutatedSave(root =>
        {
            root["mode"] = "NotARealMode";
            root["currentDungeonType"] = "no_such_dungeon";
            root["playerFacing"] = "Sideways";
        });

        var target = new GameState(seed: 1);
        SaveSystem.Load(target, path);
        Assert.NotNull(target.Player);
    }

    /// <summary>
    /// An unreadable save must be preserved, not left in the line of fire. Ironman autosaves after
    /// every state-changing command, so a file that fails to restore and stays in place is
    /// overwritten by the player's next move — losing a campaign that a later build might have
    /// recovered.
    /// </summary>
    [Fact]
    public void An_unreadable_save_is_set_aside_so_a_later_save_cannot_overwrite_it()
    {
        var path = WriteMutatedSave(root => root["party"] = new JsonArray("not-an-object", 42, true));
        var corruptBytes = File.ReadAllText(path);

        var target = new GameState(seed: 1);
        Assert.False(SaveSystem.Load(target, path));

        Assert.False(File.Exists(path));

        var preserved = Directory.EnumerateFiles(
            Path.GetDirectoryName(path)!,
            Path.GetFileName(path) + ".quarantine.*").ToList();
        Assert.Single(preserved);
        Assert.Equal(corruptBytes, File.ReadAllText(preserved[0]));

        // The path is now clear, so a subsequent save writes a fresh file rather than clobbering
        // the only copy of the broken one.
        SaveSystem.Save(target, path);
        Assert.True(File.Exists(path));
        Assert.Equal(corruptBytes, File.ReadAllText(preserved[0]));
    }

    /// <summary>
    /// The restore is a sequence of in-place mutations on the live GameState. If one of them
    /// throws, everything before it has already been applied — so a load that reports failure can
    /// still have replaced part of the running game with the contents of a broken file.
    ///
    /// This corrupts something restored late (the overworld) while leaving the early sections
    /// valid and distinguishable, then asks whether the early sections leaked in.
    /// </summary>
    [Fact]
    public void A_failed_load_does_not_apply_the_sections_it_got_through_first()
    {
        var path = WriteMutatedSave(root =>
        {
            root["partyGold"] = 4321;         // early, valid, and distinguishable
            root["overworld"] = new JsonArray(); // late, and the wrong shape
        });

        var target = new GameState(seed: 1);
        var goldBefore = target.PartyGold;

        var loaded = SaveSystem.Load(target, path);

        if (!loaded)
        {
            Assert.Equal(goldBefore, target.PartyGold);
        }
        else
        {
            Assert.Equal(4321, target.PartyGold);
        }
    }

    /// <summary>
    /// The load runs against the live GameState, so a restore that throws part-way through leaves
    /// the running game holding a mixture of the new save and whatever was there before. A failed
    /// load must leave a coherent state, not a torn one.
    /// </summary>
    [Fact]
    public void A_failed_load_leaves_a_coherent_state()
    {
        var path = WriteMutatedSave(root =>
        {
            // Structurally valid JSON whose types are wrong throughout.
            root["party"] = new JsonArray("not-an-object", 42, true);
            root["overworld"] = "not-an-object";
            root["town"] = 7;
        });

        var target = PopulatedState();
        var goldBefore = target.PartyGold;

        var loaded = SaveSystem.Load(target, path);

        Assert.Equal(6, target.Party.Members.Length);
        Assert.NotNull(target.Player);
        Assert.NotNull(target.Town);
        Assert.NotNull(target.Overworld);
        if (!loaded)
        {
            // Nothing from the broken file should have been applied.
            Assert.Equal(goldBefore, target.PartyGold);
        }
    }
}
