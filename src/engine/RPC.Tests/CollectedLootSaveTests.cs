using System;
using System.IO;
using RPC.Engine;
using RPC.Engine.Save;
using Xunit;

namespace RPC.Tests;

public class CollectedLootSaveTests
{
    [Fact]
    public void CollectedLoot_survives_save_and_restore()
    {
        var state = new GameState(seed: 42);
        state.Exploration.CollectedLoot.Add("5,7");

        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "save.json");
            SaveSystem.Save(state, path);

            var restored = new GameState(seed: 42);
            Assert.True(SaveSystem.Load(restored, path));

            Assert.Contains("5,7", restored.Exploration.CollectedLoot);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
