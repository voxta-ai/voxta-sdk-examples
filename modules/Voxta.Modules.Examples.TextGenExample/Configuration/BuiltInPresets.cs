using Voxta.Abstractions.Model;
using Voxta.Abstractions.Registration;
using Voxta.Model.Shared;
using Voxta.Shared.LLMUtils.Clients;

namespace Voxta.Modules.Examples.TextGenExample.Configuration;

/// <summary>
/// The presets the module ships with, written by <see cref="ServiceInstallationProvider"/> when the
/// user adds it.
/// <para>
/// Ship one that works out of the box, and one per genuinely different way of using the backend —
/// a fast preset and a quality preset, say. Do not ship one per field: the user can make their own.
/// </para>
/// </summary>
public static class BuiltInPresets
{
    public static ServiceSettings[] All() =>
    [
        Default(),
    ];

    private static ServiceSettings Default() => ServiceSettingsBuilder.Build(
        ServiceTypes.TextGen,
        // The label. Null falls back to ServiceSettingsProvider.GetDefaultLabel.
        null,
        // Only the values that should differ from the field defaults. Everything left out resolves
        // to the DefaultValue on the field itself, so a preset stays correct when a default moves.
        PromptingSettingsProvider.MaxLength.GetKVP(120)
    );
}
