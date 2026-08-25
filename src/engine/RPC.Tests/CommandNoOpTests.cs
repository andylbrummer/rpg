using RPC.Engine;
using RPC.Engine.Commands;

namespace RPC.Tests;

/// <summary>
/// Pins that a command which does nothing does not report that it changed the game.
///
/// The handler's StateChanged flag is not bookkeeping: a true broadcasts a fresh state snapshot to
/// every connected client and, in an ironman run, writes a save. Several commands called an engine
/// method that silently returns when the game is in the wrong mode, then reported true regardless
/// — so an action the game had refused still cost a broadcast and a save, and told the player
/// nothing about why nothing happened.
/// </summary>
public class CommandNoOpTests : IDisposable
{
    private readonly string _tempSavePath =
        Path.Combine(Path.GetTempPath(), $"test_save_{Guid.NewGuid()}.json");

    public void Dispose()
    {
        try { File.Delete(_tempSavePath); } catch { }
    }

    private (GameState State, GameCommandHandler Handler) NewGame()
    {
        var state = new GameState(seed: 42) { SavePath = _tempSavePath };
        return (state, new GameCommandHandler(state, new StubDungeonGenerator()));
    }

    [Fact]
    public void Resting_Outside_Town_Reports_No_Change()
    {
        var (state, handler) = NewGame();
        handler.Execute(new EnterDungeonCommand("test"));
        Assert.NotEqual(GameMode.Menu, state.Mode);

        var result = handler.Execute(new RestAtInnCommand());

        Assert.False(result.StateChanged);
    }

    [Fact]
    public void Resting_In_Town_Still_Reports_A_Change()
    {
        var (state, handler) = NewGame();
        Assert.Equal(GameMode.Menu, state.Mode);

        Assert.True(handler.Execute(new RestAtInnCommand()).StateChanged);
    }

    [Fact]
    public void Fleeing_Outside_Combat_Reports_No_Change()
    {
        var (_, handler) = NewGame();

        var result = handler.Execute(new FleeCombatCommand());

        Assert.False(result.StateChanged);
    }

    /// <summary>
    /// A flee that did not happen must not clear the last combat's result either — that result is
    /// what the player is still being shown.
    /// </summary>
    [Fact]
    public void Fleeing_Outside_Combat_Does_Not_Clear_The_Combat_Result()
    {
        var (_, handler) = NewGame();

        var result = handler.Execute(new FleeCombatCommand());

        Assert.False(result.ClearCombatResult);
    }

    /// <summary>
    /// Returning to town runs the town-arrival cycle, which spends a campaign turn. A client that
    /// sends the command twice — a double click, or a reconnect resending its last action — used
    /// to spend a second one for nothing.
    /// </summary>
    [Fact]
    public void Returning_To_Town_From_Town_Costs_No_Turn()
    {
        var (state, handler) = NewGame();
        handler.Execute(new EnterDungeonCommand("test"));
        handler.Execute(new ReturnToTownCommand());

        var turnsInTown = state.Overworld.Turns;
        var result = handler.Execute(new ReturnToTownCommand());

        Assert.False(result.StateChanged);
        Assert.Equal(turnsInTown, state.Overworld.Turns);
    }

    [Fact]
    public void Returning_To_Town_From_A_Dungeon_Still_Works()
    {
        var (state, handler) = NewGame();
        handler.Execute(new EnterDungeonCommand("test"));

        var result = handler.Execute(new ReturnToTownCommand());

        Assert.True(result.StateChanged);
        Assert.Equal(GameMode.Menu, state.Mode);
    }
}
