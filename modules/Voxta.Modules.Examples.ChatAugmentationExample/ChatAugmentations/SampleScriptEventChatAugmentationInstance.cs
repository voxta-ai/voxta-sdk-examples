using Microsoft.Extensions.Logging;
using Voxta.Abstractions.Chats.Objects.Characters;
using Voxta.Abstractions.Chats.Sessions;
using Voxta.Abstractions.Model;
using Voxta.Abstractions.Scripting.ActionScripts;
using Voxta.Abstractions.Services.ChatAugmentations;
using Voxta.Model.Shared;

namespace Voxta.Modules.Examples.ChatAugmentationExample.ChatAugmentations;

public class SampleScriptEventChatAugmentationInstance(
    IChatSessionChatAugmentationApi session,
    ILogger<SampleScriptEventChatAugmentationInstance> logger
    ) : IChatScriptEventsAugmentation
{
    public ServiceTypes[] GetRequiredServiceTypes() => [ServiceTypes.ActionInference];

    public Task OnChatScriptEvent(
        IActionScriptEvent e,
        ChatMessageData? message,
        ICharacterOrUserData? character,
        CancellationToken cancellationToken
    )
    {
        switch (e)
        {
            case InitActionScriptEvent:
                logger.LogInformation("Chat session {SessionId} initialized", session.SessionId);
                break;
            case StartActionScriptEvent start:
                logger.LogInformation("Chat session {SessionId} started (HasBootstrapMessages: {HasBootstrapMessages})", session.SessionId, start.HasBootstrapMessages);
                break;
            case UserMessageReceivedActionScriptEvent userMessageReceived:
                logger.LogInformation("Received a user message: {MessageContent}", userMessageReceived.Message);
                // You can modify the user message:
                // e.g.: userMessageReceived.RewriteUserMessage = userMessageReceived.Message + " (edited by SampleScriptEventChatAugmentationInstance)";
                break;
            case GeneratingActionScriptEvent generating:
                logger.LogInformation("Is generating a response for {Character}...", generating.Character.Name);
                break;
            case GeneratingCompleteActionScriptEvent:
                logger.LogInformation("Completed generating a response");
                break;
            case SpeechStartActionScriptEvent speechStart:
                logger.LogInformation("{Character} started speaking", speechStart.Character.Name);
                break;
            case SpeechCompleteActionScriptEvent speechComplete:
                logger.LogInformation("{Character} completed speaking", speechComplete.Character.Name);
                break;
            case BeforeSelectActionInferenceActionScriptEvent beforeSelectActionInference:
                logger.LogInformation("Is about to select an action inference on layout: {Layer}", beforeSelectActionInference.Layer);
                // You can add to or filter the actions list:
                // e.g.: beforeSelectActionInference.Actions = beforeSelectActionInference.Actions.Where(a => a != "action_to_filter").ToArray();
                break;
            case ActionInferenceSelectedActionScriptEvent actionInferenceSelected:
                logger.LogInformation("Triggered scenario action: {ActionName}", actionInferenceSelected.Action);
                break;
            case TranscriptionStartedScriptEvent:
                logger.LogInformation("Started transcription");
                break;
            case TranscriptionFinishedScriptEvent transcriptionFinished:
                logger.LogInformation("Finished transcription: {Text}", transcriptionFinished.Text);
                break;
            case TypingStartScriptEvent:
                logger.LogInformation("Started typing...");
                break;
            case TypingEndScriptEvent:
                logger.LogInformation("Ended typing.");
                break;
        }
        
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}