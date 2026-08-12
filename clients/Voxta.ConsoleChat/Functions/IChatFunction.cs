using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat.Functions;

// A single function the character can invoke. Add a new one by implementing this and registering it.
public interface IChatFunction
{
    ScenarioActionDefinition Definition { get; }
    FunctionResult Execute(ServerActionMessage action, IFunctionInteraction interaction);
}
