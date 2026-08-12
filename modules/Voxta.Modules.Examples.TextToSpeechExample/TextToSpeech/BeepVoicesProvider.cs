using Voxta.Abstractions.Registration;
using Voxta.Abstractions.Security;
using Voxta.Abstractions.Services.TextToSpeech;
using Voxta.Model.Shared;
using Voxta.Model.Shared.Forms;

namespace Voxta.Modules.Examples.TextToSpeechExample.TextToSpeech;

/// <summary>
/// Lists the voices users can assign to a character. A voice is nothing more than a label and a
/// bag of string parameters; what those parameters mean is entirely up to your service. Here a
/// voice is a frequency.
/// </summary>
public class BeepVoicesProvider : IVoiceProvider
{
    /// <summary>
    /// Declared as a form field so both the voice form and the service read the parameter
    /// through the same definition, instead of repeating the "Frequency" string.
    /// </summary>
    public static readonly FormIntField Frequency = new()
    {
        Name = "Frequency",
        Label = "Frequency (Hz)",
        Text = "Pitch of the beeps.",
        DefaultValue = 440,
        Min = 50,
        Max = 4000,
    };

    /// <summary>
    /// Which parameters identify a voice. A character stores the voice it was assigned, and this
    /// is how it is matched back to a catalog entry after a rename. Without it, entries fall back
    /// to their label, which breaks as soon as a label changes.
    /// </summary>
    public string[] IdentityParameters => [Frequency.Name];

    /// <summary>
    /// Fields shown when a user edits the voice by hand instead of picking one from the catalog.
    /// </summary>
    public Task<FormField[]> GetFormFieldsAsync(
        IAuthenticationContext auth,
        ISettingsSource settings,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(FormBuilder.Build(Frequency));
    }

    /// <summary>
    /// The voice catalog. Real modules call their backend here (and can cache the result); this
    /// one just makes three up.
    /// </summary>
    public Task<VoicesList> GetVoicesAsync(
        IAuthenticationContext auth,
        ISettingsSource settings,
        Guid? characterId,
        string culture,
        bool includeAllVoices,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(new VoicesList
        {
            // Set this to true when the catalog was truncated; Voxta will then offer to load the
            // full list by calling back with includeAllVoices.
            HasMore = false,
            Voices =
            [
                CreateVoice("Beep Low", 220, VoiceGender.Male),
                CreateVoice("Beep Mid", 440, VoiceGender.Unspecified),
                CreateVoice("Beep High", 880, VoiceGender.Female),
            ],
        });
    }

    /// <summary>
    /// Used when a character has no voice assigned for this service. Returning null instead is
    /// fine; the character will simply have no voice until the user picks one.
    /// </summary>
    public Task<VoiceCatalogEntry?> GetDefaultVoiceAsync(
        IAuthenticationContext auth,
        ISettingsSource settings,
        VoiceGender gender,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult<VoiceCatalogEntry?>(gender switch
        {
            VoiceGender.Male => CreateVoice("Beep Low", 220, VoiceGender.Male),
            VoiceGender.Female => CreateVoice("Beep High", 880, VoiceGender.Female),
            _ => CreateVoice("Beep Mid", 440, VoiceGender.Unspecified),
        });
    }

    private static VoiceCatalogEntry CreateVoice(string label, int frequency, VoiceGender gender) => new()
    {
        Label = label,
        Gender = gender,
        // Parameters are always strings; the service converts them back when it needs them.
        Parameters = new Dictionary<string, string> { [Frequency.Name] = frequency.ToString() },
    };
}
