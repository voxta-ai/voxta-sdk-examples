using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat.Functions;

public sealed class ExitFunction : IChatFunction
{
    public ScenarioActionDefinition Definition { get; } = new()
    {
        Name = "Exit",
        Timing = FunctionTiming.AfterUserMessage,
        CancelReply = true,
        Description = "Exit the program whenever {{ user }} asks to",
        MatchFilter = ["exit", "quit", "leave", "bye"],
    };

    public FunctionResult Execute(ServerActionMessage action, IFunctionInteraction interaction) => FunctionResult.Leave;
}
