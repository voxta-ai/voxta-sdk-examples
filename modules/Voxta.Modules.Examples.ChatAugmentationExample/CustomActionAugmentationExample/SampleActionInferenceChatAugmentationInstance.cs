using Microsoft.Extensions.Logging;
using Voxta.Abstractions.Chats.Sessions;
using Voxta.Abstractions.Model;
using Voxta.Abstractions.Services.ChatAugmentations;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.Modules.Examples.ChatAugmentationExample.CustomActionAugmentationExample;

public class SampleActionInferenceChatAugmentationInstance(
    IChatSessionChatAugmentationApi session,
    ILogger<SampleActionInferenceChatAugmentationInstance> logger
    ) : IActionInferenceAugmentation
{
    public ServiceTypes[] GetRequiredServiceTypes() => [ServiceTypes.ActionInference];
    public string[] GetAugmentationNames() => [VoxtaModule.ExampleAugmentationsKey];
    
    public IEnumerable<ClientUpdateContextMessage> RegisterChatContext()
    {
        return
        [
            new ClientUpdateContextMessage
            {
                ContextKey = VoxtaModule.ServiceName,
                SessionId = session.SessionId,
                Actions =
                [
                    new ScenarioActionDefinition
                    {
                        Name = "date_and_time",
                        ShortDescription = "print date and time",
                        Description = "when the date or time is requested",
                        MatchFilter = ["(date)|(time)"],
                        Timing = FunctionTiming.AfterUserMessage,
                        CancelReply = true,
                    }
                ]
            }
        ];
    }

    public async ValueTask<bool> TryHandleActionInference(
        ChatMessageData? message,
        ServerActionMessage serverActionMessage,
        CancellationToken cancellationToken
    )
    {
        if (serverActionMessage.ContextKey != VoxtaModule.ServiceName)
            return false;
        switch (serverActionMessage)
        {
            case { ContextKey: VoxtaModule.ServiceName, Value: "date_and_time" }:
                logger.LogInformation("Handling action inference 'date_and_time' for session {SessionId}", session.SessionId);
                await session.SendSecretAsync($"Current date and time: {TimeProvider.System.GetLocalNow().ToString()}", cancellationToken);
                await session.TriggerReplyAsync(cancellationToken);
                return true;
            default:
                return false;
        }
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}