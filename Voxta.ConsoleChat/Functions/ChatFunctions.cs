using System.Diagnostics;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat.Functions;

public readonly record struct FunctionResult(string? EventMessage, string? SystemMessage, bool LeaveRequested)
{
    public static FunctionResult None => new(null, null, false);
    public static FunctionResult Event(string message) => new(message, null, false);
    public static FunctionResult Leave => new(null, null, true);
}

public interface IFunctionInteraction
{
    bool Confirm(string title, string message);
}

public sealed class DenyInteraction : IFunctionInteraction
{
    public static readonly DenyInteraction Instance = new();
    public bool Confirm(string title, string message) => false;
}

public class ChatFunctions
{
    private static readonly string OsName =
        OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : "Linux";

    private enum FunctionName
    {
        OpenWebBrowser,
        RunCommand,
        Exit,
    }

    public ScenarioActionDefinition[] Definitions { get; } =
    [
        new()
        {
            Name = nameof(FunctionName.OpenWebBrowser),
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
        },
        new()
        {
            Name = nameof(FunctionName.RunCommand),
            Timing = FunctionTiming.AfterUserMessage,
            CancelReply = true,
            Description = $"Run a command in the {OsName} command line when the user asks to run or execute something on their computer",
            Arguments =
            [
                new FunctionArgumentDefinition
                {
                    Name = "command",
                    Type = FunctionArgumentType.String,
                    Description = $"The {OsName} shell command to execute",
                    Required = true,
                },
            ],
        },
        new()
        {
            Name = nameof(FunctionName.Exit),
            Timing = FunctionTiming.AfterUserMessage,
            CancelReply = true,
            Description = "Exit the program whenever {{ user }} asks to",
            MatchFilter = ["exit", "quit", "leave", "bye"],
        },
    ];

    public FunctionResult Execute(ServerActionMessage action, IFunctionInteraction interaction)
    {
        if (!Enum.TryParse<FunctionName>(action.Value, out var function))
            return FunctionResult.Event($"I was unable to execute the function {action.Value} because it did not exist, better tell the user.");

        switch (function)
        {
            case FunctionName.OpenWebBrowser:
                if (!action.TryGetArgument("search_query", out var query))
                    return FunctionResult.Event($"I was unable to execute function {function} because the search query was not provided. I should ask for precisions.");
                OpenUrl("https://google.com/search?q=" + Uri.EscapeDataString(query));
                return FunctionResult.Event($"I opened a web browser with the search query \"{query}\". I have to tell the user!");

            case FunctionName.RunCommand:
                action.TryGetArgument("command", out var command);
                return RunCommand(command, interaction);

            case FunctionName.Exit:
                return FunctionResult.Leave;

            default:
                throw new NotSupportedException($"Function not supported: {function}");
        }
    }

    private static FunctionResult RunCommand(string? command, IFunctionInteraction interaction)
    {
        if (string.IsNullOrWhiteSpace(command))
            return FunctionResult.Event("I could not run the command because none was provided. I should ask the user what to run.");

        if (!interaction.Confirm("Run command", $"The assistant wants to run this {OsName} command:\n\n{command}\n\nAllow it to run?"))
            return FunctionResult.Event($"The user denied permission to run \"{command}\". I should respect that.");

        var output = Execute(command);
        return new FunctionResult(
            EventMessage: $"I ran the command \"{command}\". Its output was:\n{output}\nI should summarize it for the user.",
            SystemMessage: $"$ {command}\n{output}",
            LeaveRequested: false);
    }

    private static string Execute(string command)
    {
        var startInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = "/c " + command;
        }
        else
        {
            startInfo.FileName = "/bin/sh";
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(command);
        }

        try
        {
            using var process = Process.Start(startInfo)!;
            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(30_000);
            return string.IsNullOrWhiteSpace(output) ? "(no output)" : output.TrimEnd();
        }
        catch (Exception exception)
        {
            return $"(failed to run: {exception.Message})";
        }
    }

    private static void OpenUrl(string url)
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            return;
        }

        // Launch the platform opener directly and swallow its output so the
        // browser's startup noise (e.g. GTK warnings) does not corrupt the terminal UI.
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
