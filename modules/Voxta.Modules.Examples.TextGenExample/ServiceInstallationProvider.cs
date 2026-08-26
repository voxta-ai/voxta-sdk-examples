using Voxta.Abstractions.Model;
using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Repositories;
using Voxta.Abstractions.Security;
using Voxta.Common.Reporting;
using Voxta.Model.Shared;
using Voxta.Modules.Examples.TextGenExample.Configuration;

namespace Voxta.Modules.Examples.TextGenExample;

/// <summary>
/// Runs when the user adds the module.
/// </summary>
public class ServiceInstallationProvider(IUnitOfWorkFactory unitOfWorkFactory) : IServiceInstallationProvider
{
    /// <summary>Python packages to install in the module's environment.</summary>
    public IEnumerable<string> GetPythonDependencies() => [];

    /// <summary>
    /// Called once per installed instance. It also runs again when an existing instance is
    /// reconfigured, so it must be safe to repeat.
    /// </summary>
    public async Task ConfigureModuleAsync(
        IAuthenticationContext auth,
        Module module,
        IDeferredReporter reporter,
        CancellationToken cancellationToken
    )
    {
        // Database access with decryption for this user.
        await using var uow = unitOfWorkFactory.CreateUnitOfWork(auth.UserId);

        // Presets the user has since edited or deleted are theirs; only seed an empty slate.
        var existing = await uow.ServiceSettings.GetManyByServiceTypeAsync(module.Id, ServiceTypes.TextGen, cancellationToken);
        if (existing.Length > 0) return;

        foreach (var preset in BuiltInPresets.All())
        {
            // The builder leaves the identity blank because it does not know the user or module.
            preset.UserId = auth.UserId;
            preset.ModuleId = module.Id;
            preset.Id = Guid.NewGuid();
            await uow.ServiceSettings.CreateAsync(preset);
        }

        await uow.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Files shared with other modules, such as a tokenizer or a native library. Downloading a
    /// model belongs here rather than in <see cref="ConfigureModuleAsync"/>, so that two instances
    /// of the module do not each fetch their own copy.
    /// </summary>
    public Task InstallSharedResourcesAsync(
        IAuthenticationContext auth,
        ISettingsSource settings,
        IDeferredReporter reporter,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;
}
