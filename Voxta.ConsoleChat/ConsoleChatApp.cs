using Voxta.Client;
using Voxta.Model;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat;

public class ConsoleChatApp(
    IVoxtaWebsocketsClientFactory voxtaWebsocketsClientFactory,
    VoxtaTokenProvider voxtaWebsocketsClientTokenProvider,
    IVoxtaApiClientFactory voxtaApiClientFactory,
    IDeviceAuthorization deviceAuthorization,
    ITokenStorage tokenStorage,
    IConsoleOutput output,
    ConsoleChatSessionFactory consoleChatSessionFactory,
    IRecordingManager recordingManager,
    IPlaybackManagerFactory playbackManagerFactory
    ) : IAsyncDisposable
{
    private const string ClientName = "Voxta.ConsoleChat";

    private readonly IVoxtaWebsocketsClient _voxtaWebsocketsClient = CreateVoxtaWebsocketsClientClient(voxtaWebsocketsClientFactory, voxtaWebsocketsClientTokenProvider);
    private readonly IPlaybackManager _playbackManager = playbackManagerFactory.Create(voxtaApiClientFactory.Create(voxtaWebsocketsClientTokenProvider));

    private CancellationToken _cancellationToken = CancellationToken.None;
    private ConsoleChatSession? _chat;

    private static IVoxtaWebsocketsClient CreateVoxtaWebsocketsClientClient(
        IVoxtaWebsocketsClientFactory voxtaWebsocketsClientFactory,
        VoxtaTokenProvider voxtaTokenProvider
    )
    {
        var voxtaWebsocketsClient = voxtaWebsocketsClientFactory.Create(
            new ClientAuthenticateMessage
            {
                Client = ClientName,
                ClientVersion = "1.0",
                Scope = [ClientScopes.AppRole],
                Capabilities = new ClientCapabilities
                {
                    AudioOutput = AudioOutputClientCapabilities.Url,
                    AcceptedAudioContentTypes = NAudioPlaybackManager.SupportedAudioContentTypes,
                    AudioInput = AudioInputClientCapabilities.WebSocketStream,
                    VisionCapture = VisionCaptureClientCapabilities.None,
                }
            },
            voxtaTokenProvider
        );
        return voxtaWebsocketsClient;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _voxtaWebsocketsClient.OnMessageReceived += OnMessageReceived;
        _voxtaWebsocketsClient.OnError += OnError;

        if (await AuthenticateAsync(cancellationToken)) return;

        _cancellationToken = cancellationToken;
        await _voxtaWebsocketsClient.ConnectAsync(cancellationToken);
        await recordingManager.ConnectAsync(voxtaWebsocketsClientTokenProvider);
        recordingManager.Start();
        while (true)
        {
            var line = Console.ReadLine();
            if (string.IsNullOrEmpty(line)) break;
            if(_chat == null)
            {
                output.WriteErrorLine("No chat session");
                continue;
            }
            _chat.SendMessage(new ClientSendMessage
            {
                SessionId = _chat.SessionId,
                Text = line,
                DoReply = true,
                DoUserActionInference = true,
                DoCharacterActionInference = false,
            });
        }
        output.WriteLine("Exiting...");
        await _voxtaWebsocketsClient.DisposeAsync();
    }

    private async Task<bool> AuthenticateAsync(CancellationToken cancellationToken)
    {
        var token = tokenStorage.RetrieveToken();
        var tokenValid = await deviceAuthorization.ValidateTokenAsync(token, cancellationToken);
        if (!tokenValid)
        {
            var deviceCode = await deviceAuthorization.GetDeviceCodeAsync(ClientName, cancellationToken);
            // ReSharper disable RedundantStringInterpolation
            output.WriteLine($"You need to authenticate with Voxta Server.");
            output.WriteLine($"Copy this code and paste it in the browser.");
            output.WriteLine($"==============================");
            output.WriteLine($"Code: {deviceCode.UserCode}");
            output.WriteLine($"URL: {deviceCode.VerificationUrl}");
            output.WriteLine($"==============================");
            // ReSharper restore RedundantStringInterpolation
            
            output.WriteLine("Waiting for browser authentication...");
            try
            {
                token = await deviceAuthorization.WaitForTokenAsync(deviceCode, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return true;
            }
            catch (Exception exc)
            {
                output.WriteErrorLine(exc.Message);
                return true;
            }

            if (token == null) return true;
            
            output.WriteLine("Authentication successful!");
            tokenStorage.SaveToken(token);
        }
        voxtaWebsocketsClientTokenProvider.Token = token;
        return false;
    }

    private void OnMessageReceived(object? sender, ServerMessage e)
    {
        switch (e)
        {
            case ServerWelcomeMessage welcomeMessage:
                output.WriteLine($"Welcome, {welcomeMessage.User.Name}!", ConsoleColor.Green);
                if (welcomeMessage.Assistant == null)
                {
                    output.WriteErrorLine("No assistant character");
                }
                else
                {
                    _chat?.Dispose();
                    consoleChatSessionFactory.SendStartChatMessage(_voxtaWebsocketsClient, welcomeMessage.Assistant.Id);
                }
                break;
            case ServerErrorMessage errorMessage:
                output.WriteErrorLine($"Server error: {errorMessage.Message}");
                if (!string.IsNullOrEmpty(errorMessage.Details))
                {
                    output.WriteErrorLine("--- DETAILS ---");
                    output.WriteErrorLine(errorMessage.Details);
                }
                break;
            case ServerChatStartingMessage:
                output.WriteLine("Loading chat...", ConsoleColor.DarkGray);
                break;
            case ServerChatLoadingMessage:
                break;
            case ServerChatStartedMessage readyMessage:
                if (_chat != null)
                {
                    // Refreshing existing chat
                    if (readyMessage.ChatId == _chat.ChatId) return;
                    _chat.Dispose();
                }
                _chat = consoleChatSessionFactory.Create(_voxtaWebsocketsClient, _playbackManager, output, readyMessage);
                _chat.Ready();
                break;
            case ServerAuthenticationRequiredMessage:
                output.WriteErrorLine("You need to create your profile in Voxta first.");
                break;
            case ServerReplyGeneratingMessage:
                break;
            case ServerReplyStartMessage replyStartMessage:
                _chat?.OnReplyStart(replyStartMessage, _cancellationToken);
                break;
            case ServerReplyChunkMessage replyChunkMessage:
                _chat?.OnReplyChunk(replyChunkMessage);
                break;
            case ServerReplyEndMessage replyEndMessage:
                _chat?.OnReplyEnd(replyEndMessage);
                break;
            case ServerSpeechRecognitionStartMessage speechRecognitionStartMessage:
                _chat?.OnSpeechRecognitionStart(speechRecognitionStartMessage);
                break;
            case ServerSpeechRecognitionPartialMessage speechRecognitionPartialMessage:
                _chat?.OnSpeechRecognitionPartial(speechRecognitionPartialMessage);
                break;
            case ServerSpeechRecognitionEndMessage speechRecognitionEndMessage:
                _chat?.OnSpeechRecognitionEnd(speechRecognitionEndMessage);
                break;
            case ServerActionMessage actionMessage:
                _chat?.OnAction(actionMessage);
                break;
            case ServerRecordingRequestMessage recordingRequestMessage:
                if (recordingRequestMessage.Enabled)
                    recordingManager.Start();
                else
                    recordingManager.Stop();
                break;
            case ServerSpeechPlaybackStartMessage:
            case ServerSpeechPlaybackCompleteMessage:
            case ServerContextUpdatedMessage:
            case ServerMemoryUpdatedMessage:
            case ServerUpdatedMessage:
            case ServerRecordingStatusMessage:
            case ServerChatSessionsUpdatedMessage:
            case ServerChatFlowMessage:
            case ServerChatPausedMessage:
            case ServerChatParticipantsUpdatedMessage:
                break;
            default:
                output.WriteErrorLine($"Unhandled message type {e.GetType().Name}");
                break;
        }
    }

    private void OnError(object? _, Exception e)
    {
        output.WriteErrorLine(e.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        _chat?.Dispose();
        _voxtaWebsocketsClient.OnError -= OnError;
        _voxtaWebsocketsClient.OnMessageReceived -= OnMessageReceived;
        await _voxtaWebsocketsClient.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
