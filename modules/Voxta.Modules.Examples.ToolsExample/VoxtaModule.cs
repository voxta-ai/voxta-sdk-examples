using Voxta.Abstractions.Modules;
using Voxta.Abstractions.Registration;
using Voxta.Modules.Examples.ToolsExample.ChatAugmentations;
using Voxta.Modules.Examples.ToolsExample.Configuration;

namespace Voxta.Modules.Examples.ToolsExample;

/// <summary>
/// Entry point of the module. Voxta scans loaded assemblies for <see cref="IVoxtaModule"/>
/// implementations, instantiates them, and calls <see cref="Configure"/> once at startup.
/// </summary>
public class VoxtaModule : IVoxtaModule
{
    public void Configure(IVoxtaModuleBuilder builder)
    {
        builder.Register(new()
        {
            ServiceName = ToolsExampleConstants.ServiceName,
            Label = "Examples: Tool Calling Example",
            Experimental = true,
            CanBeInstalledByAdminsOnly = false,
            Supports = new()
            {
                ChatAugmentations = new(ServiceDefinitionCategoryScore.Medium),
            },
            Pricing = ServiceDefinitionPricing.Free,
            Hosting = ServiceDefinitionHosting.Builtin,
            SupportsExplicitContent = true,
            Recommended = false,
            Notes = "Two tools the AI can call during a reply: rolling dice, and appending a line to a notes file.",
            HelpLink = "https://doc.voxta.ai/",
            Pros = "Free, no API key, works with any model that can call tools",
            Cons = "Requires a model that supports tool calling",
            Augmentations = ToolsExampleConstants.Augmentations,
            ModuleConfigurationProviderType = typeof(ModuleConfigurationProvider),
            ModuleConfigurationFieldsRequiringReload = ModuleConfigurationProvider.FieldsRequiringReload,
        });

        builder.AddChatAugmentationsService<ChatAugmentationsService>(ToolsExampleConstants.ServiceName);
    }
}
