using Microsoft.Extensions.Logging;
using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Services;
using Voxta.Abstractions.Services.ChatAugmentations;
using Voxta.Modules.Examples.ChatAugmentationExample.Configuration;

namespace Voxta.Modules.Examples.ChatAugmentationExample.ChatAugmentations;

public class ChatAugmentationsService(
    ILogger<ChatAugmentationsService> logger,
    ILoggerFactory loggerFactory
    ) : ServiceBase(logger), IChatAugmentationsService
{
    /// <summary>
    /// Augmentations are declared in two steps.
    ///
    /// <see cref="PlanAsync"/> lists augmentations that will be enabled for a given chat. Keep this light,
    /// this runs before the chat is started to allow users to opt out of them.
    ///
    /// Then, <c>create</c> is invoked to instantiate the augmentation and attach it to the chat.
    /// </summary>
    public Task<AugmentationPlanItem[]> PlanAsync(
        IChatAugmentationPlanContext context,
        CancellationToken cancellationToken
    )
    {
        var plan = new List<AugmentationPlanItem>();

        if (ModuleConfiguration.GetRequired(ModuleConfigurationProvider.SampleActionInference))
        {
            plan.Add(VoxtaModule.Augmentations.Get(VoxtaModule.ExampleAugmentationsKey).Plan(
                AugmentationActivation.OptIn,
                session =>
                {
                    logger.LogInformation("Chat session {SessionId} has been augmented with SamplePlugin", session.SessionId);
                    return new SampleActionInferenceChatAugmentationInstance(
                        session,
                        loggerFactory.CreateLogger<SampleActionInferenceChatAugmentationInstance>()
                    );
                }
            ));
        }

        if (ModuleConfiguration.GetRequired(ModuleConfigurationProvider.SampleScriptEvent))
        {
            plan.Add(VoxtaModule.Augmentations.Get(VoxtaModule.ExampleAugmentationsKey).Plan(
                AugmentationActivation.OptIn,
                session =>
                {
                    logger.LogInformation("Chat session {SessionId} has been augmented with SamplePlugin Script Events", session.SessionId);
                    return new SampleScriptEventChatAugmentationInstance(
                        session,
                        loggerFactory.CreateLogger<SampleScriptEventChatAugmentationInstance>()
                    );
                }
            ));
        }

        if (ModuleConfiguration.GetRequired(ModuleConfigurationProvider.SamplePreprocessor))
        {
            plan.Add(VoxtaModule.Augmentations.Get(VoxtaModule.ExampleAugmentationsKey).Plan(
                AugmentationActivation.OptIn,
                session =>
                {
                    logger.LogInformation("Chat session {SessionId} has been augmented with SamplePlugin Preprocessor", session.SessionId);
                    return new SamplePreprocessorChatAugmentationInstance(
                        session,
                        loggerFactory.CreateLogger<SamplePreprocessorChatAugmentationInstance>()
                    );
                }
            ));
        }

        return Task.FromResult(plan.ToArray());
    }
}
