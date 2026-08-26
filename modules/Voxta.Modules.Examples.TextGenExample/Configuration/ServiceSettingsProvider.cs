using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Security;
using Voxta.Abstractions.Services.TextGen.SettingsFields;
using Voxta.Abstractions.TextGenerationStreaming;
using Voxta.Model.Shared;
using Voxta.Model.Shared.Forms;

namespace Voxta.Modules.Examples.TextGenExample.Configuration;

/// <summary>
/// Builds the preset / service settings page's form.
/// </summary>
public class ServiceSettingsProvider : IServiceSettingsProvider
{
    /// <summary>
    /// When these fields are changed, the module is reloaded (useful for in-process modules).
    /// </summary>
    public static string[] FieldsRequiringReload => [];

    public static readonly FormIntSliderField MaxNewTokens = new()
    {
        Name = "MaxNewTokens",
        Label = "Max reply length",
        Text = "Largest reply the service may produce, in tokens.",
        DefaultValue = 80,
        Min = 10,
        Max = 1000,
        Step = 10,
    };

    public static readonly FormIntSliderField MaxContextTokens = new()
    {
        Name = "MaxContextTokens",
        Label = "Context size",
        Text = "How much prompt the service accepts, in tokens. Voxta trims history to fit.",
        DefaultValue = 4096,
        Min = 512,
        Max = 32768,
        Step = 512,
    };

    public static readonly FormTextField ReplyPrefix = new()
    {
        Name = "ReplyPrefix",
        Label = "Reply prefix",
        Text = "Placed in front of the echoed message.",
        DefaultValue = "You said:",
    };

    public static readonly FormEnumField<TextProcessingOptions> TextProcessing = new()
    {
        Name = "TextProcessing",
        Label = "Text processing",
        Text = "How the generated text is interpreted before it is spoken.",
        DefaultValue = TextProcessingOptions.Roleplay,
        Choices =
        [
            new() { Value = TextProcessingOptions.None, Label = "None (asterisks as emphasis)" },
            new() { Value = TextProcessingOptions.Roleplay, Label = "Narration as asterisks" },
        ],
    };

    /// <summary>
    /// When the preset has no label, this is used as the default. It can be overridden by the user in the preset form.
    /// </summary>
    public string? GetDefaultLabel(ServiceTypes serviceType, StaticSettingsSource settings)
    {
        return settings.GetOptional(ReplyPrefix);
    }

    public Task<FormField[]> GetFormFieldsAsync(
        IAuthenticationContext auth,
        ServiceTypes serviceType,
        ISettingsSource moduleSettings,
        ISettingsSource serviceSettings,
        CancellationToken cancellationToken
    )
    {
        var fields = FormBuilder.Build(
            TextGenServiceSettingsFields.ModelTitle,
            ReplyPrefix,
            FormTitleField.Create("Generation"),
            MaxNewTokens,
            MaxContextTokens,
            // Enum fields are declared strongly typed and converted to a choices field here.
            TextProcessing.AsField()
        );
        return Task.FromResult(fields);
    }
}
