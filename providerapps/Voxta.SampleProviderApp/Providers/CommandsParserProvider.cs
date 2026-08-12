using Microsoft.Extensions.Logging;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Providers.Host;
using Voxta.Providers.Host.Utilities;

namespace Voxta.SampleProviderApp.Providers;

// This is an example of a provider that receives commands from the chat and forward them to a hardware device.
// In this example we check if there's a command such as [device speed=5] and forward this information to an external device
public class CommandsParserProvider(
    IRemoteChatSession session,
    ILogger<CommandsParserProvider> logger,
    ISimpleCommandsParser commandsParser
)
    : ProviderBase(session, logger)
{
    protected override async Task OnStartAsync()
    {
        await base.OnStartAsync();
        // We only need to handle the character messages
        HandleMessage(ChatMessageRole.Assistant, RemoteChatMessageTiming.Spoken, OnAssistantChatMessage);
    }

    private void OnAssistantChatMessage(RemoteChatMessage message)
    {
        // If there is no command in the text, bail out
        if (!commandsParser.TryParse(message.Text, out var command))
            return;

        switch (command.Name)
        {
            case "device":
                // Example of a command named [device speed=0] or [device speed=slow] with a speed between 0 and 10
                OnDeviceCommand(command);
                break;
            default:
                // If we get here, it means the command was invalid
                Logger.LogWarning("Unknown or invalid command received: {Command}", command.RawString);
                break;
        }
    }

    private void OnDeviceCommand(SimpleCommand command)
    {
        // First we try to parse the arguments
        if (command.Arguments.TryGetValue("speed", out var speedString) && double.TryParse(speedString, out var speed))
        {
            // If we can parse the speed, clamp it between 0 and 10
            speed = Math.Clamp(speed, 0, 10);
        }
        else
        {
            // If we cannot parse the speed, the AI may have generated something close, we can try parsing it anyway
            speed = speedString switch
            {
                null => 0,
                "slow" => 3,
                "medium" => 6,
                "fast" => 9,
                _ => 0
            };
        }

        // Now we can send the command to the device
        // TODO: Here you need to call the device 
        Logger.LogInformation("Device speed set to: {Speed}", speed);
        
        // Update the AI context so the AI is aware of the device status
        Send(new ClientUpdateContextMessage
        {
            SessionId = SessionId,
            ContextKey = "DeviceExample/Speed",
            Contexts = [new() { Text = "{{ user }}'s device speed is set to " + speed + "/10." }],
        });
    }
}
