using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Security;
using Voxta.Model.Shared.Forms;

namespace Voxta.Modules.Examples.TextToSpeechExample.Configuration;

/// <summary>
/// Builds the form shown on the module's Configure page. Fields are declared as static
/// readonly members so services can read them back in a type-safe way:
/// <c>ModuleConfiguration.GetRequired(ModuleConfigurationProvider.SyllableDurationMs)</c>.
/// </summary>
public class ModuleConfigurationProvider : ModuleConfigurationProviderBase, IModuleConfigurationProvider
{
    /// <summary>
    /// Fields that cannot be applied to a live session. Changing one of these restarts the
    /// module instead of updating it in place. Nothing here needs that.
    /// </summary>
    public static string[] FieldsRequiringReload => [];

    public static readonly FormIntField SyllableDurationMs = new()
    {
        Name = "SyllableDurationMs",
        Label = "Beep duration (ms)",
        Text = "How long each beep lasts. One beep is emitted per word.",
        DefaultValue = 140,
        Min = 20,
        Max = 1000,
    };

    public static readonly FormIntField GapDurationMs = new()
    {
        Name = "GapDurationMs",
        Label = "Gap duration (ms)",
        Text = "Silence inserted between beeps.",
        DefaultValue = 60,
        Min = 0,
        Max = 1000,
    };

    public Task<FormField[]> GetModuleConfigurationFieldsAsync(
        IAuthenticationContext auth,
        ISettingsSource settings,
        CancellationToken cancellationToken
    )
    {
        // FormBuilder validates the fields and assigns their display order.
        var fields = FormBuilder.Build(
            FormTitleField.Create("Beep settings"),
            SyllableDurationMs,
            GapDurationMs
        );
        return Task.FromResult(fields);
    }
}
