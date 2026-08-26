using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Voxta.Abstractions.Chats.Sessions;
using Voxta.Abstractions.Services.Agents;
using Voxta.Abstractions.Services.ChatAugmentations;
using Voxta.Model.Shared;

namespace Voxta.Modules.Examples.ToolsExample.ChatAugmentations;

/// <summary>
/// The running chat augmentation.
/// </summary>
public sealed class ToolsExampleChatAugmentationInstance(
    IChatSessionChatAugmentationApi session,
    string notesFolder,
    ChatStyles[] chatStyles,
    bool rollDiceEnabled,
    bool writeNoteEnabled,
    IReadOnlySet<string> alwaysAllowed,
    IToolPermissionGrantWriter grantWriter,
    ILogger<ToolsExampleChatAugmentationInstance> logger
) : IToolsChatAugmentation
{
    private const string SidesArg = "sides";
    private const string CountArg = "count";
    private const string TextArg = "text";

    /// <summary>
    /// Other services this augmentation cannot work without.
    /// </summary>
    public ServiceTypes[] GetRequiredServiceTypes() => [];

    /// <summary>
    /// Where a "yes, always" is written down.
    /// </summary>
    public IToolPermissionGrantWriter GrantWriter => grantWriter;

    /// <summary>
    /// Tool definitions.
    /// </summary>
    public ValueTask<IReadOnlyList<ChatTool>> GetToolsAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<IReadOnlyList<ChatTool>>(
        [
            new ChatTool(
                ToolsExampleConstants.RollDiceToolName,
                "Roll one or more dice and report the results. Use it whenever chance should decide something.",
                ChatToolSchema.FromArguments([
                    new FunctionArgumentDefinition
                    {
                        Name = SidesArg,
                        Description = "How many sides each die has.",
                        Type = FunctionArgumentType.Integer,
                        Required = true,
                        // A closed set belongs in the schema, not in the description. A model asked in
                        // prose to pick one of six values picks a seventh often enough to matter, and it
                        // only finds out a round later.
                        Choices = ["4", "6", "8", "10", "12", "20", "100"],
                    },
                    new FunctionArgumentDefinition
                    {
                        Name = CountArg,
                        Description = "How many dice to roll. Defaults to one.",
                        Type = FunctionArgumentType.Integer,
                        Required = false,
                    },
                ]),
                ToolsExampleConstants.ContextKey
            )
            {
                AugmentationKey = ToolsExampleConstants.AugmentationKey,
                ChatStyles = chatStyles,
                Enabled = rollDiceEnabled,
                Permission = ToolPermissionClass.Read,
                // A roll is worth reading, so it gets the full card. A tool called constantly with
                // uninteresting results would ask for Pill or Minimal instead.
                Display = ToolDisplayMode.Card,
            },

            new ChatTool(
                ToolsExampleConstants.WriteNoteToolName,
                "Append a line to the user's notes file. Use it when the user asks for something to be written down.",
                ChatToolSchema.FromArguments([
                    new FunctionArgumentDefinition
                    {
                        Name = TextArg,
                        Description = "The line to append.",
                        Type = FunctionArgumentType.String,
                        Required = true,
                    },
                ]),
                ToolsExampleConstants.ContextKey
            )
            {
                AugmentationKey = ToolsExampleConstants.AugmentationKey,
                ChatStyles = chatStyles,
                Enabled = writeNoteEnabled,
                Permission = ToolPermissionClass.Act,
                // Skips the prompt.
                AlwaysAllowed = alwaysAllowed.Contains(ToolsExampleConstants.WriteNoteToolName),
            },
        ]);
    }

    /// <summary>
    /// Runs your tools.
    /// </summary>
    public async ValueTask<ChatToolCallResult> CallToolAsync(
        string name,
        JsonElement? arguments,
        CancellationToken cancellationToken
    ) => name switch
    {
        ToolsExampleConstants.RollDiceToolName => RollDice(arguments),
        ToolsExampleConstants.WriteNoteToolName => await WriteNoteAsync(arguments, cancellationToken),
        _ => new ChatToolCallResult($"There is no tool named '{name}' here.", IsError: true),
    };

    private ChatToolCallResult RollDice(JsonElement? arguments)
    {
        if (GetInt(arguments, SidesArg) is not { } sides)
            return new ChatToolCallResult($"Missing or non-numeric argument '{SidesArg}'.", IsError: true);
        if (sides < 2)
            return new ChatToolCallResult($"A die needs at least 2 sides, not {sides}.", IsError: true);

        var count = GetInt(arguments, CountArg) ?? 1;
        if (count is < 1 or > 100)
            return new ChatToolCallResult($"Roll between 1 and 100 dice, not {count}.", IsError: true);

        var rolls = new int[count];
        for (var i = 0; i < count; i++) rolls[i] = Random.Shared.Next(1, sides + 1);

        logger.LogInformation("Rolled {Count}d{Sides}", count, sides);

        // Written for the model to read back to the user, so it says what was rolled as well as the
        // total. A bare number would leave it guessing at what the dice were.
        return new ChatToolCallResult(
            count == 1
                ? $"Rolled 1d{sides}: {rolls[0]}."
                : $"Rolled {count}d{sides}: {string.Join(", ", rolls)}. Total: {rolls.Sum()}."
        );
    }

    private async ValueTask<ChatToolCallResult> WriteNoteAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        if (GetString(arguments, TextArg) is not { } text || string.IsNullOrWhiteSpace(text))
            return new ChatToolCallResult($"Missing required argument '{TextArg}'.", IsError: true);

        var folder = string.IsNullOrWhiteSpace(notesFolder)
            ? Path.Combine(AppContext.BaseDirectory, "Notes")
            : notesFolder;

        try
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "notes.txt");
            var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm}] {text.ReplaceLineEndings(" ").Trim()}{Environment.NewLine}";
            await File.AppendAllTextAsync(path, line, cancellationToken);

            logger.LogInformation("Appended a note to {Path}", path);
            return new ChatToolCallResult($"Written to {path}.");
        }
        // A real cancellation belongs to the caller and is rethrown. Everything else is the tool's own
        // failure, and the model is the one that needs to hear about it.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exc)
        {
            logger.LogWarning(exc, "Could not write the note to {Folder}", folder);
            return new ChatToolCallResult($"Could not write the note: {exc.Message}", IsError: true);
        }
    }

    /// <summary>
    /// Reads an integer the model may have written as a number or as a string. Both happen, and which
    /// one you get depends on the backend rather than on the schema, so a tool that only accepts one
    /// fails for half its users.
    /// </summary>
    private static int? GetInt(JsonElement? arguments, string name)
    {
        if (arguments is not { ValueKind: JsonValueKind.Object } args) return null;
        if (!args.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }

    private static string? GetString(JsonElement? arguments, string name)
    {
        if (arguments is not { ValueKind: JsonValueKind.Object } args) return null;
        if (!args.TryGetProperty(name, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
