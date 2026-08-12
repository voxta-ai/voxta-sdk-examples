using Voxta.Abstractions.Modules;
using Voxta.Abstractions.Registration;
using Voxta.Modules.Examples.ChatAugmentationExample.Configuration;
using Voxta.Modules.Examples.ChatAugmentationExample.CustomActionAugmentationExample;

namespace Voxta.Modules.Examples.ChatAugmentationExample;

public class VoxtaModule : IVoxtaModule
{
    /// <summary>
    /// The unique name of your module. This is used to identify your module
    /// internally in the Voxta ecosystem.
    /// </summary>
    public const string ServiceName = "ChatAugmentationExample";
    
    /// <summary>
    /// The augmentation key that can be configured in your characters.
    /// </summary>
    public const string ExampleAugmentationsKey = "my_augmentation";
    
    /// <summary>
    /// Configure dependency injection and module features
    /// </summary>
    public void Configure(IVoxtaModuleBuilder builder)
    {
        builder.Register(new()
        {
            ServiceName = ServiceName,
            Label = "Examples: Chat Augmentation Example",
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
            Notes = "This is a sample plugin that provides chat augmentations.",
            HelpLink = "https://doc.voxta.ai/",
            Augmentations = [ExampleAugmentationsKey],
            // The logo is an SVG embedded in this assembly (see the .csproj). The path is
            // relative to the type's namespace, with folder separators written as dots.
            Logo = ModuleLogo.EmbeddedResource(typeof(VoxtaModule), "Assets.chat-augmentation-example.svg"),
            ModuleConfigurationProviderType = typeof(ModuleConfigurationProvider),
            ModuleConfigurationFieldsRequiringReload = ModuleConfigurationProvider.FieldsRequiringReload,
        });
        
        builder.AddChatAugmentationsService<SamplePluginChatAugmentationsService>(ServiceName);
        
        // You can also use dependency injection:
        // e.g. builder.Services.AddSingleton<>();
    }
}