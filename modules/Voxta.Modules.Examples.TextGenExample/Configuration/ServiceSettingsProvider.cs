using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Security;
using Voxta.Abstractions.Services.TextGen.SettingsFields;
using Voxta.Model.Shared;
using Voxta.Model.Shared.Forms;
using Voxta.Shared.LLMUtils.Clients;

namespace Voxta.Modules.Examples.TextGenExample.Configuration;

/// <summary>
/// Builds the preset ("service settings") form.
/// <para>
/// Most of it comes from <see cref="PromptingSettingsProvider"/>, and it has to: these are the same
/// fields <c>TextGenServiceBase</c> reads back to work out the token budget, the reply length and
/// the text processing options. Declaring your own <c>MaxNewTokens</c> would give the user a slider
/// the base class never looks at.
/// </para>
/// <para>
/// It is also why every text generation module in Voxta has the same settings page. A user who has
/// tuned one preset knows where everything is in the next one, and the wording and the warning
/// thresholds are written once rather than guessed at per module.
/// </para>
/// </summary>
public class ServiceSettingsProvider : IServiceSettingsProvider
{
    /// <summary>
    /// When these fields change, the module is reloaded. Useful for in-process modules whose client
    /// caches something built at startup.
    /// </summary>
    public static string[] FieldsRequiringReload => [];

    /// <summary>
    /// Fields of your own go alongside the shared ones. This is the place for anything specific to
    /// your backend — a model name, a sampler, an endpoint override.
    /// <para>
    /// Note the name. <c>PromptingSettingsProvider</c> already has a <c>ReplyPrefix</c>, and it
    /// means something else: text the model is forced to begin its reply with. Two fields called
    /// the same thing on one form is how a user ends up setting the wrong one.
    /// </para>
    /// </summary>
    public static readonly FormTextField EchoPrefix = new()
    {
        Name = "EchoPrefix",
        Label = "Echo prefix",
        Text = "Placed in front of the echoed message.",
        DefaultValue = "You said:",
    };

    /// <summary>
    /// Used as the preset name when the user has not given one. A real module usually returns the
    /// selected model, which is what tells two presets apart at a glance.
    /// </summary>
    public string? GetDefaultLabel(ServiceTypes serviceType, StaticSettingsSource settings)
    {
        return settings.GetOptional(EchoPrefix);
    }

    public Task<FormField[]> GetFormFieldsAsync(
        IAuthenticationContext auth,
        ServiceTypes serviceType,
        ISettingsSource moduleSettings,
        ISettingsSource serviceSettings,
        CancellationToken cancellationToken
    )
    {
        // FormBuilder validates the fields and assigns their display order. The group helpers each
        // return one field that expands into a titled section, and they know which sections do not
        // apply to a given service type — an Assistant preset has no reply length, for instance, so
        // GetGenerationLengthFields skips itself rather than making you branch here.
        var fields = FormBuilder.Build(
            TextGenServiceSettingsFields.ModelTitle,
            EchoPrefix,

            // Context window, memory ratio, subject models ratio.
            PromptingSettingsProvider.GetInputTokensFields(),

            // Max tokens, max sentences, multiple lines.
            PromptingSettingsProvider.GetGenerationLengthFields(serviceType),

            // Asterisk handling, prosody tags.
            PromptingSettingsProvider.GetTextProcessingFields(serviceType),

            // Thinking toggle, budget, reasoning effort. Only worth offering when the client
            // declares a ReasoningStyle other than None — this one does not, so it is left out:
            //   PromptingSettingsProvider.GetThinkingModelsFields(),
            // A completion backend that renders the prompt itself adds the template picker here
            // too, from PromptFormattingSettingsProvider.GetPromptFormattingTemplatesFields(). A
            // chat backend like this one has no prompt to format.

            // Formatting style, attachments, system prompt overrides, reply prefix.
            PromptingSettingsProvider.GetPromptingFields()
        );
        return Task.FromResult(fields);
    }
}
