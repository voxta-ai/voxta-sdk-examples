using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Voxta.Abstractions.Connections;
using Voxta.Abstractions.Encryption;
using Voxta.Abstractions.Exporting.Import;
using Voxta.Abstractions.Model;
using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Repositories;
using Voxta.Abstractions.Services.ChatBridges;
using Voxta.Modules.Examples.GameExample.Configuration;

namespace Voxta.Modules.Examples.GameExample.Game;

// The endpoint the game mod talks to: localhost only, JSON in and out, one conversation at a time.
//   POST /talk   {"characterId": "..."}   starts a conversation, ending the one before
//   POST /say    {"text": "..."}          what the player did or said, when not through Voxta's microphone
//   POST /scene  {"text": "..."}          what the character can see now
//   POST /played {"messageId": "..."}     the reply's audio finished playing
//   POST /interrupt, POST /bye
//   GET  /events                          lines, actions and reply ends since the last poll
public class GameBridgeServer(
    IUnitOfWorkFactory unitOfWorkFactory,
    IUserKeyManager userKeyManager,
    ILocalChatSessionHost chatSessionHost,
    IAppResourceDeployer deployer,
    IHostApplicationLifetime lifetime,
    ILogger<GameBridgeServer> logger
) : BackgroundService, IChatBridge
{
    public const int Port = 5390;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string AudioFolder = Path.Combine(Path.GetTempPath(), "VoxtaGameExample");

    // Requests one at a time: a game polls, and nothing here is worth racing.
    private readonly SemaphoreSlim _lock = new(1, 1);
    private GameConversation? _conversation;
    private bool _listening;
    private bool _stopped;

    public string ServiceName => VoxtaModule.ServiceName;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Hosted services start before the database is created and migrated.
        var started = new TaskCompletionSource();
        await using (lifetime.ApplicationStarted.Register(() => started.TrySetResult()))
            await started.Task.WaitAsync(stoppingToken);

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{Port}/");
        try
        {
            listener.Start();
        }
        catch (HttpListenerException exc)
        {
            logger.LogError(exc, "Could not listen on port {Port}; is it in use?", Port);
            return;
        }

        _listening = true;
        await using var stop = stoppingToken.Register(listener.Stop);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync();
                await HandleAsync(context, stoppingToken);
            }
        }
        catch (Exception) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        finally
        {
            _listening = false;
            await EndAsync();
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var body = context.Request.HasEntityBody
                ? await JsonSerializer.DeserializeAsync<GameRequest>(context.Request.InputStream, Json, cancellationToken) ?? new()
                : new GameRequest();
            object? response = (context.Request.HttpMethod, context.Request.Url?.AbsolutePath) switch
            {
                ("POST", "/talk") => await TalkAsync(body.CharacterId, cancellationToken),
                ("GET", "/events") => _conversation?.TakeEvents() ?? [],
                ("POST", "/say") => await With(c => c.SayAsync(body.Text ?? "")),
                ("POST", "/scene") => await With(c => c.SetSceneAsync(body.Text ?? "")),
                ("POST", "/played") => await With(c => c.PlayedAsync(body.MessageId)),
                ("POST", "/interrupt") => await With(c => c.InterruptAsync()),
                ("POST", "/bye") => await EndAsync(),
                _ => null,
            };
            context.Response.StatusCode = response == null ? 404 : 200;
            if (response != null) await JsonSerializer.SerializeAsync(context.Response.OutputStream, response, Json, cancellationToken);
        }
        catch (Exception exc) when (exc is not OperationCanceledException)
        {
            // Told to the game: a mod author reads its log, not Voxta's.
            logger.LogWarning(exc, "Game request failed");
            context.Response.StatusCode = 500;
            await JsonSerializer.SerializeAsync(context.Response.OutputStream, new { error = exc.Message }, Json, cancellationToken);
        }
        finally
        {
            context.Response.Close();
            _lock.Release();
        }
    }

    private async Task<object> TalkAsync(Guid characterId, CancellationToken cancellationToken)
    {
        if (_stopped) throw new InvalidOperationException("Stopped from the Chat Bridges page");
        await EndAsync();
        var module = await FindModuleAsync(cancellationToken)
            ?? throw new InvalidOperationException("Enable the Game Integration example module in Voxta, and sign in if the account is encrypted");
        // The one named, else the one picked in the settings, else the module's own.
        if (characterId == Guid.Empty)
        {
            var settings = new StaticSettingsSource(module.Configuration);
            if (!Guid.TryParse(settings.GetOptional(ModuleConfigurationProvider.CharacterId), out characterId))
                characterId = GameConversation.CompanionId;
        }
        _conversation = await GameConversation.StartAsync(chatSessionHost, deployer, module.UserId, characterId, AudioFolder, logger, cancellationToken);
        return new { ok = true };
    }

    private async Task<object> With(Func<GameConversation, Task> action)
    {
        if (_conversation is not { Closed: false } conversation) throw new InvalidOperationException("No conversation: POST /talk first");
        await action(conversation);
        return new { ok = true };
    }

    private async Task<object> EndAsync()
    {
        if (_conversation != null) await _conversation.DisposeAsync();
        _conversation = null;
        return new { ok = true };
    }

    // The account the game plays as: the one that enabled the module. A game has nowhere to sign in, so a locked
    // (encrypted, signed out) account cannot be used until its owner signs in to Voxta.
    private async Task<Module?> FindModuleAsync(CancellationToken cancellationToken)
    {
        // The system unit of work reads plaintext columns only: which accounts have the module, not its settings.
        var modules = await unitOfWorkFactory.ReadSystemAsync(x => x.Modules.GetEnabledModulesForAllUsersAsync(cancellationToken));
        foreach (var module in modules.Where(m => m.ServiceName == ServiceName))
            if (await userKeyManager.IsContentReadableAsync(module.UserId, cancellationToken))
                return await unitOfWorkFactory.ReadAsync(module.UserId, x => x.Modules.GetByIdAsync(module.Id, cancellationToken));
        return null;
    }

    public async Task<IReadOnlyList<ChatBridgeStatus>> GetStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var modules = await unitOfWorkFactory.ReadAsync(userId, x => x.Modules.GetManyByServiceNameAsync(ServiceName, cancellationToken));
        var chats = _conversation is { Closed: false } ? 1 : 0;
        return
        [
            .. modules.Select(module => new ChatBridgeStatus
            {
                ModuleId = module.Id,
                Running = _listening && !_stopped,
                Chats = chats,
                Detail = !_listening ? $"Not listening: port {Port} is in use."
                    : _stopped ? "Stopped."
                    : chats > 0 ? "In a conversation." : $"Waiting for the game on port {Port}.",
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
            _stopped = !running;
            if (_stopped) await EndAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed class GameRequest
    {
        public Guid CharacterId { get; init; }
        public Guid MessageId { get; init; }
        public string? Text { get; init; }
    }
}
