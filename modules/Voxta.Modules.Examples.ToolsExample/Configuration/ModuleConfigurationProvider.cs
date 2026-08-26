using System.Diagnostics.CodeAnalysis;
using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Security;
using Voxta.Abstractions.Services.Agents;
using Voxta.Model.Shared.Forms;

namespace Voxta.Modules.Examples.ToolsExample.Configuration;

/// <summary>
/// Builds the Configure page form. Fields can be read in your code, e.g.:
/// <c>ModuleConfiguration.GetRequired(ModuleConfigurationProvider.TokenDelayMs)</c>.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "Normalize fields visibility for consistency")]
public class ModuleConfigurationProvider : ModuleConfigurationProviderBase, IModuleConfigurationProvider
{
    public static string[] FieldsRequiringReload => [];

    /// <summary>
    /// Available tools
    /// </summary>
    public static readonly IReadOnlyList<ChatToolChoice> Tools =
    [
        new(ToolsExampleConstants.RollDiceToolName, "Roll Dice"),
        new(ToolsExampleConstants.WriteNoteToolName, "Write Note", EnabledByDefault: false),
    ];

    public static readonly FormTextField NotesFolder = new()
    {
        Name = "NotesFolder",
        Label = "Notes Folder",
        Text = "Where write_note appends, by default writes to `Data/Notes`.",
        DefaultValue = "Notes",
    };

    /// <summary>
    /// Where "yes, always" is remembered for this module's tools. It is an ordinary settings field, which
    /// is the point: the user can see what they granted and clear it, instead of a permission living
    /// somewhere only the code knows about. See <see cref="ChatAugmentations.ToolsExampleGrantWriter"/>.
    /// </summary>
    public static readonly FormTextField AlwaysAllow = new()
    {
        Name = "AlwaysAllow",
        Label = "Always Allow",
        Text = "Tools you have allowed for good, comma separated. Clear this to be asked again.",
        DefaultValue = "",
    };

    public Task<FormField[]> GetModuleConfigurationFieldsAsync(
        IAuthenticationContext auth,
        ISettingsSource settings,
        CancellationToken cancellationToken
    )
    {
        var fields = FormBuilder.Build(
            FormTitleField.Create(
                "Example Tools",
                text: $"Two tools the AI can call during a reply. Augmentation key: `{ToolsExampleConstants.AugmentationKey}`"
            ),
            ChatToolSettingsContributions.GetFormFields(Tools),
            NotesFolder,
            AlwaysAllow
        );
        return Task.FromResult(fields);
    }
}
