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
    private readonly IConsoleAudioOutput _audioOutput;
    private readonly SpeechPlayback _speech;
    private readonly VoxtaAppDeployer _deployer;
    private readonly ConsoleScenario _scenario;
    private readonly ChatFunctions _functions;
    private readonly ChatSessionFactory _sessionFactory;
    private readonly ILastChatStore _lastChatStore;
    private readonly ILogger<VoxtaConnection> _logger;
    private readonly ServerMessageDispatcher _dispatcher;

    private readonly TaskCompletionSource _welcome = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource<ServerChatStartedMessage>? _pendingStart;
    private IProgress<LoadingUpdate>? _loadingProgress;
    private Guid? _scenarioId;
    private ChatSession? _session;
    private CancellationToken _cancellationToken = CancellationToken.None;

    public VoxtaConnection(
        IVoxtaWebsocketsClientFactory clientFactory,
        VoxtaTokenProvider tokenProvider,
        IDeviceAuthorization deviceAuthorization,
        ITokenStore tokenStore,
        IAudioRecorder recorder,
        IConsoleAudioOutput audioOutput,
        ChatFunctions functions,
        ChatSessionFactory sessionFactory,
        ILastChatStore lastChatStore,
        ILogger<VoxtaConnection> logger)
    {
        _tokenProvider = tokenProvider;
        _deviceAuthorization = deviceAuthorization;
        _tokenStore = tokenStore;
        _recorder = recorder;
        _audioOutput = audioOutput;
        _functions = functions;
        _sessionFactory = sessionFactory;
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
                    AcceptedAudioContentTypes = VlcAudioOutput.SupportedContentTypes,
                    AudioInput = AudioInputClientCapabilities.WebSocketStream,
                    VisionCapture = VisionCaptureClientCapabilities.None,
                },
            },
            tokenProvider);

        _speech = new SpeechPlayback(_client, _audioOutput);
        _deployer = new VoxtaAppDeployer(_client);
        _scenario = new ConsoleScenario(_deployer);
        _dispatcher = BuildDispatcher();

        _client.OnMessageReceived += OnMessageReceived;
        _client.OnError += OnError;
        _client.OnDisconnected += OnDisconnected;
        _client.OnReconnected += OnReconnected;
    }

    private ServerMessageDispatcher BuildDispatcher() => new ServerMessageDispatcher()
        .On<ServerWelcomeMessage>(_ => _welcome.TrySetResult())
        .On<ServerAuthenticationRequiredMessage>(_ => _welcome.TrySetException(new InvalidOperationException("You need to create your profile in Voxta first.")))
        .On<ServerChatStartingMessage>(_ => _loadingProgress?.Report(new LoadingUpdate(0, "Starting chat...")))
        .On<ServerChatLoadingMessage>(m => _loadingProgress?.Report(new LoadingUpdate(m.Progress, m.Text)))
        .On<ServerChatStartedMessage>(m => _pendingStart?.TrySetResult(m))
        .On<ServerActionMessage>(m => _session?.OnAction(m))
        .On<ServerReplyStartMessage>(m => _session?.OnReplyStart(m, _cancellationToken))
        .On<ServerReplyChunkMessage>(m => _session?.OnReplyChunk(m))
        .On<ServerReplyEndMessage>(m => _session?.OnReplyEnd(m))
        .On<ServerReplyCancelledMessage>(m => _session?.OnReplyCancelled(m))
        .On<ServerReplyToolCallMessage>(m => _session?.OnToolCall(m))
        .On<ServerToolPermissionRequestMessage>(m => _session?.OnToolPermissionRequest(m))
        .On<ServerSpeechRecognitionStartMessage>(m => _session?.OnSpeechRecognitionStart(m))
        .On<ServerSpeechRecognitionPartialMessage>(m => _session?.OnSpeechRecognitionPartial(m))
        .On<ServerSpeechRecognitionEndMessage>(m => _session?.OnSpeechRecognitionEnd(m))
        .On<ServerUpdatedMessage>(m => _session?.OnServerUpdated(m))
        .On<ServerChatFlowMessage>(m => _session?.OnChatFlow(m))
        .On<ServerRecordingRequestMessage>(m => { if (m.Enabled) _recorder.Start(); else _recorder.Stop(); })
        .On<ServerChatSessionErrorMessage>(m => _session?.OnChatError(m.Message))
        .On<ServerErrorMessage>(m =>
        {
            _logger.LogError("Server error: {Message} {Details}", m.Message, m.Details);
            _pendingStart?.TrySetException(new InvalidOperationException(m.Message));
        });

    private void OnDisconnected(object? sender, EventArgs e) => _session?.SetConnected(false);

    private void OnReconnected(object? sender, EventArgs e) => _session?.SetConnected(true);

    public async Task ConnectAsync(IProgress<ConnectionProgress> progress, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        await AuthenticateAsync(progress, cancellationToken);

        progress.Report(new ConnectionProgress("Connecting..."));
        await _client.ConnectAsync(cancellationToken);
        await _welcome.Task.WaitAsync(cancellationToken);

        progress.Report(new ConnectionProgress("Publishing scenario..."));
        await EnsureScenarioAsync(cancellationToken);

        await _recorder.ConnectAsync(_tokenProvider);
        _recorder.Start();
        progress.Report(new ConnectionProgress("Connected"));
    }

    private async Task EnsureScenarioAsync(CancellationToken cancellationToken)
    {
        try
        {
            _deployer.RegisterApp("Voxta Console Chat", clientVersion: "1.0");
            _scenarioId = await _scenario.PublishAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The chat falls back to an inline context if the scenario could not be published.
            _logger.LogWarning(exception, "Could not publish the console scenario");
        }
    }

    public Task<ChatSession> StartChatAsync(Guid characterId, IProgress<LoadingUpdate> progress, CancellationToken cancellationToken) =>
        StartAsync(new ClientStartChatMessage
        {
            CharacterId = characterId,
            ScenarioId = _scenarioId,
            Contexts = _scenarioId == null ? [new ContextDefinition { Text = TerminalContext }] : null,
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
            _session = _sessionFactory.Create(_client, _speech, started, _audioOutput.UnavailableReason);
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
        _session?.Stop();
        _session?.Dispose();
        _session = null;
    }

    private async Task AuthenticateAsync(IProgress<ConnectionProgress> progress, CancellationToken cancellationToken)
    {
        var token = _tokenStore.Load();
        if (!await _deviceAuthorization.ValidateTokenAsync(token, cancellationToken))
        {
            var code = await _deviceAuthorization.GetDeviceCodeAsync(ClientName, cancellationToken);
            progress.Report(new ConnectionProgress("Waiting for browser authentication...", code.UserCode, code.VerificationUrl));

            token = await _deviceAuthorization.WaitForTokenAsync(code, cancellationToken)
                ?? throw new InvalidOperationException("Authentication failed");
            _tokenStore.Save(token);
        }

        _tokenProvider.Token = token;
    }

    private void OnMessageReceived(object? sender, ServerMessage message) => _dispatcher.Dispatch(message);

    private void OnError(object? sender, Exception exception) => _logger.LogError(exception, "Websocket error");

    public async ValueTask DisposeAsync()
    {
        LeaveChat();
        _client.OnMessageReceived -= OnMessageReceived;
        _client.OnError -= OnError;
        _client.OnDisconnected -= OnDisconnected;
        _client.OnReconnected -= OnReconnected;
        await _client.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
