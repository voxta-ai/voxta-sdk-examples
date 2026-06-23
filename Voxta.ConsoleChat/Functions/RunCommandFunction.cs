using System.Diagnostics;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat.Functions;

public sealed class RunCommandFunction : IChatFunction
{
    private static readonly string OsName =
        OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : "Linux";

    public ScenarioActionDefinition Definition { get; } = new()
    {
        Name = "RunCommand",
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
    };

    public FunctionResult Execute(ServerActionMessage action, IFunctionInteraction interaction)
    {
        if (!action.TryGetArgument("command", out var command) || string.IsNullOrWhiteSpace(command))
            return FunctionResult.Event("I could not run the command because none was provided. I should ask the user what to run.");

        if (!interaction.Confirm("Run command", $"The assistant wants to run this {OsName} command:\n\n{command}\n\nAllow it to run?"))
            return FunctionResult.Event($"The user denied permission to run \"{command}\". I should respect that.");

        var output = Run(command);
        return new FunctionResult(
            EventMessage: $"I ran the command \"{command}\". Its output was:\n{output}\nI should summarize it for the user.",
            SystemMessage: $"$ {command}\n{output}",
            LeaveRequested: false);
    }

    private static string Run(string command)
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
}
