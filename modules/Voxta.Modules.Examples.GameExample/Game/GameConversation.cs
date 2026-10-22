using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Voxta.Abstractions.Connections;
using Voxta.Abstractions.Exporting.Import;
using Voxta.Abstractions.Model;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.Modules.Examples.GameExample.Game;

// What the game picks up from GET /events: a line to voice, an action to act out, or the end of a reply.
[Serializable]
public sealed record GameEvent(string Type, Guid MessageId, string? Text = null, string? AudioPath = null, string? Arguments = null);

// One conversation with one character. The game sends what the player does, voices the lines and acts out the tools.
public sealed class GameConversation : IAsyncDisposable
{
    private const string SceneContextKey = "game_scene";
    private const string WaveTool = "wave";

    // The module's own: a fixed id, versioned, owned by this module. Bump the version when the text changes.
    private static readonly Guid ScenarioId = new("3f0c6c1e-5b7a-4c39-9a52-6a1f1d0b2e47");

    // Mira, the character the module ships (ServiceInstallationProvider): who answers when nobody else is picked.
    public static readonly Guid CompanionId = new("8d4b2f60-1c3e-4a7b-9f15-6e2a0c9d7b31");

    private readonly ILocalChatSession _session;
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<GameEvent> _events = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _reader;

    private GameConversation(ILocalChatSession session, ILogger logger)
    {
        _session = session;
        _logger = logger;
        _reader = ReadAsync(_cts.Token);
    }

    public bool Closed => _reader.IsCompleted;

    public static async Task<GameConversation> StartAsync(
        ILocalChatSessionHost host,
        IAppResourceDeployer deployer,
        Guid userId,
        Guid characterId,
        string audioFolder,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        // Written the first time, updated when the version changes, left alone once the user edits it. Characters ship
        // in a .voxpak instead (ServiceInstallationProvider); a scenario can also be built in code, like this one.
        var scenario = new Scenario
        {
            Name = "Game",
            Client = VoxtaModule.ServiceName,
            Version = "1.0.0",
            Template = "{{ char }} and {{ user }} meet in the game world. {{ char }} speaks briefly, as people do face to face.",
        };
        scenario.SetPublishedId(ScenarioId);
        await deployer.DeployScenarioAsync(userId, scenario, cancellationToken);

        var session = await host.StartAsync(new LocalChatSessionRequest
        {
            UserId = userId,
            Client = VoxtaModule.ServiceName,
            Capabilities = new ClientCapabilities
            {
                // Wav files in a folder the game reads: it plays them on the speaker and lip syncs from them.
                AudioOutput = AudioOutputClientCapabilities.LocalFile,
                AudioFolder = audioFolder,
                AcceptedAudioContentTypes = ["audio/x-wav"],
                // The player talks into Voxta's own microphone, not the game.
                AudioInput = AudioInputClientCapabilities.None,
                VisionCapture = VisionCaptureClientCapabilities.Disabled,
            },
            Start = new ClientStartChatMessage
            {
                CharacterId = characterId,
                ScenarioId = ScenarioId,
                // A conversation in passing: not kept in the chats list.
                Ephemeral = true,
                Tools =
                [
                    new ClientToolDefinition
                    {
                        Name = WaveTool,
                        Description = "Wave at the player.",
                        // Only changes the game. Anything else is refused: nobody is at a keyboard to approve it.
                        Permission = ToolPermissionClass.InChat,
                        Display = ToolDisplayMode.Hidden,
                    },
                ],
            },
        }, cancellationToken);
        return new GameConversation(session, logger);
    }

    // Everything since the last call, in order.
    public GameEvent[] TakeEvents()
    {
        var events = new List<GameEvent>();
        while (_events.TryDequeue(out var e)) events.Add(e);
        return [.. events];
    }

    public Task SayAsync(string text) =>
        _session.SendAsync(new ClientSendMessage { SessionId = Guid.Empty, Text = text });

    // What the character can see right now. Replaces the last one rather than piling up in the chat.
    public Task SetSceneAsync(string text) =>
        _session.SendAsync(new ClientUpdateContextMessage
        {
            SessionId = Guid.Empty,
            ContextKey = SceneContextKey,
            Contexts = [ContextDefinition.Single(text)],
        });

    // The player walked off or started talking: stop the reply and its voice.
    public Task InterruptAsync() =>
        _session.SendAsync(new ClientInterruptMessage { SessionId = Guid.Empty });

    // Required, once the game has played the reply: the chat waits for it before the next one.
    public Task PlayedAsync(Guid messageId) =>
        _session.SendAsync(new ClientSpeechPlaybackCompleteMessage { SessionId = Guid.Empty, MessageId = messageId });

    private async Task ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Read everything: the channel is unbounded, so a reader that stops leaks.
            await foreach (var message in _session.Messages.ReadAllAsync(cancellationToken))
            {
                switch (message)
                {
                    case ServerReplyChunkMessage chunk:
                        // With LocalFile, the audio "url" is the wav's path.
                        _events.Enqueue(new GameEvent("line", chunk.MessageId, chunk.Text, chunk.AudioUrl));
                        break;

                    case ServerReplyEndMessage end:
                        _events.Enqueue(new GameEvent("end", end.MessageId));
                        break;

                    case ServerToolCallRequestMessage call:
                        _events.Enqueue(new GameEvent("action", Guid.Empty, call.Name, Arguments: call.Arguments));
                        // Answered now: the model is waiting, and the game acts it out on its own time.
                        await _session.SendAsync(new ClientToolCallResultMessage
                        {
                            SessionId = Guid.Empty,
                            RequestId = call.RequestId,
                            Result = "Done. It is happening in the game; you have already said your line.",
                        });
                        break;

                    case ServerErrorMessage error:
                        _logger.LogWarning("Game conversation error: {Message}", error.Message);
                        break;

                    case ServerChatClosedMessage:
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Disposing.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        // Ends the chat, the same teardown as a websocket disconnect.
        await _session.DisposeAsync();
        await _reader;
        _cts.Dispose();
    }
}
