using Microsoft.Extensions.DependencyInjection;
using Voxta.Abstractions.Modules;
using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Services.ChatBridges;
using Voxta.Abstractions.Services.Launchers;
using Voxta.Modules.Examples.GameExample.Configuration;
using Voxta.Modules.Examples.GameExample.Game;

namespace Voxta.Modules.Examples.GameExample;

public class VoxtaModule : IVoxtaModule
{
    public const string ServiceName = "GameExample";

    public void Configure(IVoxtaModuleBuilder builder)
    {
        builder.Register(new()
        {
            ServiceName = ServiceName,
            Label = "Examples: Game Integration",
            Version = ModuleInfo.Version,
            Flags = [ModuleFlag.Experimental, ModuleFlag.SupportsExplicitContent],
            // The game's conversations are chats nobody types into, so they show on the Chat Bridges page; the game
            // itself is in the Play library.
            Supports = new()
            {
                ChatBridge = new(ServiceDefinitionCategoryScore.Low),
                Launcher = new(ServiceDefinitionCategoryScore.Low),
            },
            Pricing = ServiceDefinitionPricing.Free,
            Hosting = ServiceDefinitionHosting.Builtin,
            Notes = $"The game mod connects to Voxta's port, /api/extensions/{GameBridgeServer.RoutePrefix}/, with an API key.",
            ModuleConfigurationProviderType = typeof(ModuleConfigurationProvider),
            // Mira, the module's character, in the account's library on install and on every update.
            ModuleInstallationProviderType = typeof(ServiceInstallationProvider),
        });

        // The game is the client: its requests come through Voxta's own port, whether or not a conversation is open.
        builder.Services.AddSingleton<GameBridgeServer>();
        builder.Contribute<IModuleHttpEndpoints>(sp => sp.GetRequiredService<GameBridgeServer>());
        builder.Contribute<IChatBridge>(sp => sp.GetRequiredService<GameBridgeServer>());
        builder.Services.AddSingleton<GameLauncher>();
        builder.Contribute<ILauncher>(sp => sp.GetRequiredService<GameLauncher>());
    }
}
