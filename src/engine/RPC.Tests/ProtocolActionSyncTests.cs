using System.Text.Json.Nodes;
using RPC.Engine.Commands;

namespace RPC.Tests;

/// <summary>
/// Drift-detection for the protocol action contract. Three artifacts describe
/// the set of player actions and can silently fall out of sync:
///   1. Server truth      — CommandDispatcher.KnownActions (the actions the server parses).
///   2. Test reference     — Fixtures/protocol-schema.json "actions" (asserted elsewhere).
///   3. Client union source — tools/protocol-gen/schema.json PlayerAction "oneOf" consts,
///                            from which src/client/.../protocol.gen.ts is generated.
/// These tests fail, naming the offending action(s), whenever any pair diverges.
/// Regenerate client types with: cd tools/protocol-gen &amp;&amp; node generate.js
/// </summary>
public class ProtocolActionSyncTests
{
    private static JsonNode LoadFixtureSchema()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "protocol-schema.json"));
        return JsonNode.Parse(json)!;
    }

    private static HashSet<string> FixtureActions() =>
        LoadFixtureSchema()["actions"]!.AsObject().Select(x => x.Key).ToHashSet();

    [Fact]
    public void Dispatcher_And_Fixture_Actions_Are_In_Sync()
    {
        var serverActions = CommandDispatcher.KnownActions.ToHashSet();
        var fixtureActions = FixtureActions();

        var serverOnly = serverActions.Except(fixtureActions).OrderBy(x => x).ToArray();
        var fixtureOnly = fixtureActions.Except(serverActions).OrderBy(x => x).ToArray();

        Assert.True(
            serverOnly.Length == 0 && fixtureOnly.Length == 0,
            $"Protocol action drift between CommandDispatcher and Fixtures/protocol-schema.json. " +
            $"Server-only (parsed but missing from schema fixture): [{string.Join(", ", serverOnly)}]. " +
            $"Fixture-only (in schema fixture but not parsed by server): [{string.Join(", ", fixtureOnly)}]. " +
            $"Add the action to the schema fixture or to CommandDispatcher so both agree.");
    }

    [Fact]
    public void Dispatcher_And_Client_Union_Source_Are_In_Sync()
    {
        var clientActions = ClientUnionActions();
        var serverActions = CommandDispatcher.KnownActions.ToHashSet();

        var serverOnly = serverActions.Except(clientActions).OrderBy(x => x).ToArray();
        var clientOnly = clientActions.Except(serverActions).OrderBy(x => x).ToArray();

        Assert.True(
            serverOnly.Length == 0 && clientOnly.Length == 0,
            $"Protocol action drift between CommandDispatcher and the client union source " +
            $"(tools/protocol-gen/schema.json PlayerAction). " +
            $"Server-only (parsed but not in client union): [{string.Join(", ", serverOnly)}]. " +
            $"Client-only (in client union but not parsed by server): [{string.Join(", ", clientOnly)}]. " +
            $"Update tools/protocol-gen/schema.json (then regenerate: cd tools/protocol-gen && node generate.js) " +
            $"or CommandDispatcher so both agree.");
    }

    /// <summary>
    /// Reads the "type" const of each PlayerAction variant in the client's JSON Schema
    /// source (tools/protocol-gen/schema.json) — the source from which the TypeScript
    /// PlayerAction union (protocol.gen.ts) is generated.
    /// </summary>
    private static HashSet<string> ClientUnionActions()
    {
        var schemaPath = LocateClientSchema();
        var schema = JsonNode.Parse(File.ReadAllText(schemaPath))!;
        var variants = schema["definitions"]!["PlayerAction"]!["oneOf"]!.AsArray();

        var actions = new HashSet<string>();
        foreach (var variant in variants)
        {
            var constValue = variant!["properties"]?["type"]?["const"]?.GetValue<string>();
            if (constValue is not null)
            {
                actions.Add(constValue);
            }
        }
        return actions;
    }

    /// <summary>
    /// Walks up from the test output directory to the repo root and returns the path
    /// to tools/protocol-gen/schema.json. Fails fast (no fallback) if not found.
    /// </summary>
    private static string LocateClientSchema()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "tools", "protocol-gen", "schema.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate tools/protocol-gen/schema.json by walking up from " +
            AppContext.BaseDirectory + ". The client union source is the source of truth for the " +
            "TypeScript PlayerAction union; this drift check requires it.");
    }
}
