using System.Diagnostics;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat;

public enum VoxtaFunctionNames
{
    OpenWebBrowser,
    Exit,
}

public class VoxtaFunctions
{
    public readonly ScenarioActionDefinition[] FunctionsDefinitions =
    [
        new()
        {
            Name = nameof(VoxtaFunctionNames.OpenWebBrowser),
            Timing = FunctionTiming.AfterUserMessage,
            CancelReply = true,
            Description = "When the user asks to search for something online or shows curiosity about a topic",
            Arguments =
            [
                new FunctionArgumentDefinition
                {
                    Name = "search_query",
                    Type = FunctionArgumentType.String,
                    Description = "What to google for",
                    Required = true
                },
            ]
        },
        new()
        {
            Name = nameof(VoxtaFunctionNames.Exit),
            Timing = FunctionTiming.AfterUserMessage,
            CancelReply = true,
            Description = "Exit the program whenever {{ user }} asks to",
            MatchFilter = ["exit", "quit", "leave", "bye"],
        }
    ];

    public static string Execute(ServerActionMessage actionMessage)
    {
        if (!Enum.TryParse<VoxtaFunctionNames>(actionMessage.Value, out var fnName))
        {
            return $"I was unable to execute the function {fnName} because it did not exist, better tell the user.";
        }

        switch (fnName)
        {
            case VoxtaFunctionNames.OpenWebBrowser:
                var query = actionMessage.Arguments?.FirstOrDefault()?.Value;
                if (query == null)
                    return $"I was unable to execute function {fnName} because the search query was not provided. I should Ask for precisions.";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://google.com/search?q=" + Uri.EscapeDataString(query),
                    UseShellExecute = true,
                });
                Console.WriteLine($"Action: Opening browser with query '{query}'");
                return $"I opened a web browser with the search query \"{query}\". I have to tell the user!";
            case VoxtaFunctionNames.Exit:
                Console.WriteLine("ACTION: Exit");
                Environment.Exit(1);
                return null;
            default:
                throw new NotSupportedException($"Function not supported: {fnName}");
        }
    }
}