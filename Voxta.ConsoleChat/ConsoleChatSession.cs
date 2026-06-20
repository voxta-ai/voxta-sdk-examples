using Voxta.Client;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat;

public class ConsoleChatSession(IVoxtaWebsocketsClient client, IConsoleOutput output, ServerChatStartedMessage chatStartedMessage, IPlaybackManager manager)
{
    private bool _characterSpeaking;
    private ServerActionMessage? _pendingAction;

    public Guid SessionId => chatStartedMessage.SessionId;
    public Guid ChatId => chatStartedMessage.ChatId;

    public void Ready()
    {
        manager.OnSpeechStart += OnSpeechStart;
        manager.OnSpeechChunk += OnSpeechChunk;
        manager.OnSpeechComplete += OnSpeechComplete;
        manager.OnSpeechError += OnSpeechError;
            
        output.WriteLine($"Chat ready: {chatStartedMessage.ChatId}", ConsoleColor.Green);
        if(chatStartedMessage.Services.SpeechToText == null)
            output.WriteErrorLine("Microphone input is disabled");
        if(chatStartedMessage.Services.TextToSpeech == null)
            output.WriteErrorLine("Voice playback is disabled");
        if(chatStartedMessage.Services.ActionInference == null)
            output.WriteErrorLine("Action inference is disabled, functions will not work");
    }

    public void OnReplyStart(ServerReplyStartMessage replyStartMessage, CancellationToken cancellationToken)
    {
        _characterSpeaking = true;
        manager.Start(replyStartMessage.MessageId, cancellationToken);
    }

    public void OnReplyChunk(ServerReplyChunkMessage replyChunkMessage)
    {
        manager.PlayChunk(replyChunkMessage);
    }

    public void OnReplyEnd(ServerReplyEndMessage replyEndMessage)
    {
        manager.Complete(replyEndMessage.MessageId);
    }

    private void OnSpeechStart(object? sender, Guid messageId)
    {
    }

    private void OnSpeechChunk(object? sender, (ServerReplyChunkMessage Chunk, TimeSpan Duration) e)
    {
        output.Write($"{chatStartedMessage.Characters[0].Name}: ", ConsoleColor.Magenta);
        output.WriteLine(e.Chunk.Text);
        client.Send(new ClientSpeechPlaybackStartMessage
        {
            SessionId = e.Chunk.SessionId,
            MessageId = e.Chunk.MessageId,
            StartIndex = e.Chunk.StartIndex,
            EndIndex = e.Chunk.EndIndex,
            Duration = e.Duration.TotalSeconds,
        });
    }

    private void OnSpeechComplete(object? sender, Guid messageId)
    {
        client.Send(new ClientSpeechPlaybackCompleteMessage
        {
            SessionId = chatStartedMessage.SessionId,
            MessageId = messageId,
        });
        
        _characterSpeaking = false;
        
        if(_pendingAction != null) RunPendingAction();
        else PrintUserPromptInvite();
    }

    private void OnSpeechError(object? sender, Exception e)
    {
        output.WriteErrorLine(e.ToString());
    }

    public void SendMessage(ClientSendMessage message)
    {
        client.Send(message);
    }

    public void OnSpeechRecognitionStart(ServerSpeechRecognitionStartMessage _)
    {
        output.WriteLine("Transcribing...", ConsoleColor.DarkGray);
    }

    public void OnSpeechRecognitionPartial(ServerSpeechRecognitionPartialMessage speechRecognitionPartialMessage)
    {
        output.WriteLine(speechRecognitionPartialMessage.Text, ConsoleColor.DarkGray);
    }

    public void OnSpeechRecognitionEnd(ServerSpeechRecognitionEndMessage speechRecognitionEndMessage)
    {
        if (string.IsNullOrEmpty(speechRecognitionEndMessage.Text))
        {
            output.WriteLine("Canceled", ConsoleColor.DarkGray);
        }
        else
        {
            output.Write($"{chatStartedMessage.User.Name}: ", ConsoleColor.Blue);
            output.WriteLine(speechRecognitionEndMessage.Text);
            output.WriteLine("Thinking...", ConsoleColor.DarkGray);
            client.Send(new ClientSendMessage
            {
                SessionId = chatStartedMessage.SessionId,
                Text = speechRecognitionEndMessage.Text,
                DoCharacterActionInference = false,
                DoUserActionInference = false,
                DoReply = true,
            });
        }
    }

    public void Dispose()
    {
        manager.OnSpeechStart -= OnSpeechStart;
        manager.OnSpeechChunk -= OnSpeechChunk;
        manager.OnSpeechComplete -= OnSpeechComplete;
        manager.OnSpeechError -= OnSpeechError;
    }

    public void OnAction(ServerActionMessage actionMessage)
    {
        _pendingAction = actionMessage;
        if (_characterSpeaking)
            return;
        RunPendingAction();
    }

    private void RunPendingAction()
    {
        var pendingAction = _pendingAction;
        _pendingAction = null;
        if (pendingAction == null) return;
        var response = VoxtaFunctions.Execute(pendingAction);
        if (!string.IsNullOrEmpty(response))
        {
            SendMessage(
                new ClientSendMessage
                {
                    SessionId = chatStartedMessage.SessionId,
                    Text = "/event " + response,
                    DoUserActionInference = false,
                    DoCharacterActionInference = false,
                }
            );
        }
    }

    private void PrintUserPromptInvite()
    {
        output.Write(chatStartedMessage.User.Name + ": ", ConsoleColor.Blue);
    }
}