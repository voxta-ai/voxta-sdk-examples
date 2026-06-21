using Microsoft.Extensions.Logging;
using Voxta.Client;
using Voxta.ConsoleChat.Audio;
using Voxta.ConsoleChat.Auth;
using Voxta.ConsoleChat.Functions;
using Voxta.ConsoleChat.Persistence;
using Voxta.Model;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat.Session;

public class VoxtaConnection : IAsyncDisposable
{
    private const string ClientName = "Voxta.ConsoleChat";

    private const string TerminalContext =
        "{{char}} exists inside a command line. {{char}} is being prompted by {{user}} inside the terminal. " +
        "{{char}} can speak and run commands.";

    private readonly IVoxtaWebsocketsClient _client;
    private readonly VoxtaTokenProvider _tokenProvider;
    private readonly IDeviceAuthorization _deviceAuthorization;
    private readonly ITokenStore _tokenStore;
    private readonly IAudioRecorder _recorder;
    private readonly IAudioPlayback _playback;
    private readonly ChatFunctions _functions;
    private readonly ILastChatStore _lastChatStore;
    private readonly ILogger<VoxtaConnection> _logger;

    private readonly TaskCompletionSource _welcome = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource<ServerChatStartedMessage>? _pendingStart;
    private IProgress<LoadingUpdate>? _loadingProgress;
    private ChatSession? _session;
    private CancellationToken _cancellationToken = CancellationToken.None;

    public VoxtaConnection(
        IVoxtaWebsocketsClientFactory clientFactory,
        VoxtaTokenProvider tokenProvider,
        IDeviceAuthorization deviceAuthorization,
        ITokenStore tokenStore,
        IAudioRecorder recorder,
        IAudioPlayback playback,
        ChatFunctions functions,
        ILastChatStore lastChatStore,
        ILogger<VoxtaConnection> logger)
    {
        _tokenProvider = tokenProvider;
        _deviceAuthorization = deviceAuthorization;
        _tokenStore = tokenStore;
        _recorder = recorder;
        _playback = playback;
        _functions = functions;
        _lastChatStore = lastChatStore;
        _logger = logger;

        _client = clientFactory.Create(
            new ClientAuthenticateMessage
            {
                Client = ClientName,
                ClientVersion = "1.0",
                Scope = [ClientScopes.AppRole],
                Capabilities = new ClientCapabilities
                {
                    AudioOutput = AudioOutputClientCapabilities.Url,
                    AcceptedAudioContentTypes = NAudioAudioPlayback.SupportedContentTypes,
                    AudioInput = AudioInputClientCapabilities.WebSocketStream,
                    VisionCapture = VisionCaptureClientCapabilities.None,
                },
            },
            tokenProvider);

        _client.OnMessageReceived += OnMessageReceived;
        _client.OnError += OnError;
    }

    public async Task ConnectAsync(IConnectionProgress progress, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        await AuthenticateAsync(progress, cancellationToken);

        progress.Status("Connecting...");
        await _client.ConnectAsync(cancellationToken);
        await _welcome.Task.WaitAsync(cancellationToken);

        await _recorder.ConnectAsync(_tokenProvider);
        _recorder.Start();
        progress.Status("Connected");
    }

    public Task<ChatSession> StartChatAsync(Guid characterId, IProgress<LoadingUpdate> progress, CancellationToken cancellationToken) =>
        StartAsync(new ClientStartChatMessage
        {
            CharacterId = characterId,
            Contexts = [new ContextDefinition { Text = TerminalContext }],
            Actions = _functions.Definitions,
        }, progress, cancellationToken);

    public Task<ChatSession> ResumeChatAsync(Guid chatId, IProgress<LoadingUpdate> progress, CancellationToken cancellationToken) =>
        StartAsync(new ClientStartChatMessage
        {
            ChatId = chatId,
            Actions = _functions.Definitions,
        }, progress, cancellationToken);

    private async Task<ChatSession> StartAsync(ClientStartChatMessage message, IProgress<LoadingUpdate> progress, CancellationToken cancellationToken)
    {
        _pendingStart = new TaskCompletionSource<ServerChatStartedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _loadingProgress = progress;
        try
        {
            _client.Send(message);
            var started = await _pendingStart.Task.WaitAsync(cancellationToken);
            _lastChatStore.Save(new LastChat(started.ChatId, started.Characters[0].Id, started.Characters[0].Name));
            _session = new ChatSession(_client, _playback, _functions, started);
            _session.Start();
            return _session;
        }
        finally
        {
            _loadingProgress = null;
        }
    }

    public void LeaveChat()
    {
        _session?.Dispose();
        _session = null;
    }

    private async Task AuthenticateAsync(IConnectionProgress progress, CancellationToken cancellationToken)
    {
        var token = _tokenStore.Load();
        if (!await _deviceAuthorization.ValidateTokenAsync(token, cancellationToken))
        {
            var code = await _deviceAuthorization.GetDeviceCodeAsync(ClientName, cancellationToken);
            progress.DeviceCode(code.UserCode, code.VerificationUrl);
            progress.Status("Waiting for browser authentication...");

            token = await _deviceAuthorization.WaitForTokenAsync(code, cancellationToken)
                ?? throw new InvalidOperationException("Authentication failed");
            _tokenStore.Save(token);
        }

        _tokenProvider.Token = token;
    }

    private void OnMessageReceived(object? sender, ServerMessage message)
    {
        switch (message)
        {
            case ServerWelcomeMessage:
                _welcome.TrySetResult();
                break;
            case ServerAuthenticationRequiredMessage:
                _welcome.TrySetException(new InvalidOperationException("You need to create your profile in Voxta first."));
                break;
            case ServerChatStartingMessage:
                _loadingProgress?.Report(new LoadingUpdate(0, "Starting chat..."));
                break;
            case ServerChatLoadingMessage loading:
                _loadingProgress?.Report(new LoadingUpdate(loading.Progress, loading.Text));
                break;
            case ServerChatStartedMessage started:
                _pendingStart?.TrySetResult(started);
                break;
            case ServerActionMessage action:
                _session?.OnAction(action);
                break;
            case ServerReplyStartMessage reply:
                _session?.OnReplyStart(reply, _cancellationToken);
                break;
            case ServerReplyChunkMessage chunk:
                _session?.OnReplyChunk(chunk);
                break;
            case ServerReplyEndMessage end:
                _session?.OnReplyEnd(end);
                break;
            case ServerSpeechRecognitionStartMessage start:
                _session?.OnSpeechRecognitionStart(start);
                break;
            case ServerSpeechRecognitionPartialMessage partial:
                _session?.OnSpeechRecognitionPartial(partial);
                break;
            case ServerSpeechRecognitionEndMessage recognized:
                _session?.OnSpeechRecognitionEnd(recognized);
                break;
            case ServerRecordingRequestMessage recording:
                if (recording.Enabled) _recorder.Start();
                else _recorder.Stop();
                break;
            case ServerErrorMessage error:
                _logger.LogError("Server error: {Message} {Details}", error.Message, error.Details);
                _pendingStart?.TrySetException(new InvalidOperationException(error.Message));
                break;
        }
    }

    private void OnError(object? sender, Exception exception) => _logger.LogError(exception, "Websocket error");

    public async ValueTask DisposeAsync()
    {
        LeaveChat();
        _client.OnMessageReceived -= OnMessageReceived;
        _client.OnError -= OnError;
        await _client.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
