using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Security;
using Voxta.Model.Shared.Forms;

namespace Voxta.Modules.Examples.TextGenExample.Configuration;

/// <summary>
/// Builds the form shown on the module's Configure page. Fields are declared as static
/// readonly members so services can read them back in a type-safe way:
/// <c>ModuleConfiguration.GetRequired(ModuleConfigurationProvider.TokenDelayMs)</c>.
///
/// This form is about the module itself — where the backend is and how to talk to it. Settings
/// the user tunes per chat belong in a preset instead; see <see cref="ServiceSettingsProvider"/>.
/// </summary>
public class ModuleConfigurationProvider : ModuleConfigurationProviderBase, IModuleConfigurationProvider
{
    /// <summary>
    /// Fields that cannot be applied to a live session. Changing one of these restarts the
    /// module instead of updating it in place. Nothing here needs that.
    /// </summary>
    public static string[] FieldsRequiringReload => [];

    public static readonly FormIntField TokenDelayMs = new()
    {
        Name = "TokenDelayMs",
        Label = "Delay per token (ms)",
        Text = "Artificial delay between tokens, to imitate a streaming model.",
        DefaultValue = 30,
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
            FormTitleField.Create("Echo settings"),
            TokenDelayMs
        );
        return Task.FromResult(fields);
    }
}
