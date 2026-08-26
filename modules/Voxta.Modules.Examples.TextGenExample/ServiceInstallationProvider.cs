using Voxta.Abstractions.Model;
using Voxta.Abstractions.Repositories;
using Voxta.Modules.Examples.TextGenExample.Configuration;
using Voxta.Shared.LLMUtils;

namespace Voxta.Modules.Examples.TextGenExample;

/// <summary>
/// Runs when the user adds the module, to write the presets it ships with.
/// <para>
/// <see cref="PresetsInstallationProviderBase"/> does the writing. It is worth using rather than
/// inserting the rows yourself, because the job is not "create some presets": this also runs on
/// reinstall and on app updates, so it has to leave a preset the user has since tuned alone while
/// still pointing every services configuration set at one that exists. Getting that wrong is
/// invisible until somebody's settings are quietly reset.
/// </para>
/// <para>
/// Override <see cref="PresetsInstallationProviderBase.InstallSharedResourcesAsync"/> as well if
/// the module has something to download — a model, a tokenizer, a native library. It belongs there
/// rather than beside the presets, so that two installs of the module do not each fetch their own
/// copy. <see cref="PresetsInstallationProviderBase.GetPythonDependencies"/> is the other hook, for
/// packages to install into the module's Python environment.
/// </para>
/// </summary>
public class ServiceInstallationProvider(IUnitOfWorkFactory unitOfWorkFactory)
    : PresetsInstallationProviderBase(unitOfWorkFactory)
{
    protected override IEnumerable<ServiceSettings> GetBuiltInPresets() => BuiltInPresets.All();
}
