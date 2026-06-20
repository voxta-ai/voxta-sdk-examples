using Microsoft.Extensions.Logging;
using Voxta.Abstractions.Chats.Sessions;
using Voxta.Abstractions.Security;
using Voxta.Abstractions.Services;
using Voxta.Abstractions.Services.ChatAugmentations;
using Voxta.Modules.Examples.ChatAugmentationExample.Configuration;

namespace Voxta.Modules.Examples.ChatAugmentationExample.CustomActionAugmentationExample;

public class SamplePluginChatAugmentationsService(
    ILogger<SamplePluginChatAugmentationsService> logger,
    ILoggerFactory loggerFactory
    ) : ServiceBase(logger), IChatAugmentationsService
{
    public async Task<IChatAugmentationServiceInstanceBase[]> CreateInstanceAsync(
        IChatSessionChatAugmentationApi session,
        IAuthenticationContext auth,
        CancellationToken cancellationToken
    )
    {
        await using var instances = new ChatAugmentationServiceInitializationHolder();
        instances.Add(CreateActionInferenceExample(session));
        instances.Add(CreateScriptEventExample(session));
        instances.Add(CreatePreprocessorExample(session));
        return instances.Acquire();
    }

    private SampleActionInferenceChatAugmentationInstance? CreateActionInferenceExample(IChatSessionChatAugmentationApi session)
    {
        if (!session.IsAugmentationEnabled(VoxtaModule.ExampleAugmentationsKey))
            return null;
        if (!ModuleConfiguration.GetRequired(ModuleConfigurationProvider.SampleActionInference))
            return null;
        logger.LogInformation("Chat session {SessionId} has been augmented with SamplePlugin", session.SessionId);
        return new SampleActionInferenceChatAugmentationInstance(
            session,
            loggerFactory.CreateLogger<SampleActionInferenceChatAugmentationInstance>()
        );
    }
    
    private SampleScriptEventChatAugmentationInstance? CreateScriptEventExample(IChatSessionChatAugmentationApi session)
    {
        if (!session.IsAugmentationEnabled(VoxtaModule.ExampleAugmentationsKey))
            return null;
        if (!ModuleConfiguration.GetRequired(ModuleConfigurationProvider.SampleScriptEvent))
            return null;
        logger.LogInformation("Chat session {SessionId} has been augmented with SamplePlugin Script Events", session.SessionId);
        return new SampleScriptEventChatAugmentationInstance(
            session,
            loggerFactory.CreateLogger<SampleScriptEventChatAugmentationInstance>()
        );
    }
    
    private SamplePreprocessorChatAugmentationInstance? CreatePreprocessorExample(IChatSessionChatAugmentationApi session)
    {
        if (!session.IsAugmentationEnabled(VoxtaModule.ExampleAugmentationsKey))
            return null;
        if (!ModuleConfiguration.GetRequired(ModuleConfigurationProvider.SamplePreprocessor))
            return null;
        logger.LogInformation("Chat session {SessionId} has been augmented with SamplePlugin Preprocessor", session.SessionId);
        return new SamplePreprocessorChatAugmentationInstance(
            session,
            loggerFactory.CreateLogger<SamplePreprocessorChatAugmentationInstance>()
        );
    }
}
