using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Voxta.Abstractions.Chats.Sessions;
using Voxta.Abstractions.Services.ChatAugmentations;
using Voxta.Model.Shared;

namespace Voxta.Modules.Examples.ChatAugmentationExample.CustomActionAugmentationExample;

public class SamplePreprocessorChatAugmentationInstance(
    IChatSessionChatAugmentationApi session,
    ILogger<SamplePreprocessorChatAugmentationInstance> logger
    ) : IChatPreProcessAugmentation, IChatPostProcessAugmentation
{
    public ServiceTypes[] GetRequiredServiceTypes() => [ServiceTypes.ActionInference];
    public string[] GetAugmentationNames() => [VoxtaModule.ExampleAugmentationsKey];

    public ValueTask<string> PreProcessTextAsync(ChatMessageRole role, string text, CancellationToken cancellationToken)
    {
        if (role == ChatMessageRole.User)
        {
            // Add a timestamp to user messages
            logger.LogInformation("Pre processing user message: {Text}", text);
            return ValueTask.FromResult($"[{DateTime.Now.ToString(session.MainCharacter.Culture)}] " + text);
        }

        return ValueTask.FromResult(text);
    }

    public ValueTask<string> PostProcessTextAsync(ChatMessageRole role, string text, CancellationToken cancellationToken)
    {
        if (role == ChatMessageRole.Assistant)
        {
            // Replace Assistant by Robot in character messages
            logger.LogInformation("Post processing assistant message: {Text}", text);
            return ValueTask.FromResult(Regex.Replace(text, "[Aa]ssistant", "Robot"));
        }
        
        return ValueTask.FromResult(text);
    }


    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}