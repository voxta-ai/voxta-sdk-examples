using Microsoft.Extensions.Logging;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;
using Voxta.Providers.Host;

namespace Voxta.SampleProviderApp.Providers;

// This example shows how to create and act on character action inference.
// Note that this is typically not for user commands, another system will be released later
public class ActionProvider(
    IRemoteChatSession session,
    ILogger<ActionProvider> logger
) : ProviderBase(session, logger)
{
    protected override async Task OnStartAsync()
    {
        await base.OnStartAsync();
        
        // Register our action
        Send(new ClientUpdateContextMessage
        {
            SessionId = SessionId,
            ContextKey = "SampleActions",
            Actions = 
            [
                new()
                {
                    // The LLM will use this name to call the action, use a good action name
                    Name = "vibrate_gamepad",
                    // Layers allow you to run your actions separately from the scene
                    Layer = "gamepad",
                    // Helps the AI understand when and how to use the function
                    Description = "When {{ char }} wants to physically interact with {{ user }}.",
                    // This text will be prepended to the AI's response
                    Effect = new ActionEffect
                    {
                        Secret = "{{ char }} made {{ user }}'s gamepad vibrate."
                    },
                    // Optional arguments for your action
                    Arguments =
                    [
                        new FunctionArgumentDefinition
                        {
                            // The name sent to the LLM
                            Name = "strength",
                            // The type of argument
                            Type = FunctionArgumentType.String,
                            // Whether to access failure to generate this argument
                            Required = true,
                            // Explanation for the LLM
                            Description = "The strength of the vibration. Can be 'low', 'medium', or 'high'."
                        }
                    ]
                }
            ]
        });
        
        // Act when an action is called
        HandleMessage<ServerActionMessage>(message =>
        {
            // We only care about our layer
            if (message.Layer != "gamepad") return;
            
            switch (message.Value)
            {
                case "vibrate_gamepad":
                    var strength = message.Arguments?.FirstOrDefault(a => a.Name == "strength")?.Value ?? "undefined";
                    //TODO: Make the gamepad vibrate
                    Logger.LogInformation("Running vibrate_gamepad command with strength {Strength}", strength);
                    break;
            }
        });
    }
}
