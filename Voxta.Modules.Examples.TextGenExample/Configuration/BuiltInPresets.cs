using Voxta.Abstractions.Model;
using Voxta.Abstractions.Registration;
using Voxta.Model.Shared;

namespace Voxta.Modules.Examples.TextGenExample.Configuration;

/// <summary>
/// Presets shipped with the module, so a user who just installed it has something selected
/// instead of an empty dropdown. Created once by <see cref="ServiceInstallationProvider"/>.
///
/// Only fields that differ from their declared default are worth listing: an unset field falls
/// back to the default declared on it, so repeating those here only creates two places to change.
/// </summary>
public static class BuiltInPresets
{
    public static ServiceSettings[] All() =>
    [
        Default(),
    ];

    private static ServiceSettings Default() => ServiceSettingsBuilder.Build(
        ServiceTypes.TextGen,
        // The label shown until the user renames the preset.
        "Echo",
        // GetKVP turns a field and a value into the key/value pair the preset stores, so the
        // field's name is never spelled out as a string.
        ServiceSettingsProvider.MaxNewTokens.GetKVP(120)
    );
}
