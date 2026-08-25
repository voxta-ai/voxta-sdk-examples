using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Security;
using Voxta.Abstractions.Services.TextGen.SettingsFields;
using Voxta.Abstractions.TextGenerationStreaming;
using Voxta.Model.Shared;
using Voxta.Model.Shared.Forms;

namespace Voxta.Modules.Examples.TextGenExample.Configuration;

/// <summary>
/// Builds the form for a <em>preset</em>, which is where generation settings live.
///
/// The split between this and <see cref="ModuleConfigurationProvider"/> matters:
/// <list type="bullet">
/// <item>Module configuration answers "how do I reach this backend" — endpoint, API key, which
/// engine to run. There is one set of those per installed module.</item>
/// <item>A preset answers "how should it generate" — model, reply length, sampling. A user can
/// have several and switch between them per chat, so anything worth tuning belongs here.</item>
/// </list>
/// Presets are per service type: the same provider is asked for the fields of every service type
/// the module supports, and <paramref name="serviceType"/> tells it which one. That is also why
/// values can be stored under a <c>TextGen:Name</c> key — a setting can be given a different value
/// for text gen than for summarization. Reading a field resolves the prefixed key first and falls
/// back to the bare one, so services never have to care which form was saved.
/// </summary>
public class ServiceSettingsProvider : IServiceSettingsProvider
{
    /// <summary>
    /// Preset fields that cannot be applied to a live session. Changing one of these restarts the
    /// module instead of updating it in place — a field that decides how a model is loaded belongs
    /// here. Nothing in this module does.
    /// </summary>
    public static string[] FieldsRequiringReload => [];

    /// <summary>
    /// "MaxNewTokens" and "MaxContextTokens" are the names the built-in modules use. Keeping them
    /// means the parts of Voxta that read a token budget generically — the module test page, the
    /// OpenAI-compatible endpoint — find yours too.
    /// </summary>
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

    /// <summary>
    /// Turns on the tool call demonstration in <see cref="TextGen.EchoTextGenService"/>. A real
    /// module has no such setting: it calls a tool when its backend asks for one.
    /// </summary>
    public static readonly FormBooleanField SimulateToolCall = new()
    {
        Name = "SimulateToolCall",
        Label = "Simulate a tool call",
        Text = "Ask permission and run a fake \"what time is it\" tool before echoing.",
        DefaultValue = false,
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
    /// Name given to a preset the user has not named. Real modules return the model name, which is
    /// what distinguishes one preset from another; the reply prefix plays that role here.
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
        // Titles group the form into sections. TextGenServiceSettingsFields also carries ready-made
        // fields for the standard sampling parameters, e.g. TextGenServiceSettingsFields
        // .Temperature("Temperature") — use those rather than redefining them, so your module's
        // settings read like every other one. Nothing here samples anything, so none are included.
        var fields = FormBuilder.Build(
            TextGenServiceSettingsFields.ModelTitle,
            ReplyPrefix,
            FormTitleField.Create("Generation"),
            MaxNewTokens,
            MaxContextTokens,
            // Enum fields are declared strongly typed and converted to a choices field here.
            TextProcessing.AsField(),
            FormTitleField.Create("Tools"),
            SimulateToolCall
        );
        return Task.FromResult(fields);
    }
}
