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
        var state = new GameState(seed: 42);
        Assert.False(Present(state).GetProperty("isIronman").GetBoolean());

        var handler = new GameCommandHandler(state, new StubDungeonGenerator());
        handler.Execute(new SetIronmanCommand(true));

        Assert.True(Present(state).GetProperty("isIronman").GetBoolean());
    }
}
