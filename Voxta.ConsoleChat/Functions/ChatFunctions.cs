using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat.Functions;

// Registry of the available functions: exposes their definitions and dispatches an action to the right one.
public sealed class ChatFunctions
{
    private readonly IReadOnlyDictionary<string, IChatFunction> _byName;

    public ChatFunctions(IEnumerable<IChatFunction> functions)
    {
        var all = functions.ToArray();
        _byName = all.ToDictionary(f => f.Definition.Name);
        Definitions = all.Select(f => f.Definition).ToArray();
    }

    public ScenarioActionDefinition[] Definitions { get; }

    public FunctionResult Execute(ServerActionMessage action, IFunctionInteraction interaction) =>
        _byName.TryGetValue(action.Value, out var function)
            ? function.Execute(action, interaction)
            : FunctionResult.Event($"I was unable to execute the function {action.Value} because it did not exist, better tell the user.");
}
