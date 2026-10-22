using Voxta.Abstractions.Model;
using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Repositories;
using Voxta.Abstractions.Services.Launchers;
using Voxta.Modules.Examples.GameExample.Configuration;
using Voxta.Shared.ExternalProcesses;

namespace Voxta.Modules.Examples.GameExample.Game;

/// <summary>
/// Puts the game in the Play library: found through the stores (Steam, GOG, Epic), or the executable in the module's
/// settings for a copy no store knows. Play starts the bridge first, so the game finds it listening.
/// </summary>
public class GameLauncher(IUnitOfWorkFactory unitOfWorkFactory, IInstalledGames installedGames) : ILauncher
{
    // Portal 2 stands in for your game: its id in each store (the first one installed wins), and its process.
    private static readonly string[] StoreKeys = ["steam:620"];
    private const string GameProcess = "portal2.exe";

    public string ServiceName => VoxtaModule.ServiceName;

    // Exactly 16:9; the game's official art reads best. A store that keeps its own copy of the art is shown instead.
    public ModuleLogo Cover { get; } = ModuleLogo.EmbeddedResource(typeof(VoxtaModule), "Assets.Cover.svg");

    // The game talks to this module's own bridge.
    public string ChatBridgeServiceName => VoxtaModule.ServiceName;

    public async Task<IReadOnlyList<LauncherStatus>> GetStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var modules = await unitOfWorkFactory.ReadAsync(userId, x => x.Modules.GetManyByServiceNameAsync(ServiceName, cancellationToken));
        return [.. modules.Select(Status)];
    }

    private LauncherStatus Status(Module module)
    {
        if (GamePath(module) is { } path)
        {
            if (!File.Exists(path))
                return new LauncherStatus { ModuleId = module.Id, Running = false, CanLaunch = false, NotFound = true, Detail = "Nothing is at the game's path any more." };
            var running = ExternalProgram.IsRunning(path);
            return new LauncherStatus { ModuleId = module.Id, Running = running, CanLaunch = true, Detail = running ? "Running." : null };
        }

        if (installedGames.Find(StoreKeys) is { } game)
        {
            var running = IsRunning(game);
            return new LauncherStatus
            {
                ModuleId = module.Id,
                Running = running,
                CanLaunch = true,
                Detail = running ? "Running." : $"Installed by {game.Store}.",
                CoverFile = game.CoverFile,
            };
        }

        return new LauncherStatus
        {
            ModuleId = module.Id,
            // Started by hand from somewhere Voxta does not know: it still shows as running.
            Running = ExternalProgram.IsRunning(GameProcess, null),
            CanLaunch = false,
            Detail = "Not found on Steam, GOG or Epic. Set the game's executable in the settings.",
        };
    }

    public async Task LaunchAsync(Guid userId, Guid moduleId, CancellationToken cancellationToken)
    {
        var module = await unitOfWorkFactory.ReadAsync(userId, x => x.Modules.GetByIdAsync(moduleId, cancellationToken));
        if (module?.ServiceName != ServiceName) throw new InvalidOperationException("Not a Game Integration example module.");
        if (GamePath(module) is { } path)
        {
            if (!ExternalProgram.IsRunning(path)) ExternalProgram.Start(path);
            return;
        }
        var game = installedGames.Find(StoreKeys)
            ?? throw new InvalidOperationException("The game was not found on Steam, GOG or Epic. Set its executable in the module's settings.");
        if (!IsRunning(game)) installedGames.Launch(game);
    }

    // What the store says (Steam knows, far cheaper than listing processes); else the game's process under its folder.
    private bool IsRunning(InstalledGame game) => installedGames.IsRunning(game) ?? ExternalProgram.IsRunning(GameProcess, game.Folder);

    private static string? GamePath(Module module)
    {
        var path = new StaticSettingsSource(module.Configuration).GetOptional(ModuleConfigurationProvider.GamePath)?.Trim().Trim('"');
        return string.IsNullOrEmpty(path) ? null : path;
    }
}
