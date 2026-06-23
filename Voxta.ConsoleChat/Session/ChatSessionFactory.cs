using Voxta.Client;
using Voxta.ConsoleChat.Functions;
using Voxta.Model.WebsocketMessages.ServerMessages;
using ModelRole = Voxta.Model.Shared.ChatMessageRole;

namespace Voxta.ConsoleChat.Session;

public sealed class ChatSessionFactory(ChatFunctions functions)
{
    public ChatSession Create(IVoxtaWebsocketsClient client, SpeechPlayback speech, ServerChatStartedMessage started, string? audioUnavailableReason)
    {
        var transcript = new Transcript();
        foreach (var message in started.Messages.Where(m => m.Role is ModelRole.User or ModelRole.Assistant))
            transcript.Add(new TranscriptEntry
            {
                MessageId = message.MessageId,
                Role = message.Role == ModelRole.User ? TranscriptRole.User : TranscriptRole.Character,
                Speaker = message.Name ?? (message.Role == ModelRole.User ? started.User.Name : started.Characters[0].Name),
                Text = message.Text,
            });

        return new ChatSession(client, speech, functions, started, transcript, audioUnavailableReason);
    }
}
