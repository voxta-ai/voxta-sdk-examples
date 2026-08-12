using Voxta.Abstractions.Modules;
using Voxta.Abstractions.Registration;
using Voxta.Modules.Examples.TextToSpeechExample.Configuration;
using Voxta.Modules.Examples.TextToSpeechExample.TextToSpeech;

namespace Voxta.Modules.Examples.TextToSpeechExample;

/// <summary>
/// Entry point of the module. Voxta scans loaded assemblies for <see cref="IVoxtaModule"/>
/// implementations, instantiates them, and calls <see cref="Configure"/> once at startup.
/// </summary>
public class VoxtaModule : IVoxtaModule
{
    /// <summary>
    /// The unique name of your module. This identifies your module in the Voxta ecosystem,
    /// and is what user settings are saved against, so never change it once published.
    /// </summary>
    public const string ServiceName = "TextToSpeechExample";

    public void Configure(IVoxtaModuleBuilder builder)
    {
        builder.Register(new ModuleDefinition
        {
            ServiceName = ServiceName,
            Label = "Examples: Text to Speech",

            Supports = new ModuleSupports
            {
                // Note the dedicated TextToSpeechSupport type: each service type carries its own
                // extra data, so a voices provider cannot be attached to the wrong service.
                TextToSpeech = new TextToSpeechSupport(
                    ServiceDefinitionCategoryScore.Low,
                    // Do not steal text to speech from whatever the user already configured.
                    claim: ServiceDefaultAssignment.Never
                )
                {
                    // Lets users pick one of our "voices" on a character.
                    VoicesProviderType = typeof(BeepVoicesProvider),
                    // We stream raw PCM, so web clients can play it without a decoder.
                    SupportsPcm = true,
                },
            },

            // Shown in the module picker to help users choose.
            Pricing = ServiceDefinitionPricing.Free,
            Hosting = ServiceDefinitionHosting.Builtin,
            Notes = "Sample text to speech service. It generates beeps instead of speech.",
            Pros = "No model to download, no API key, instant.",
            Cons = "Nobody will understand a word.",
            HelpLink = "https://doc.voxta.ai/",
            SupportsExplicitContent = true,
            Experimental = true,
            Recommended = false,

            // The logo is an SVG embedded in this assembly (see the .csproj). The path is
            // relative to the type's namespace, with folder separators written as dots.
            Logo = ModuleLogo.EmbeddedResource(typeof(VoxtaModule), "Assets.tts-example.svg"),

            // User-facing settings for this module. Required, even if you have no settings to show.
            ModuleConfigurationProviderType = typeof(ModuleConfigurationProvider),
            ModuleConfigurationFieldsRequiringReload = ModuleConfigurationProvider.FieldsRequiringReload,
        });

        // Binds the implementation to the service type. One service instance is created per
        // user session, so services can hold per-session state.
        builder.AddTextToSpeechService<BeepTextToSpeechService>(ServiceName);
    }
}
