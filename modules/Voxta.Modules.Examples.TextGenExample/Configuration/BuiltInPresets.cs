using Voxta.Abstractions.Model;
using Voxta.Abstractions.Registration;
using Voxta.Model.Shared;

namespace Voxta.Modules.Examples.TextGenExample.Configuration;

/// <summary>
/// Presets shipped with the module.
/// </summary>
public static class BuiltInPresets
{
    public static ServiceSettings[] All() =>
    [
        Default(),
    ];

    private static ServiceSettings Default() => ServiceSettingsBuilder.Build(
        ServiceTypes.TextGen,
        // The label is optional, by default it will use ServiceSettingsProvider.GetDefaultLabel
null,
        // Specify non-default values you want to override for this preset
        ServiceSettingsProvider.MaxNewTokens.GetKVP(120)
    );
}
