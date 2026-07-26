using System.Reflection;
using RPC.Engine;
using RPC.Engine.Commands;

namespace RPC.Tests;

/// <summary>
/// Pins the command dispatch surface as a whole.
///
/// <see cref="GameCommandHandler.Execute"/> is a switch over every command type, ending in a
/// default that throws. A command added to the protocol but not wired into that switch therefore
/// fails only at runtime, for the player, as an "internal error" — and nothing in the suite
/// notices, because the tests around commands exercise the game-state methods underneath rather
/// than the dispatch to them. Most of the switch had no coverage at all.
///
/// Enumerating the command types by reflection means this keeps holding for commands that do not
/// exist yet, which is the point: the failure it guards against is one of omission.
/// </summary>
public class CommandDispatchTests : IDisposable
{
    private readonly string _tempSavePath =
        Path.Combine(Path.GetTempPath(), $"test_save_{Guid.NewGuid()}.json");

    public void Dispose()
    {
        try { File.Delete(_tempSavePath); } catch { }
    }

    private static IEnumerable<Type> AllCommandTypes() =>
        typeof(ICommand).Assembly.GetTypes()
            .Where(t => typeof(ICommand).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .OrderBy(t => t.Name);

    [Fact]
    public void Every_Command_Type_Is_Handled_By_The_Dispatcher()
    {
        var commandTypes = AllCommandTypes().ToList();
        Assert.NotEmpty(commandTypes);

        var unhandled = new List<string>();

        foreach (var type in commandTypes)
        {
            // A fresh state per command: these are executed for their dispatch, not their outcome,
            // and one command's effects must not decide whether the next is reachable.
            var state = new GameState(seed: 42) { SavePath = _tempSavePath };
            var handler = new GameCommandHandler(state, new StubDungeonGenerator());
            var command = (ICommand)Construct(type);

            try
            {
                handler.Execute(command);
            }
            catch (ArgumentException ex) when (ex.Message.Contains("Unhandled command type"))
            {
                unhandled.Add(type.Name);
            }
            catch
            {
                // Any other failure means the command reached its case and the game refused it on
                // the merits — no dungeon to move in, no such mission, an index this state does not
                // have. That is dispatch working, which is all this test claims.
            }
        }

        Assert.Empty(unhandled);
    }

    /// <summary>
    /// Builds a command from its primary constructor with placeholder arguments. The values are
    /// deliberately meaningless: this test is about reaching the right case, and a command that is
    /// refused once it gets there has still proved the only thing being asserted.
    /// </summary>
    private static object Construct(Type type)
    {
        var ctor = type.GetConstructors()
            .OrderBy(c => c.GetParameters().Length)
            .First();

        var args = ctor.GetParameters().Select(p => PlaceholderFor(p.ParameterType)).ToArray();
        return ctor.Invoke(args);
    }

    private static object? PlaceholderFor(Type type)
    {
        if (type == typeof(string)) return "placeholder-id";
        if (Nullable.GetUnderlyingType(type) != null) return null;
        if (type == typeof(Guid)) return Guid.NewGuid();
        if (type.IsEnum) return Enum.GetValues(type).GetValue(0);
        if (type.IsValueType) return Activator.CreateInstance(type);
        return Construct(type);
    }
}
