using System.Text.Json;
using System.Text.Json.Nodes;
using RPC.Engine.Combat;
using RPC.Engine.Commands;

namespace RPC.Tests;

public class ProtocolSchemaTests
{
    private readonly JsonNode _schema;

    public ProtocolSchemaTests()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "protocol-schema.json"));
        _schema = JsonNode.Parse(json)!;
    }

    [Fact]
    public void All_Dispatcher_Actions_Are_In_Schema()
    {
        var schemaActions = _schema["actions"]!.AsObject().Select(x => x.Key).ToHashSet();

        // Every action the dispatcher handles must be in the schema
        var dispatcherActions = new[]
        {
            "move_forward", "move_back", "strafe_left", "strafe_right",
            "turn_left", "turn_right", "cancel",
            "combat_action", "flee_combat", "enter_combat",
            "enter_dungeon", "rest", "return_to_town",
            "save_game", "reset_game", "swap_row",
            "tavern_recruit", "mission_accept", "vendor_purchase",
            "wildcard_alliance", "travel", "resolve_travel_encounter",
            "set_reputation", "complete_mission", "fail_mission", "abandon_mission",
            "dialogue_choice", "encounter_choice", "branch_choose", "accuse_faction",
            "transfer_to_cache", "transfer_from_cache",
            "downtime_action", "resurrect_character", "rumor_verify"
        };

        foreach (var action in dispatcherActions)
        {
            Assert.Contains(action, schemaActions);
        }
    }

    [Fact]
    public void All_Schema_Actions_Are_Handled_By_Dispatcher()
    {
        var schemaActions = _schema["actions"]!.AsObject().Select(x => x.Key).ToHashSet();

        // Every schema action must be parseable by the dispatcher
        foreach (var actionType in schemaActions)
        {
            var required = actionType switch
            {
                "combat_action" => new PlayerAction { Type = actionType, Action = new CombatAction(Guid.Empty, ActionType.UseAbility, Guid.Empty, "test", null) },
                "enter_dungeon" => new PlayerAction { Type = actionType, DungeonType = "test" },
                "swap_row" => new PlayerAction { Type = actionType, Slot = 0 },
                "set_reputation" => new PlayerAction { Type = actionType, TargetId = "bureau", Value = 0 },
                "dialogue_choice" => new PlayerAction { Type = actionType, TargetId = "bureau", Value = 0 },
                "branch_choose" => new PlayerAction { Type = actionType, TargetId = Guid.Empty.ToString(), Branch = "test" },
                "transfer_to_cache" or "transfer_from_cache" => new PlayerAction { Type = actionType, Slot = 0, TargetId = "item", Value = 1 },
                "downtime_action" => new PlayerAction { Type = actionType, TargetId = Guid.Empty.ToString(), DowntimeAction = "Rest" },
                "resurrect_character" => new PlayerAction { Type = actionType, TargetId = Guid.Empty.ToString() },
                "rumor_verify" => new PlayerAction { Type = actionType, TargetId = "test", Source = "InkbloodScribe" },
                _ => new PlayerAction { Type = actionType, TargetId = "test" }
            };

            var ex = Record.Exception(() => CommandDispatcher.Parse(required));
            Assert.Null(ex);
        }
    }

    [Fact]
    public void Schema_Does_Not_Contain_Unsupported_Actions()
    {
        var schemaActions = _schema["actions"]!.AsObject().Select(x => x.Key).ToHashSet();

        // Actions the server deliberately does not handle
        Assert.DoesNotContain("generate_dungeon", schemaActions);
    }

    [Fact]
    public void Protocol_Version_Matches_Envelope_Contract()
    {
        var version = _schema["protocolVersion"]!.GetValue<int>();
        Assert.Equal(2, version);
    }

    [Fact]
    public void Server_Envelope_Types_Include_Content_Reload()
    {
        var serverTypes = _schema["envelopeTypes"]!["serverToClient"]!.AsArray()
            .Select(x => x!.GetValue<string>()).ToHashSet();

        Assert.Contains("content.reload", serverTypes);
        Assert.Contains("hello", serverTypes);
        Assert.Contains("state", serverTypes);
        Assert.Contains("error", serverTypes);
        Assert.Contains("heartbeat.ping", serverTypes);
    }

    [Fact]
    public void Client_Envelope_Types_Include_Action()
    {
        var clientTypes = _schema["envelopeTypes"]!["clientToServer"]!.AsArray()
            .Select(x => x!.GetValue<string>()).ToHashSet();

        Assert.Contains("action", clientTypes);
        Assert.Contains("ready", clientTypes);
        Assert.Contains("heartbeat.pong", clientTypes);
    }

    [Fact]
    public void PartyMember_Schema_Includes_Branch_Fields()
    {
        var fields = _schema["stateShape"]!["partyMemberFields"]!.AsArray()
            .Select(x => x!.GetValue<string>()).ToHashSet();

        Assert.Contains("branchLevel6", fields);
        Assert.Contains("branchWarnings", fields);
        Assert.Contains("branchChoice", fields);
        Assert.Contains("awaitingBranchChoice", fields);
        Assert.Contains("availableBranches", fields);
    }
}
