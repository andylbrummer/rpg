using System.Reflection;
using System.Text.Json;
using RPC.Engine;
using RPC.Engine.Character;
using RPC.Engine.Combat;
using RPC.Engine.Content;
using RPC.Host.Web;

namespace RPC.Tests;

public class StatePresenterTests
{
    private readonly StatePresenter _presenter;
    private readonly ClassRegistry _classRegistry;
    private readonly ItemRegistry _itemRegistry;

    public StatePresenterTests()
    {
        _classRegistry = new ClassRegistry();
        _itemRegistry = new ItemRegistry();
        _presenter = new StatePresenter(_classRegistry, _itemRegistry);
    }

    private static void SetCombat(GameState state, CombatState? combat)
    {
        typeof(GameState).GetProperty("Combat", BindingFlags.Instance | BindingFlags.Public)!.SetValue(state, combat);
        typeof(GameState).GetProperty("Mode", BindingFlags.Instance | BindingFlags.Public)!.SetValue(state, GameMode.Combat);
    }

    private static void SetLastCombatResult(GameState state, CombatResult? result)
    {
        typeof(GameState).GetProperty("LastCombatResult", BindingFlags.Instance | BindingFlags.Public)!.SetValue(state, result);
    }

    [Fact]
    public void CreateStateMessage_Includes_TopLevel_Type_And_Mode()
    {
        var state = new GameState();
        state.LoadGame();

        var msg = _presenter.CreateStateMessage(state);
        var json = JsonSerializer.Serialize(msg);
        var root = JsonSerializer.Deserialize<JsonElement>(json);

        Assert.Equal("state", root.GetProperty("type").GetString());
        Assert.Equal("Menu", root.GetProperty("mode").GetString());
    }

    [Fact]
    public void CreateStateMessage_Includes_Player_Position_And_Facing()
    {
        var state = new GameState();
        state.LoadGame();

        var msg = _presenter.CreateStateMessage(state);
        var json = JsonSerializer.Serialize(msg);
        var root = JsonSerializer.Deserialize<JsonElement>(json);
        var player = root.GetProperty("player");

        Assert.True(player.GetProperty("x").ValueKind == JsonValueKind.Number);
        Assert.True(player.GetProperty("y").ValueKind == JsonValueKind.Number);
        Assert.Equal("North", player.GetProperty("facing").GetString());
    }

    [Fact]
    public void CreateStateMessage_Includes_Party_As_Array()
    {
        var state = new GameState();
        state.LoadGame();

        var msg = _presenter.CreateStateMessage(state);
        var json = JsonSerializer.Serialize(msg);
        var root = JsonSerializer.Deserialize<JsonElement>(json);
        var party = root.GetProperty("party");

        Assert.Equal(JsonValueKind.Array, party.ValueKind);
    }

    [Fact]
    public void CreateStateMessage_Includes_Combat_When_In_Combat_Mode()
    {
        var state = new GameState();
        state.LoadGame();
        var combat = new CombatState(
            Array.Empty<Combatant>(),
            1, Array.Empty<Guid>(), 0,
            new List<CombatLogEntry>(),
            null, CombatPhase.RoundStart);
        SetCombat(state, combat);

        var msg = _presenter.CreateStateMessage(state);
        var json = JsonSerializer.Serialize(msg);
        var root = JsonSerializer.Deserialize<JsonElement>(json);

        Assert.Equal("Combat", root.GetProperty("mode").GetString());
        Assert.NotEqual(JsonValueKind.Null, root.GetProperty("combat").ValueKind);
    }

    [Fact]
    public void CreateStateMessage_Combat_Is_Null_When_Not_In_Combat()
    {
        var state = new GameState();
        state.LoadGame();
        typeof(GameState).GetProperty("Mode", BindingFlags.Instance | BindingFlags.Public)!.SetValue(state, GameMode.Menu);

        var msg = _presenter.CreateStateMessage(state);
        var json = JsonSerializer.Serialize(msg);
        var root = JsonSerializer.Deserialize<JsonElement>(json);

        Assert.Equal(JsonValueKind.Null, root.GetProperty("combat").ValueKind);
    }

    [Fact]
    public void CreateStateMessage_Includes_Town_State()
    {
        var state = new GameState();
        state.LoadGame();

        var msg = _presenter.CreateStateMessage(state);
        var json = JsonSerializer.Serialize(msg);
        var root = JsonSerializer.Deserialize<JsonElement>(json);
        var town = root.GetProperty("town");

        Assert.NotEqual(JsonValueKind.Null, town.ValueKind);
        Assert.True(town.TryGetProperty("currentTownId", out _));
        Assert.True(town.TryGetProperty("availableMissions", out _));
        Assert.True(town.TryGetProperty("vendorStock", out _));
        Assert.True(town.TryGetProperty("tavernRoster", out _));
    }

    [Fact]
    public void CreateStateMessage_Includes_Overworld_State()
    {
        var state = new GameState();
        state.LoadGame();

        var msg = _presenter.CreateStateMessage(state);
        var json = JsonSerializer.Serialize(msg);
        var root = JsonSerializer.Deserialize<JsonElement>(json);
        var overworld = root.GetProperty("overworld");

        Assert.NotEqual(JsonValueKind.Null, overworld.ValueKind);
        Assert.True(overworld.TryGetProperty("currentNodeId", out _));
        Assert.True(overworld.TryGetProperty("nodes", out _));
        Assert.True(overworld.TryGetProperty("routes", out _));
    }

    [Fact]
    public void CreateStateMessage_Includes_Reputation()
    {
        var state = new GameState();
        state.LoadGame();

        var msg = _presenter.CreateStateMessage(state);
        var json = JsonSerializer.Serialize(msg);
        var root = JsonSerializer.Deserialize<JsonElement>(json);

        Assert.Equal(JsonValueKind.Object, root.GetProperty("reputation").ValueKind);
    }

    [Fact]
    public void CreateStateMessage_Includes_Evidence()
    {
        var state = new GameState();
        state.LoadGame();

        var msg = _presenter.CreateStateMessage(state);
        var json = JsonSerializer.Serialize(msg);
        var root = JsonSerializer.Deserialize<JsonElement>(json);
        var evidence = root.GetProperty("evidence");

        Assert.NotEqual(JsonValueKind.Null, evidence.ValueKind);
        Assert.True(evidence.TryGetProperty("canConfront", out _));
        Assert.True(evidence.TryGetProperty("canAccuse", out _));
        Assert.True(evidence.TryGetProperty("hasIrrefutableProof", out _));
    }

    [Fact]
    public void CreateStateMessage_Includes_FactionStates()
    {
        var state = new GameState();
        state.LoadGame();

        var msg = _presenter.CreateStateMessage(state);
        var json = JsonSerializer.Serialize(msg);
        var root = JsonSerializer.Deserialize<JsonElement>(json);
        var factionStates = root.GetProperty("factionStates");

        Assert.Equal(JsonValueKind.Object, factionStates.ValueKind);
        Assert.True(factionStates.TryGetProperty("bureau", out _));
        Assert.True(factionStates.TryGetProperty("convocation", out _));
    }

    [Fact]
    public void CreateStateMessage_Includes_ActionLog()
    {
        var state = new GameState();
        state.LoadGame();

        var msg = _presenter.CreateStateMessage(state);
        var json = JsonSerializer.Serialize(msg);
        var root = JsonSerializer.Deserialize<JsonElement>(json);
        var actionLog = root.GetProperty("actionLog");

        Assert.Equal(JsonValueKind.Array, actionLog.ValueKind);
    }

    [Fact]
    public void CreateStateMessage_Includes_WorldState()
    {
        var state = new GameState();
        state.LoadGame();

        var msg = _presenter.CreateStateMessage(state);
        var json = JsonSerializer.Serialize(msg);
        var root = JsonSerializer.Deserialize<JsonElement>(json);
        var worldState = root.GetProperty("worldState");

        Assert.NotEqual(JsonValueKind.Null, worldState.ValueKind);
        Assert.True(worldState.TryGetProperty("settlements", out _));
        Assert.True(worldState.TryGetProperty("accessibleDungeons", out _));
    }

    [Fact]
    public void CreateStateMessage_Indicates_No_Dungeon_By_Default()
    {
        var state = new GameState();
        // Use a temp path to avoid loading an existing save file from other test runs
        state.LoadGame(Path.GetTempFileName() + ".notfound");

        var msg = _presenter.CreateStateMessage(state);
        var json = JsonSerializer.Serialize(msg);
        var root = JsonSerializer.Deserialize<JsonElement>(json);

        Assert.False(root.GetProperty("hasDungeon").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("dungeonType").ValueKind);
        Assert.Empty(root.GetProperty("tiles").EnumerateArray());
        Assert.Empty(root.GetProperty("explored").EnumerateArray());
    }

    [Fact]
    public void CreateStateMessage_Has_No_Side_Effects()
    {
        var state = new GameState();
        state.LoadGame();
        SetLastCombatResult(state, new CombatResult(true, 100, Array.Empty<string>(), 5));

        // Call presenter twice
        _presenter.CreateStateMessage(state);
        var msg2 = _presenter.CreateStateMessage(state);

        var json = JsonSerializer.Serialize(msg2);
        var root = JsonSerializer.Deserialize<JsonElement>(json);

        // combatResult should still be present because presenter is pure
        Assert.NotEqual(JsonValueKind.Null, root.GetProperty("combatResult").ValueKind);
    }
}
