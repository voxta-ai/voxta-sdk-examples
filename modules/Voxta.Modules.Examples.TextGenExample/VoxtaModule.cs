using Microsoft.Extensions.DependencyInjection;
using Voxta.Abstractions.Modules;
using Voxta.Abstractions.Registration;
using Voxta.Modules.Examples.TextGenExample.Clients;
using Voxta.Modules.Examples.TextGenExample.Configuration;
using Voxta.Modules.Examples.TextGenExample.TextGen;

namespace Voxta.Modules.Examples.TextGenExample;

/// <summary>
/// Entry point of the module. Voxta finds <see cref="IVoxtaModule"/> implementations in the module's
/// assembly, instantiates them, and calls <see cref="Configure"/> once when the module loads.
/// </summary>
public class VoxtaModule : IVoxtaModule
{
    /// <summary>
    /// The unique name of your module. This identifies your module in the Voxta ecosystem,
    /// and is what user settings are saved against, so never change it once published.
    /// </summary>
    public const string ServiceName = "TextGenExample";

    public void Configure(IVoxtaModuleBuilder builder)
    {
        builder.Register(new ModuleDefinition
        {
            ServiceName = ServiceName,
            Label = "Examples: Text Generation",
            Version = ModuleInfo.Version,

            // Which service types this module offers. A module can offer several; this one only
            // does text generation. The score is a hint for the module picker, not a limit.
            Supports = new ModuleSupports
            {
                TextGen = new ServiceSupport(
                    ServiceDefinitionCategoryScore.Low,
                    // Do not steal text gen from whatever the user already configured.
                    claim: ServiceDefaultAssignment.Never
                ),
            },

            // Shown in the module picker to help users choose.
            Pricing = ServiceDefinitionPricing.Free,
            Hosting = ServiceDefinitionHosting.Builtin,
            Notes = "Sample text generation service. It echoes the last user message instead of running a model.",
            Pros = "No model to download, no API key, fully deterministic.",
            Cons = "Not actually intelligent.",
            HelpLink = "https://doc.voxta.ai/",
            SupportsExplicitContent = true,
            Experimental = true,
            Recommendation = ServiceDefinitionRecommendation.Neutral,

            // The logo is an SVG embedded in this assembly (see the .csproj). The path is
            // relative to the type's namespace, with folder separators written as dots.
            Logo = ModuleLogo.EmbeddedResource(typeof(VoxtaModule), "Assets.textgen-example.svg"),

            // How to reach the backend. Required, even if you have no settings to show.
            ModuleConfigurationProviderType = typeof(ModuleConfigurationProvider),
            ModuleConfigurationFieldsRequiringReload = ModuleConfigurationProvider.FieldsRequiringReload,

            // How it should generate. Optional, but any module with settings worth tuning per
            // chat should offer presets rather than putting everything on the Configure page.
            PresetsProviderType = typeof(ServiceSettingsProvider),
            PresetsFieldsRequiringReload = ServiceSettingsProvider.FieldsRequiringReload,

            // Runs when the module is added, to write the preset it ships with.
            ModuleInstallationProviderType = typeof(ServiceInstallationProvider),

            // Backs the Test button, so a user can check their settings without starting a chat.
            ModuleTestingProviderType = typeof(ModuleTestingProvider),
        });

        // Binds the implementation to the service type. One service instance is created per
        // user session, so services can hold per-session state.
        builder.AddTextGenService<EchoTextGenService>(ServiceName);

        // The client the service is built on. Transient rather than singleton: it is initialized
        // with one user's settings, and a second session must not inherit them.
        builder.Services.AddTransient<EchoClient>();

        // Anything else your services need can be registered here:
        // builder.Services.AddSingleton<MyHelper>();
    }
}
