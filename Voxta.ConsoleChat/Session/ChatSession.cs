using Voxta.Client;
using Voxta.ConsoleChat.Audio;
using Voxta.ConsoleChat.Functions;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;
using ModelRole = Voxta.Model.Shared.ChatMessageRole;

namespace Voxta.ConsoleChat.Session;

public class ChatSession(
    IVoxtaWebsocketsClient client,
    IAudioPlayback playback,
    ChatFunctions functions,
    ServerChatStartedMessage started) : IDisposable
{
    public event Action<ChatMessage>? MessageStarted;
    public event Action<string>? MessageAppended;
    public event Action<string?>? StatusChanged;
    public event Action? LeaveRequested;

    public IFunctionInteraction? Interaction { get; set; }

    public Guid SessionId => started.SessionId;
    public Guid ChatId => started.ChatId;
    public string CharacterName => started.Characters[0].Name;
    public string UserName => started.User.Name;

    public IReadOnlyList<ChatMessage> History { get; } = started.Messages
        .Where(message => message.Role is ModelRole.User or ModelRole.Assistant)
        .Select(message => new ChatMessage(
            message.Role == ModelRole.User ? ChatRole.User : ChatRole.Character,
            message.Name ?? (message.Role == ModelRole.User ? started.User.Name : started.Characters[0].Name),
            message.Text))
        .ToList();

    private bool _characterSpeaking;
    private ServerActionMessage? _pendingAction;
    private bool _startNewCharacterLine;

    public void Start()
    {
        playback.ChunkPlaying += OnChunkPlaying;
        playback.Completed += OnPlaybackCompleted;
        playback.Failed += OnPlaybackFailed;

        if (started.Services.SpeechToText == null) Warn("Microphone input is disabled");
        if (started.Services.TextToSpeech == null) Warn("Voice playback is disabled");
        if (started.Services.ActionInference == null) Warn("Action inference is disabled, functions will not work");
    }

    public void SendText(string text)
    {
        MessageStarted?.Invoke(new ChatMessage(ChatRole.User, UserName, text));
        StatusChanged?.Invoke("Thinking...");
        client.Send(new ClientSendMessage
        {
            SessionId = SessionId,
            Text = text,
            DoReply = true,
            DoUserActionInference = true,
            DoCharacterActionInference = false,
        });
    }

    internal void OnReplyStart(ServerReplyStartMessage message, CancellationToken cancellationToken)
    {
        _characterSpeaking = true;
        _startNewCharacterLine = true;
        playback.Begin(message.MessageId, cancellationToken);
    }

    internal void OnReplyChunk(ServerReplyChunkMessage message) => playback.Enqueue(message);

    internal void OnReplyEnd(ServerReplyEndMessage message) => playback.End(message.MessageId);

    internal void OnSpeechRecognitionStart(ServerSpeechRecognitionStartMessage _) => StatusChanged?.Invoke("Transcribing...");

    internal void OnSpeechRecognitionPartial(ServerSpeechRecognitionPartialMessage message) => StatusChanged?.Invoke(message.Text);

    internal void OnSpeechRecognitionEnd(ServerSpeechRecognitionEndMessage message)
    {
        if (string.IsNullOrEmpty(message.Text))
        {
            StatusChanged?.Invoke(null);
            return;
        }

        MessageStarted?.Invoke(new ChatMessage(ChatRole.User, UserName, message.Text));
        StatusChanged?.Invoke("Thinking...");
        client.Send(new ClientSendMessage
        {
            SessionId = SessionId,
            Text = message.Text,
            DoReply = true,
            DoUserActionInference = false,
            DoCharacterActionInference = false,
        });
    }

    internal void OnAction(ServerActionMessage action)
    {
        _pendingAction = action;
        if (!_characterSpeaking)
            RunPendingAction();
    }

    private void OnChunkPlaying(object? sender, (ServerReplyChunkMessage Chunk, TimeSpan Duration) e)
    {
        StatusChanged?.Invoke(null);
        if (_startNewCharacterLine)
        {
            _startNewCharacterLine = false;
            MessageStarted?.Invoke(new ChatMessage(ChatRole.Character, CharacterName, e.Chunk.Text));
        }
        else
        {
            MessageAppended?.Invoke(e.Chunk.Text);
        }
        client.Send(new ClientSpeechPlaybackStartMessage
        {
            SessionId = e.Chunk.SessionId,
            MessageId = e.Chunk.MessageId,
            StartIndex = e.Chunk.StartIndex,
            EndIndex = e.Chunk.EndIndex,
            Duration = e.Duration.TotalSeconds,
        });
    }

    private void OnPlaybackCompleted(object? sender, Guid messageId)
    {
        client.Send(new ClientSpeechPlaybackCompleteMessage { SessionId = SessionId, MessageId = messageId });
        _characterSpeaking = false;
        if (_pendingAction != null)
            RunPendingAction();
    }

    private void OnPlaybackFailed(object? sender, Exception exception) => Warn(exception.Message);

    private void RunPendingAction()
    {
        var action = _pendingAction;
        _pendingAction = null;
        if (action == null) return;

        var result = functions.Execute(action, Interaction ?? DenyInteraction.Instance);
        if (result.SystemMessage != null)
            MessageStarted?.Invoke(new ChatMessage(ChatRole.System, "System", result.SystemMessage));
        if (result.LeaveRequested)
        {
            LeaveRequested?.Invoke();
            return;
        }
        if (!string.IsNullOrEmpty(result.EventMessage))
            client.Send(new ClientSendMessage
            {
                SessionId = SessionId,
                Text = "/event " + result.EventMessage,
                DoReply = true,
                DoUserActionInference = false,
                DoCharacterActionInference = false,
            });
    }

    private void Warn(string message) => MessageStarted?.Invoke(new ChatMessage(ChatRole.System, "System", message));

    public void Dispose()
    {
        playback.ChunkPlaying -= OnChunkPlaying;
        playback.Completed -= OnPlaybackCompleted;
        playback.Failed -= OnPlaybackFailed;
        GC.SuppressFinalize(this);
    }
}
