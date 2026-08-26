using Microsoft.Extensions.Logging;
using Voxta.Abstractions.Services;
using Voxta.Abstractions.Services.AudioOutput.Tunneling;
using Voxta.Abstractions.Services.TextToSpeech;
using Voxta.Modules.Examples.TextToSpeechExample.Configuration;

namespace Voxta.Modules.Examples.TextToSpeechExample.TextToSpeech;

/// <summary>
/// A text to speech service that does not speak: it emits one beep per word. Everything a real
/// service does is here except the call to a speech engine, which happens in
/// <see cref="GenerateSpeechAsync"/>.
///
/// Audio is not returned, it is written to an <see cref="ISpeechTunnel"/>. That is the whole
/// point of the interface: Voxta starts playing the beginning of a line while the rest is still
/// being generated, so write chunks as soon as you have them and never buffer the whole clip.
///
/// Inherit <see cref="ServiceBase"/> to get <c>ModuleConfiguration</c>, <c>ServiceSettings</c>,
/// <c>Auth</c> and <c>InstanceSettings</c>. One instance is created per user session.
/// </summary>
public class BeepTextToSpeechService(ILogger<BeepTextToSpeechService> logger)
    : ServiceBase(logger), ITextToSpeechService
{
    /// <summary>
    /// What the audio actually is. Returning raw PCM is the simplest option and avoids an encode
    /// then decode round trip; a service returning a file would use "audio/wav" or "audio/mpeg"
    /// and leave <see cref="PcmStreaming"/> null.
    /// </summary>
    public string ContentType => PcmStreaming.ContentType;

    /// <summary>
    /// The PCM format we write. It must match what the tunnel is told and what we generate,
    /// otherwise playback is pitched wrong or plays as noise.
    /// </summary>
    public PcmStreamingConfig PcmStreaming { get; } = new(SampleRate: 24_000, Channels: 1, BitsPerSample: 16);

    /// <summary>
    /// Optional pre-generated clips ("hmm", "let me think") played while the model is still
    /// writing, to cover the latency. Nothing here.
    /// </summary>
    public IReadOnlyList<string> GetThinkingSpeech() => [];

    /// <summary>
    /// Playback tweaks for engines that need them, such as extra silence between lines.
    /// </summary>
    public ValueTask<TextToSpeechAudioSettings> GetAudioSettings(VoiceSelection selection, CancellationToken cancellationToken)
        => ValueTask.FromResult(TextToSpeechAudioSettings.Empty);

    /// <summary>
    /// Called when a chat starts so a local engine can load its model before the first line.
    /// Real modules usually synthesize a short throwaway line here, writing it to a
    /// <see cref="NullSpeechTunnel"/>. Nothing to warm up in this one.
    /// </summary>
    public Task WarmupAsync(
        VoiceSelection selection,
        Guid userId,
        Guid? characterId,
        string culture,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;

    /// <summary>
    /// Turns one line of text into audio. Called once per sentence, not once per message, so
    /// this runs several times while a reply is still being generated.
    /// </summary>
    public async Task GenerateSpeechAsync(SpeechRequest request, ISpeechTunnel tunnel, CancellationToken cancellationToken)
    {
        // Voice parameters come from the character's assigned voice. That voice can be unset, or
        // belong to another service and carry parameters we know nothing about, so never assume a
        // parameter is there: reading it through the field definition falls back to its default.
        var frequency = request.GetSettings().GetRequired(BeepVoicesProvider.Frequency);

        var beepDuration = ModuleConfiguration.GetRequired(ModuleConfigurationProvider.SyllableDurationMs);
        var gapDuration = ModuleConfiguration.GetRequired(ModuleConfigurationProvider.GapDurationMs);

        var words = request.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        logger.LogDebug("Beeping {WordCount} words at {Frequency}Hz", words.Length, frequency);

        // RunAsync opens the tunnel, and completes or fails it for us. Report the content type we
        // are about to write; the length is unknown while streaming, so pass null.
        await tunnel.RunAsync(ContentType, null, async response =>
        {
            foreach (var _ in words)
            {
                // A real service would send whatever its engine just produced instead.
                await response.SendAsync(GenerateTone(frequency, beepDuration), cancellationToken);

                if (gapDuration > 0)
                    await response.SendAsync(GenerateSilence(gapDuration), cancellationToken);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Builds a sine wave as 16-bit mono PCM. This is the part a real module replaces with its
    /// engine's output.
    /// </summary>
    private ReadOnlyMemory<byte> GenerateTone(int frequency, int durationMs)
    {
        var sampleCount = GetSampleCount(durationMs);
        // Mono 16-bit, so two bytes per sample.
        var buffer = new byte[sampleCount * 2];
        var angleStep = 2.0 * Math.PI * frequency / PcmStreaming.SampleRate;

        for (var i = 0; i < sampleCount; i++)
        {
            // Fade the beep in and out, otherwise the discontinuity at each end clicks.
            var envelope = Math.Sin(Math.PI * i / sampleCount);
            var amplitude = Math.Sin(i * angleStep) * envelope * 0.4;
            var sample = (short)(amplitude * short.MaxValue);
            // PCM is little-endian.
            buffer[i * 2] = (byte)(sample & 0xFF);
            buffer[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return buffer;
    }

    /// <summary>Silence is simply zeroed samples.</summary>
    private ReadOnlyMemory<byte> GenerateSilence(int durationMs) => new byte[GetSampleCount(durationMs) * 2];

    /// <summary>
    /// Chunks must contain whole samples. Sending a half sample shifts every following sample by a
    /// byte, and the rest of the line plays as noise.
    /// </summary>
    private int GetSampleCount(int durationMs) => PcmStreaming.SampleRate * durationMs / 1000;
}
