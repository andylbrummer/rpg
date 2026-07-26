using RPC.Engine;
using RPC.Engine.Commands;

namespace RPC.Tests;

/// <summary>
/// Pins where a dispatched save actually lands.
///
/// A run's save file is named by <see cref="GameState.SavePath"/>. The ironman autosave writes
/// there and permadeath deletes it from there, but the manual save command used to resolve the
/// path itself and so always wrote the shared per-user default. Nothing in the shipped game sets
/// SavePath away from that default, so the two agreed by accident rather than by construction —
/// and a test that dispatched this command wrote to the developer's own save file.
/// </summary>
public class SaveCommandTests : IDisposable
{
    private readonly string _tempSavePath =
        Path.Combine(Path.GetTempPath(), $"test_save_{Guid.NewGuid()}.json");

    public void Dispose()
    {
        try { File.Delete(_tempSavePath); } catch { }
    }

    [Fact]
    public void Save_Command_Writes_To_The_Run_Save_Path()
    {
        var state = new GameState(seed: 42) { SavePath = _tempSavePath };
        var handler = new GameCommandHandler(state, new StubDungeonGenerator());

        var result = handler.Execute(new SaveGameCommand());

        Assert.True(result.StateChanged);
        Assert.True(File.Exists(_tempSavePath), "The save command did not write to the run's save path.");
    }
}
