using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Campaign;
using RPC.Engine.Character;
using RPC.Engine.Commands;
using RPC.Engine.Content;
using RPC.Host.Web;

namespace RPC.Tests;

/// <summary>
/// Covers the state a client needs to offer ironman mode and the betrayal choice, both of which
/// were engine-only until an action string and a UI existed to reach them.
///
/// The betrayal flags are booleans rather than the mastermind's id on purpose. Who is behind the
/// scheme is the campaign's hidden information, and a client that could read it out of a state
/// frame would give the answer away regardless of what the interface chose to draw.
/// </summary>
public class BetrayalAndIronmanStateTests
{
    private readonly StatePresenter _presenter = new(new ClassRegistry(), new ItemRegistry());

    /// <summary>
    /// A run whose save file is its own. Turning ironman on through the command handler autosaves,
    /// and a state left on the default path writes the machine's real save file — these tests were
    /// overwriting whatever the developer last played, and the file they left behind then decided
    /// what other tests observed when they loaded "no save".
    /// </summary>
    private static GameState IsolatedRun(int seed = 42)
        => new(seed: seed) { SavePath = Path.Combine(Path.GetTempPath(), $"reach-ironman-{Guid.NewGuid():N}.json") };

    private JsonElement Present(GameState state)
    {
        var json = JsonSerializer.Serialize(_presenter.CreateStateMessage(state));
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static GameState WithMastermind(string mastermind = "inkblood")
    {
        var state = new GameState(seed: 42);
        state.Campaign.CampaignConfig = new CampaignConfig
        {
            Mastermind = mastermind,
            FactionTimelines = new Dictionary<string, FactionTimeline>(),
            NpcCasting = new Dictionary<string, string>()
        };
        return state;
    }

    [Fact]
    public void Betrayal_Is_Not_Offered_Without_Evidence_Against_The_Mastermind()
    {
        var evidence = Present(WithMastermind()).GetProperty("evidence");

        Assert.False(evidence.GetProperty("canBetray").GetBoolean());
        Assert.False(evidence.GetProperty("onBetrayalPath").GetBoolean());
    }

    [Fact]
    public void Betrayal_Is_Offered_Once_There_Is_Evidence_Against_The_Mastermind()
    {
        var state = WithMastermind();
        state.Evidence.AddEvidence("inkblood", "test", 1);

        Assert.True(Present(state).GetProperty("evidence").GetProperty("canBetray").GetBoolean());
    }

    /// <summary>
    /// Evidence against somebody else proves nothing about the party's actual quarry, and must not
    /// open the door.
    /// </summary>
    [Fact]
    public void Evidence_Against_Another_Faction_Does_Not_Offer_Betrayal()
    {
        var state = WithMastermind();
        state.Evidence.AddEvidence("bureau", "test", 9);

        Assert.False(Present(state).GetProperty("evidence").GetProperty("canBetray").GetBoolean());
    }

    [Fact]
    public void Committing_To_Betrayal_Closes_The_Offer_And_Reports_The_Path()
    {
        var state = WithMastermind();
        state.Evidence.AddEvidence("inkblood", "test", 1);
        var handler = new GameCommandHandler(state, new StubDungeonGenerator());

        Assert.True(handler.Execute(new ChooseBetrayalCommand()).StateChanged);

        var evidence = Present(state).GetProperty("evidence");
        Assert.False(evidence.GetProperty("canBetray").GetBoolean());
        Assert.True(evidence.GetProperty("onBetrayalPath").GetBoolean());
    }

    /// <summary>
    /// The betrayal fields carry whether the option exists, never who it points at. The client is
    /// separately told which faction the party's own gathered evidence suspects — that is the game
    /// reporting the player's findings back to them — but nothing about the option to change sides
    /// may name the mastermind, because the option appears before the campaign has revealed them.
    /// </summary>
    [Fact]
    public void The_Betrayal_Fields_Carry_No_Faction_Identity()
    {
        var state = WithMastermind("convocation");
        state.Evidence.AddEvidence("convocation", "test", 3);

        var evidence = Present(state).GetProperty("evidence");

        Assert.Equal(JsonValueKind.True, evidence.GetProperty("canBetray").ValueKind);
        Assert.Equal(JsonValueKind.False, evidence.GetProperty("onBetrayalPath").ValueKind);
    }

    [Fact]
    public void Ironman_Is_Reported_So_The_Settings_Toggle_Reflects_The_Run()
    {
        var state = IsolatedRun();
        Assert.False(Present(state).GetProperty("isIronman").GetBoolean());

        var handler = new GameCommandHandler(state, new StubDungeonGenerator());
        handler.Execute(new SetIronmanCommand(true));

        Assert.True(Present(state).GetProperty("isIronman").GetBoolean());
    }

    /// <summary>
    /// Ironman is a commitment for the length of a run. Everything that makes it mean something —
    /// the single save, its deletion on a wipe — is worth nothing if the player can step out of the
    /// mode before a hard fight and back into it afterwards.
    /// </summary>
    [Fact]
    public void Ironman_Cannot_Be_Turned_Off_Once_Taken()
    {
        var state = IsolatedRun();
        var handler = new GameCommandHandler(state, new StubDungeonGenerator());
        handler.Execute(new SetIronmanCommand(true));

        var result = handler.Execute(new SetIronmanCommand(false));

        Assert.False(result.StateChanged);
        Assert.True(state.IsIronman);
    }

    [Fact]
    public void Asking_For_Ironman_Twice_Changes_Nothing_The_Second_Time()
    {
        var state = IsolatedRun();
        var handler = new GameCommandHandler(state, new StubDungeonGenerator());
        Assert.True(handler.Execute(new SetIronmanCommand(true)).StateChanged);

        Assert.False(handler.Execute(new SetIronmanCommand(true)).StateChanged);
        Assert.True(state.IsIronman);
    }

    /// <summary>
    /// The one way out, and it costs the run. Without this a commitment made in one campaign would
    /// bind every campaign after it, with no way to undo it — the same shape as the aggregates that
    /// were surviving a reset before each one was made to clear itself.
    /// </summary>
    [Fact]
    public void A_New_Campaign_Is_Not_Bound_By_The_Previous_Runs_Commitment()
    {
        var state = IsolatedRun();
        var handler = new GameCommandHandler(state, new StubDungeonGenerator());
        handler.Execute(new SetIronmanCommand(true));

        handler.Execute(new ResetGameCommand());

        Assert.False(state.IsIronman);
        Assert.False(Present(state).GetProperty("isIronman").GetBoolean());
    }

    /// <summary>
    /// Restoring a save has to be able to set the flag either way — it reports what the run was,
    /// and is not the player asking for a change.
    /// </summary>
    [Fact]
    public void Loading_A_Save_Still_Restores_Either_Setting()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_save_{Guid.NewGuid()}.json");
        try
        {
            var ironmanRun = new GameState(seed: 42) { SavePath = path, IsIronman = true };
            RPC.Engine.Save.SaveSystem.Save(ironmanRun, path);

            var standardRun = new GameState(seed: 42) { SavePath = path };
            Assert.True(RPC.Engine.Save.SaveSystem.Load(standardRun, path));
            Assert.True(standardRun.IsIronman);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}
