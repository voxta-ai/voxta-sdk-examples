using Microsoft.Extensions.Logging;
using Voxta.Abstractions.Configuration;
using Voxta.Abstractions.Diagnostics;
using Voxta.Abstractions.ModuleRuntimeInstances;
using Voxta.Abstractions.Services;
using Voxta.Shared.LLMUtils.Testing;

namespace Voxta.Modules.Examples.TextGenExample;

/// <summary>
/// Backs the Test button on the module's page. Declare it as
/// <c>ModuleTestingProviderType</c> in the module definition and the user gets a way to find out
/// whether their settings actually work, without starting a chat.
/// <para>
/// <see cref="TextGenModuleTestingProviderBase"/> is the whole implementation for a module that only
/// offers text generation: it resolves the user's configuration, creates the service through the
/// normal path, asks it a question with a known answer, and reports back. A module offering several
/// service types composes instead — implement <c>IVoxtaModuleTestingProvider</c> and call
/// <see cref="VoxtaModuleTestingTextGen"/> alongside the other testers.
/// </para>
/// </summary>
public class ModuleTestingProvider(
    IServiceFactory serviceFactory,
    IInferenceLoggersManager inferenceLoggersManager,
    IServicesConfigurationsSetResolver servicesConfigurationsSetResolver,
    IModuleRuntimeInstancesManager moduleRuntimeInstancesManager,
    ILogger<ModuleTestingProvider> logger
) : TextGenModuleTestingProviderBase(
    serviceFactory,
    inferenceLoggersManager,
    servicesConfigurationsSetResolver,
    moduleRuntimeInstancesManager,
    logger
);
