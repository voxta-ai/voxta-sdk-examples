using Voxta.Abstractions.Modules;
using Voxta.Abstractions.Registration;
using Voxta.Modules.Examples.ToolsExample.ChatAugmentations;
using Voxta.Modules.Examples.ToolsExample.Configuration;

namespace Voxta.Modules.Examples.ToolsExample;

/// <summary>
/// Entry point of the module. Voxta finds <see cref="IVoxtaModule"/> implementations in the module's
/// assembly, instantiates them, and calls <see cref="Configure"/> once when the module loads.
/// </summary>
public class VoxtaModule : IVoxtaModule
{
    public void Configure(IVoxtaModuleBuilder builder)
    {
        builder.Register(new()
        {
            ServiceName = ToolsExampleConstants.ServiceName,
            Label = "Examples: Tool Calling Example",
            Version = ModuleInfo.Version,
            Experimental = true,
            InstallableBy = ModuleInstallableBy.Anyone,
            Supports = new()
            {
                ChatAugmentations = new(ServiceDefinitionCategoryScore.Medium),
            },
            Pricing = ServiceDefinitionPricing.Free,
            Hosting = ServiceDefinitionHosting.Builtin,
            SupportsExplicitContent = true,
            Recommendation = ServiceDefinitionRecommendation.Neutral,
            Notes = "Two tools the AI can call during a reply: rolling dice, and appending a line to a notes file.",
            HelpLink = "https://doc.voxta.ai/",
            Pros = "Free, no API key, works with any model that can call tools",
            Cons = "Requires a model that supports tool calling",
            Augmentations = ToolsExampleConstants.Augmentations,
            // Instructions the model loads when it needs them: the markdown in Skills/, which the SDK embeds.
            Skills = ModuleSkills.EmbeddedResources(typeof(VoxtaModule)),
            ModuleConfigurationProviderType = typeof(ModuleConfigurationProvider),
            ModuleConfigurationFieldsRequiringReload = ModuleConfigurationProvider.FieldsRequiringReload,
        });

        builder.AddChatAugmentationsService<ChatAugmentationsService>(ToolsExampleConstants.ServiceName);
    }
}
