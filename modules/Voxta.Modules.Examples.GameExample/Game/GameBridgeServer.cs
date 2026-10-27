using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Voxta.Abstractions.Exporting.Import;
using Voxta.Abstractions.Modules;
using Voxta.Abstractions.Security;
using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Repositories;
using Voxta.Abstractions.Services.ChatBridges;
using Voxta.Modules.Examples.GameExample.Configuration;

namespace Voxta.Modules.Examples.GameExample.Game;

// What the game mod talks to, on Voxta's own port under /api/extensions/gameexample/: JSON in and out, one conversation
// at a time. The mod sends one of the player's API keys (Authorization: Bearer), and the conversation runs as its account.
//   POST talk   {"characterId": "..."}   starts a conversation, ending the one before
//   POST say    {"text": "..."}          what the player did or said, when not through Voxta's microphone
//   POST scene  {"text": "..."}          what the character can see now
//   POST played {"messageId": "..."}     the reply's audio finished playing
//   POST interrupt, POST bye
//   GET  events                          lines, actions and reply ends since the last poll
public class GameBridgeServer(
    IUnitOfWorkFactory unitOfWorkFactory,
    IUserContexts userContexts,
    IAppResourceDeployer deployer,
    ILogger<GameBridgeServer> logger
) : IModuleHttpEndpoints, IChatBridge, IAsyncDisposable
{
    public const string RoutePrefix = "gameexample";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string AudioFolder = Path.Combine(Path.GetTempPath(), "VoxtaGameExample");

    // Requests one at a time: a game polls, and nothing here is worth racing.
    private readonly SemaphoreSlim _lock = new(1, 1);
    private GameConversation? _conversation;
    // The accounts that pressed Stop on the Chat Bridges page: each account's own, so one doesn't end another's game.
    private readonly ConcurrentDictionary<Guid, bool> _stopped = new();

    string IModuleHttpEndpoints.RoutePrefix => RoutePrefix;

    // A game has nowhere to sign in: it calls with a key (pasted in its config, or from Voxta's device authorization).
    public ModuleHttpAccess Access => ModuleHttpAccess.ApiKey;

    public string ServiceName => VoxtaModule.ServiceName;

    public async Task<ModuleHttpResponse?> HandleAsync(ModuleHttpRequest request, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            using var reader = new StreamReader(request.Body);
            var text = await reader.ReadToEndAsync(cancellationToken);
            var body = string.IsNullOrWhiteSpace(text) ? new GameRequest() : JsonSerializer.Deserialize<GameRequest>(text, Json) ?? new();
            // Only the account that started it may drive the conversation; another key starts its own.
            object? response = (request.Method, request.Path) switch
            {
                ("POST", "talk") => await TalkAsync(request.UserId, body.CharacterId, cancellationToken),
                ("GET", "events") => Mine(request.UserId)?.TakeEvents() ?? [],
                ("POST", "say") => await With(request.UserId, c => c.SayAsync(body.Text ?? "")),
                ("POST", "scene") => await With(request.UserId, c => c.SetSceneAsync(body.Text ?? "")),
                ("POST", "played") => await With(request.UserId, c => c.PlayedAsync(body.MessageId)),
                ("POST", "interrupt") => await With(request.UserId, c => c.InterruptAsync()),
                ("POST", "bye") => Mine(request.UserId) != null ? await EndAsync() : new { ok = true },
                _ => null,
            };
            return response == null ? null : ModuleHttpResponse.Json(response);
        }
        catch (Exception exc) when (exc is not OperationCanceledException)
        {
            // Told to the game: a mod author reads its log, not Voxta's.
            logger.LogWarning(exc, "Game request failed");
            return new ModuleHttpResponse(500, "application/json", JsonSerializer.Serialize(new { error = exc.Message }, Json));
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<object> TalkAsync(Guid userId, Guid characterId, CancellationToken cancellationToken)
    {
        if (_stopped.ContainsKey(userId)) throw new InvalidOperationException("Stopped from the Chat Bridges page");
        await EndAsync();
        // The account the key belongs to, with its settings for this module.
        var user = await userContexts.ForModuleAsync(ServiceName, userId, cancellationToken)
            ?? throw new InvalidOperationException("Enable the Game Integration example module on the account of this API key");
        // The one named, else the one picked in the settings, else the module's own.
        if (characterId == Guid.Empty && !Guid.TryParse(user.Settings.GetOptional(ModuleConfigurationProvider.CharacterId), out characterId))
            characterId = GameConversation.CompanionId;
        _conversation = await GameConversation.StartAsync(user, deployer, characterId, AudioFolder, logger, cancellationToken);
        return new { ok = true };
    }

    private GameConversation? Mine(Guid userId) => _conversation is { Closed: false } c && c.UserId == userId ? c : null;

    private async Task<object> With(Guid userId, Func<GameConversation, Task> action)
    {
        if (Mine(userId) is not { } conversation) throw new InvalidOperationException("No conversation: POST talk first");
        await action(conversation);
        return new { ok = true };
    }

    private async Task<object> EndAsync()
    {
        if (_conversation != null) await _conversation.DisposeAsync();
        _conversation = null;
        return new { ok = true };
    }

    public async Task<IReadOnlyList<ChatBridgeStatus>> GetStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var modules = await unitOfWorkFactory.ReadAsync(userId, x => x.Modules.GetManyByServiceNameAsync(ServiceName, cancellationToken));
        var chats = Mine(userId) != null ? 1 : 0;
        return
        [
            .. modules.Select(module => new ChatBridgeStatus
            {
                ModuleId = module.Id,
                Running = !_stopped.ContainsKey(userId),
                Chats = chats,
                Detail = _stopped.ContainsKey(userId) ? "Stopped."
                    : chats > 0 ? "In a conversation." : $"Waiting for the game on /api/extensions/{RoutePrefix}/.",
            }),
        ];
    }

    public async Task SetRunningAsync(Guid userId, Guid moduleId, bool running, CancellationToken cancellationToken)
    {
        // The controller checked the module is this account's. Check anyway: the id is all this is given.
        var module = await unitOfWorkFactory.ReadAsync(userId, x => x.Modules.GetByIdAsync(moduleId, cancellationToken));
        if (module?.ServiceName != ServiceName) return;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (running) _stopped.TryRemove(userId, out _);
            else if (_stopped.TryAdd(userId, true) && Mine(userId) != null) await EndAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync() => await EndAsync();

    private sealed class GameRequest
    {
        public Guid CharacterId { get; init; }
        public Guid MessageId { get; init; }
        public string? Text { get; init; }
    }
}
