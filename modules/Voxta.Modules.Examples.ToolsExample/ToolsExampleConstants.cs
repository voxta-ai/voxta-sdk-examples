using Voxta.Abstractions.Registration;

namespace Voxta.Modules.Examples.ToolsExample;

public static class ToolsExampleConstants
{
    public const string ServiceName = "ToolsExample";

    /// <summary>
    /// How will the tools be grouped in the UI
    /// </summary>
    public const string ContextKey = ServiceName;

    /// <summary>The names the model calls the tools by.</summary>
    public const string RollDiceToolName = "roll_dice";
    public const string WriteNoteToolName = "write_note";

    public const string AugmentationKey = "examples.tools";

    /// <summary>
    /// List of augmentations and their descriptions.
    /// </summary>
    public static readonly AugmentationKeyDescriptor[] Augmentations =
    [
        new(
            AugmentationKey,
            "Example Tools",
            "Lets the character roll dice and jot a note down, to show how tool calling works."
        ),
    ];
}
