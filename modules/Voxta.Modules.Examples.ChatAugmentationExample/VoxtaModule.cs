using Voxta.Abstractions.Modules;
using Voxta.Abstractions.Registration;
using Voxta.Modules.Examples.ChatAugmentationExample.ChatAugmentations;
using Voxta.Modules.Examples.ChatAugmentationExample.Configuration;

namespace Voxta.Modules.Examples.ChatAugmentationExample;

public class VoxtaModule : IVoxtaModule
{
    /// <summary>
    /// The unique identifier of your module.
    /// </summary>
    public const string ServiceName = "ChatAugmentationExample";
    
    /// <summary>
    /// The chat augmentation key provided by this module. Can be specified in a character's augmentations.
    /// </summary>
    public const string ExampleAugmentationsKey = "sample_augmentation";

    /// <summary>
    /// Augmentations list for this module.
    /// </summary>
    public static readonly AugmentationKeyDescriptor[] Augmentations =
    [
        new(
            ExampleAugmentationsKey,
            "Chat Augmentation Examples",
            "Date and time lookup, script event logging, and message preprocessing."
        ),
    ];
    
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
            InstallableBy = ModuleInstallableBy.Anyone,
            Supports = new()
            {
                ChatAugmentations = new(ServiceDefinitionCategoryScore.Medium),
            },
            Pricing = ServiceDefinitionPricing.Free,
            Hosting = ServiceDefinitionHosting.Builtin,
            SupportsExplicitContent = true,
            Recommendation = ServiceDefinitionRecommendation.Neutral,
            Notes = "This is a sample plugin that provides chat augmentations.",
            HelpLink = "https://doc.voxta.ai/",
            Augmentations = Augmentations,
            Logo = ModuleLogo.EmbeddedResource(typeof(VoxtaModule), "Assets.chat-augmentation-example.svg"),
            ModuleConfigurationProviderType = typeof(ModuleConfigurationProvider),
            ModuleConfigurationFieldsRequiringReload = ModuleConfigurationProvider.FieldsRequiringReload,
        });
        
        builder.AddChatAugmentationsService<ChatAugmentationsService>(ServiceName);
        
        // You can also use dependency injection:
        // e.g. builder.Services.AddSingleton<>();
    }
}