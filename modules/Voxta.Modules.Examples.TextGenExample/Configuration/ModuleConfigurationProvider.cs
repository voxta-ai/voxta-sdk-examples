using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Security;
using Voxta.Model.Shared.Forms;

namespace Voxta.Modules.Examples.TextGenExample.Configuration;

/// <summary>
/// Builds the Configure page form. Fields can be read in your code, e.g.:
/// <c>ModuleConfiguration.GetRequired(ModuleConfigurationProvider.TokenDelayMs)</c>.
/// </summary>
public class ModuleConfigurationProvider : ModuleConfigurationProviderBase, IModuleConfigurationProvider
{
    /// <summary>
    /// When these fields are changed, the module is reloaded (useful for in-process modules).
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
