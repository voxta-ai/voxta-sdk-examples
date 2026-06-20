using Voxta.Abstractions.Modules;
using Voxta.Abstractions.Registration;
using Voxta.Model.Shared;
using Voxta.Modules.Examples.ChatAugmentationExample.Configuration;
using Voxta.Modules.Examples.ChatAugmentationExample.CustomActionAugmentationExample;

namespace Voxta.Modules.Examples.ChatAugmentationExample;

public class VoxtaModule : IVoxtaModule
{
    public const string ServiceName = "SamplePlugin";
    
    public const string ExampleAugmentationsKey = "my_augmentation";
    
    /// <summary>
    /// Configure dependency injection and module features
    /// </summary>
    public void Configure(IVoxtaModuleBuilder builder)
    {
        builder.Register(new()
        {
            ServiceName = ServiceName,
            Label = "Sample Plugin",
            Experimental = true,
            CanBeInstalledByAdminsOnly = false,
            Supports = new()
            {
                { ServiceTypes.ChatAugmentations, ServiceDefinitionCategoryScore.Medium },
            },
            Pricing = ServiceDefinitionPricing.Free,
            Hosting = ServiceDefinitionHosting.Builtin,
            SupportsExplicitContent = true,
            Recommended = false,
            Notes = "This is a sample plugin.",
            HelpLink = "https://doc.voxta.ai/",
            Augmentations = [ExampleAugmentationsKey],
            ModuleConfigurationProviderType = typeof(ModuleConfigurationProvider),
            ModuleConfigurationFieldsRequiringReload = ModuleConfigurationProvider.FieldsRequiringReload,
        });
        
        builder.AddChatAugmentationsService<SamplePluginChatAugmentationsService>(ServiceName);
        
        // You can also use dependency injection:
        // e.g. builder.Services.AddSingleton<>();
    }
}