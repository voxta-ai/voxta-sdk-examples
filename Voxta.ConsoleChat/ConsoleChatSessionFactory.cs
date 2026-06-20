using Voxta.Client;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat;

public class ConsoleChatSessionFactory(VoxtaFunctions voxtaFunctions)
{
    public void SendStartChatMessage(IVoxtaWebsocketsClient client, Guid charId)
    {
        client.Send(new ClientStartChatMessage
        {
            CharacterId = charId,
            Contexts = [new()
            {
                Text = "{{char}} exists inside a command line. {{char}} is being prompted by {{user}} inside the terminal. {{char}} can speak and run commands."
            }],
            Actions = voxtaFunctions.FunctionsDefinitions,
        });
    }

    public ConsoleChatSession Create(IVoxtaWebsocketsClient client, IPlaybackManager playbackManager, IConsoleOutput consoleOutput, ServerChatStartedMessage chatStartedMessage)
    {
        return new ConsoleChatSession(client, consoleOutput, chatStartedMessage, playbackManager);
    }
}
