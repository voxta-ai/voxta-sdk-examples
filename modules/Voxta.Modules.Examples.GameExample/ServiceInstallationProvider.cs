using Voxta.Abstractions.Exporting.Import;
using Voxta.Abstractions.Model;
using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Security;
using Voxta.Common.Reporting;

namespace Voxta.Modules.Examples.GameExample;

// Runs when an account installs the module, and again when the module updates: the place for the characters, books and
// scenarios it ships. The account installing it is signed in, so its library can be written even when it is encrypted.
public class ServiceInstallationProvider(IAppResourceDeployer deployer) : IServiceInstallationProvider
{
    // Assets/Companion.voxpak, exported from Voxta.
    private const string CompanionPackage = "Companion.voxpak";

    public async Task ConfigureModuleAsync(IAuthenticationContext auth, Module module, IDeferredReporter reporter, CancellationToken cancellationToken)
    {
        // Writes what is missing or at an older version, keeps what the user edited, and writes nothing when all of it
        // is current. Give a resource a new version when you change it, or installed copies keep the old one.
        await using var package = typeof(ServiceInstallationProvider).Assembly.GetManifestResourceStream(CompanionPackage)
            ?? throw new InvalidOperationException($"{CompanionPackage} is not embedded");
        var result = await deployer.DeployPackageAsync(auth.UserId, package, cancellationToken);
        reporter.Info($"Mira: {result}");
    }

    // For files every account shares (models, tools); nothing here.
    public Task InstallSharedResourcesAsync(IAuthenticationContext auth, ISettingsSource settings, IDeferredReporter reporter,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
