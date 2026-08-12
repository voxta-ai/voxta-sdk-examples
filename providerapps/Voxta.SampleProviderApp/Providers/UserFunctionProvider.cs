using Microsoft.Extensions.Logging;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;
using Voxta.Providers.Host;

namespace Voxta.SampleProviderApp.Providers;

// This example shows how to create commands that the AI can call.
// This will slow down the AI since there will be an LLM run before generating text.
public class UserFunctionProvider(
    IRemoteChatSession session,
    ILogger<UserFunctionProvider> logger
) : ProviderBase(session, logger)
{
    protected override async Task OnStartAsync()
    {
        await base.OnStartAsync();
        
        // Register our action
        Send(new ClientUpdateContextMessage
        {
            SessionId = SessionId,
            ContextKey = "SampleUserFunctions",
            Actions = 
            [
                // Example simple action
                new()
                {
                    // The name used by the LLM to select the action. Make sure to select a clear name.
                    Name = "run_diagnostic",
                    // A short description of the action to be included in the functions list, typically used for character action inference
                    ShortDescription = "run a self-diagnostic",
                    // The condition for executing this function
                    Description = "When {{ user }} asks to run a self-diagnostic.",
                    // This match will ensure user action inference is only going to be triggered if this regex matches the message.
                    // For example, if you use "please" in all functions, this can avoid running user action inference at all unless
                    // the user said "please".
                    MatchFilter = ["diagnostic"],
                    // Only run in response to the user messages 
                    Timing = FunctionTiming.AfterUserMessage,
                    // Do not generate a response, we will instead handle the action ourselves
                    CancelReply = true,
                },
                // Example action with arguments
                new()
                {
                    Name = "play_music",
                    ShortDescription = "play music on the speaker",
                    Description = "When {{ user }} asks to play music.",
                    Timing = FunctionTiming.AfterUserMessage,
                    CancelReply = true,
                    Arguments =
                    [
                        new FunctionArgumentDefinition
                        {
                            Name = "music_search_query",
                            Description = "The first result of this query will be played",
                            Required = true,
                            Type = FunctionArgumentType.String,
                        }
                    ],
                }
            ]
        });
        
        // Act when an action is called
        HandleMessage<ServerActionMessage>(message =>
        {
            // We only want to handle user actions
            if (message.Role != ChatMessageRole.User) return;
            
            switch (message.Value)
            {
                case "run_diagnostic":
                    //TODO: Run the diagnostic
                    Logger.LogInformation("Running the self-diagnostic");
                    
                    Send(new ClientSendMessage
                    {
                        SessionId = SessionId,
                        // We want to avoid a loop!
                        DoUserActionInference = false,
                        CharacterResponsePrefix = "[The requested self-diagnostic has completed successfully, everything is in order]"
                    });
                    break;
                case "play_music":
                    if(!message.TryGetArgument("music_search_query", out var query))
                        query = "anything";
                    //TODO: Play the song
                    Logger.LogInformation("Playing music. Search query: '{Query}", query);
                    
                    Send(new ClientSendMessage
                    {
                        SessionId = SessionId,
                        // We want to avoid a loop!
                        DoUserActionInference = false,
                        Text = $"/note As requested, the song \"{query}\" starts playing."
                    });
                    break;
            }
        });
    }
}
