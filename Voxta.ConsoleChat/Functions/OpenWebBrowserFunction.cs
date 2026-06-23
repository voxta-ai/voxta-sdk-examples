using System.Diagnostics;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat.Functions;

public sealed class OpenWebBrowserFunction : IChatFunction
{
    public ScenarioActionDefinition Definition { get; } = new()
    {
        Name = "OpenWebBrowser",
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
                Required = true,
            },
        ],
    };

    public FunctionResult Execute(ServerActionMessage action, IFunctionInteraction interaction)
    {
        if (!action.TryGetArgument("search_query", out var query))
            return FunctionResult.Event("I could not open the browser because no search query was provided. I should ask for precisions.");

        OpenUrl("https://google.com/search?q=" + Uri.EscapeDataString(query));
        return FunctionResult.Event($"I opened a web browser with the search query \"{query}\". I have to tell the user!");
    }

    private static void OpenUrl(string url)
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            return;
        }

        // Launch the platform opener directly and swallow its output so the browser's startup
        // noise (e.g. GTK warnings) does not corrupt the terminal UI.
        Process.Start(new ProcessStartInfo
        {
            FileName = OperatingSystem.IsMacOS() ? "open" : "xdg-open",
            ArgumentList = { url },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
    }
}
