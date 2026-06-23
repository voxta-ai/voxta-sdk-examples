using Voxta.Client;
using Voxta.ConsoleChat.Functions;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat.Session;

public class ChatSession(
    IVoxtaWebsocketsClient client,
    SpeechPlayback speech,
    ChatFunctions functions,
    ServerChatStartedMessage started,
    Transcript transcript,
    string? audioUnavailableReason) : IDisposable
{
    private bool _characterSpeaking;
    private ServerActionMessage? _pendingAction;
    private TranscriptEntry? _reply;
    private Guid _replyId;

    public event Action<string?>? StatusChanged;
    public event Action<bool>? ConnectionChanged;
    public event Action<bool>? TurnChanged;
    public event Action? LeaveRequested;

    public IFunctionInteraction? Interaction { get; set; }
    public Transcript Transcript => transcript;

    public Guid SessionId => started.SessionId;
    public Guid ChatId => started.ChatId;
    public string CharacterName => started.Characters[0].Name;
    public string UserName => started.User.Name;

    public void Start()
    {
        speech.ChunkStarted += OnChunkStarted;
        speech.ReplyCompleted += OnReplyCompleted;
        speech.Failed += OnSpeechFailed;

        if (started.Services.SpeechToText == null) AddSystem("Microphone input is disabled");
        if (started.Services.TextToSpeech == null) AddSystem("Voice playback is disabled");
        if (started.Services.ActionInference == null) AddSystem("Action inference is disabled, functions will not work");
        if (audioUnavailableReason != null) AddSystem(audioUnavailableReason);
    }

    public void SendText(string text) => Submit(text, doUserActionInference: true);

    public void TypingStarted() => client.Send(new ClientTypingStartMessage { SessionId = SessionId });

    public void TypingStopped(bool sent) => client.Send(new ClientTypingEndMessage { SessionId = SessionId, Sent = sent });

    public void Stop() => client.Send(new ClientStopChatMessage { SessionId = SessionId });

    internal void SetConnected(bool connected) => ConnectionChanged?.Invoke(connected);

    internal void OnChatFlow(ServerChatFlowMessage message) =>
        TurnChanged?.Invoke(message.State == ChatFlowStates.WaitingForUserInput
            || (message.State == ChatFlowStates.SpeakerSelected && message.NextSpeakerId == null));

    internal void OnReplyStart(ServerReplyStartMessage message, CancellationToken cancellationToken)
    {
        _characterSpeaking = true;
        _replyId = message.MessageId;
        _reply ??= AddCharacterEntry();
        _reply.MessageId = message.MessageId;
        speech.Begin(SessionId, message.MessageId, cancellationToken);
    }

    internal void OnReplyChunk(ServerReplyChunkMessage message) => speech.Enqueue(message);

    internal void OnReplyEnd(ServerReplyEndMessage message)
    {
        speech.End(message.MessageId);
        // No audio was playing (a text-only or empty reply): finalize now so the chat is not left waiting.
        if (!_characterSpeaking)
        {
            speech.Interrupt(SessionId, message.MessageId);
            FinishReply();
        }
    }

    internal void OnReplyCancelled(ServerReplyCancelledMessage message)
    {
        speech.Interrupt(SessionId, message.MessageId);
        FinishReply();
    }

    internal void OnChatError(string message)
    {
        AddError(message);
        if (_reply == null) return;
        speech.Interrupt(SessionId, _reply.MessageId);
        FinishReply();
    }

    internal void OnSpeechRecognitionStart(ServerSpeechRecognitionStartMessage _) => StatusChanged?.Invoke("Listening...");

    internal void OnSpeechRecognitionPartial(ServerSpeechRecognitionPartialMessage message) => StatusChanged?.Invoke(message.Text);

    internal void OnSpeechRecognitionEnd(ServerSpeechRecognitionEndMessage message)
    {
        StatusChanged?.Invoke(null);
        if (!string.IsNullOrEmpty(message.Text))
            Submit(message.Text, doUserActionInference: false);
    }

    internal void OnAction(ServerActionMessage action)
    {
        // An empty value is sent for inspection only; ignore it.
        if (string.IsNullOrEmpty(action.Value)) return;
        _pendingAction = action;
        if (!_characterSpeaking)
            RunPendingAction();
    }

    internal void OnServerUpdated(ServerUpdatedMessage message)
    {
        var entry = transcript.LastWithMessageId(message.MessageId);
        if (entry == null || message.Text == null) return;
        entry.Text = message.Text;
        entry.Pending = false;
        transcript.NotifyChanged();
    }

    private void Submit(string text, bool doUserActionInference)
    {
        if (_characterSpeaking)
        {
            // Interrupt the current reply before sending the new message.
            client.Send(new ClientInterruptMessage { SessionId = SessionId });
            speech.Interrupt(SessionId, _replyId);
            _characterSpeaking = false;
            ClearPendingReply();
        }

        transcript.Add(new TranscriptEntry { Role = TranscriptRole.User, Speaker = UserName, Text = text });
        StatusChanged?.Invoke(null);
        _reply = AddCharacterEntry();

        client.Send(new ClientSendMessage
        {
            SessionId = SessionId,
            Text = text,
            DoReply = true,
            DoUserActionInference = doUserActionInference,
            DoCharacterActionInference = false,
        });
    }

    private void OnChunkStarted(object? sender, ServerReplyChunkMessage chunk)
    {
        _reply ??= AddCharacterEntry();

        if (_reply.Text.Length == 0)
        {
            // The empty placeholder adopts the first chunk's narration style.
            _reply.Narration = chunk.IsNarration;
        }
        else if (_reply.Narration != chunk.IsNarration)
        {
            // Narration and dialogue need different styling, so continue in a new entry.
            _reply = AddCharacterEntry(continuation: true, narration: chunk.IsNarration);
        }

        _reply.Pending = false;
        _reply.Text += chunk.Text;
        transcript.NotifyChanged();
    }

    private void OnReplyCompleted(object? sender, Guid messageId) => FinishReply();

    private void OnSpeechFailed(object? sender, Exception exception) => AddError(exception.Message);

    private void FinishReply()
    {
        _characterSpeaking = false;
        ClearPendingReply();
        if (_pendingAction != null)
            RunPendingAction();
    }

    private void ClearPendingReply()
    {
        if (_reply == null) return;
        if (_reply.Text.Length == 0)
            transcript.Remove(_reply);
        else
        {
            _reply.Pending = false;
            transcript.NotifyChanged();
        }
        _reply = null;
    }

    private void RunPendingAction()
    {
        var action = _pendingAction;
        _pendingAction = null;
        if (action == null) return;

        // A cancel-reply action takes over, so drop the empty "thinking" placeholder.
        if (_reply is { Text.Length: 0 })
            ClearPendingReply();

        var result = functions.Execute(action, Interaction ?? DenyInteraction.Instance);
        if (result.SystemMessage != null)
            AddSystem(result.SystemMessage);
        if (result.LeaveRequested)
        {
            LeaveRequested?.Invoke();
            return;
        }
        if (!string.IsNullOrEmpty(result.EventMessage))
            client.Send(new ClientSendMessage
            {
                // An event the AI sees and reacts to (e.g. it summarizes a command's output).
                SessionId = SessionId,
                Text = "/event " + result.EventMessage,
                DoReply = true,
                DoUserActionInference = false,
                DoCharacterActionInference = false,
            });
    }

    private TranscriptEntry AddCharacterEntry(bool continuation = false, bool narration = false)
    {
        var entry = transcript.Add(new TranscriptEntry
        {
            Role = TranscriptRole.Character,
            Speaker = CharacterName,
            Pending = !continuation,
            Continuation = continuation,
            Narration = narration,
        });
        if (continuation) entry.MessageId = _replyId;
        return entry;
    }

    private void AddSystem(string message) => transcript.Add(new TranscriptEntry { Role = TranscriptRole.System, Speaker = "System", Text = message });

    private void AddError(string message) => transcript.Add(new TranscriptEntry { Role = TranscriptRole.Error, Speaker = "Error", Text = message });

    public void Dispose()
    {
        speech.ChunkStarted -= OnChunkStarted;
        speech.ReplyCompleted -= OnReplyCompleted;
        speech.Failed -= OnSpeechFailed;
        GC.SuppressFinalize(this);
    }
}
