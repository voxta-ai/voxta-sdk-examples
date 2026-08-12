using System.Web;
using LibVLCSharp.Shared;
using Voxta.Client;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat.Audio;

// Adds a reason the user can see when audio is unavailable (VLC not installed).
public interface IConsoleAudioOutput : ISpeechAudioOutput
{
    string? UnavailableReason { get; }
}

// Cross-platform audio backend for the SDK's SpeechPlayback. Decodes regular media (wav/mpeg/ogg),
// raw PCM streams (signalled by a `pcm` query parameter) and `silence:` placeholders. If VLC is not
// installed, clips are skipped gracefully and a reason is surfaced to the user.
public sealed class VlcAudioOutput : IConsoleAudioOutput, IDisposable
{
    public static readonly string[] SupportedContentTypes = ["audio/x-wav", "audio/wav", "audio/mpeg", "audio/ogg"];

    private const int PcmDefaultSampleRate = 24_000;
    private const int PcmBytesPerSample = 2;

    public string? UnavailableReason { get; }

    private readonly IVoxtaApiClient _apiClient;
    private readonly LibVLC? _libVlc;

    public VlcAudioOutput(IVoxtaApiClient apiClient)
    {
        _apiClient = apiClient;
        try
        {
            Core.Initialize();
            _libVlc = new LibVLC("--no-video", "--quiet");
        }
        catch (Exception exception)
        {
            UnavailableReason = $"Voice playback is unavailable ({exception.Message}). Install VLC to hear audio.";
        }
    }

    public async Task<ISpeechAudioClip?> PrepareAsync(ServerReplyChunkMessage chunk, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(chunk.AudioUrl))
            return null;

        if (chunk.AudioUrl.StartsWith("silence:", StringComparison.Ordinal))
            return new SilenceClip(TimeSpan.FromMilliseconds(int.TryParse(chunk.AudioUrl["silence:".Length..], out var ms) ? ms : 0));

        var data = await ReadAllBytesAsync(await _apiClient.Speech.GetAudioStreamAsync(chunk.AudioUrl, cancellationToken), cancellationToken);
        if (_libVlc == null)
            return null;

        if (IsPcm(chunk.AudioUrl))
        {
            var sampleRate = PcmSampleRate(chunk.AudioUrl);
            var media = new Media(_libVlc, new StreamMediaInput(new MemoryStream(data)));
            media.AddOption(":demux=rawaud");
            media.AddOption(":rawaud-channels=1");
            media.AddOption($":rawaud-samplerate={sampleRate}");
            media.AddOption(":rawaud-fourcc=s16l");
            return new VlcClip(_libVlc, media, TimeSpan.FromSeconds(data.Length / (double)(sampleRate * PcmBytesPerSample)));
        }

        TimeSpan duration;
        using (var probe = new Media(_libVlc, new StreamMediaInput(new MemoryStream(data))))
        {
            await probe.Parse(MediaParseOptions.ParseLocal, timeout: 5000, cancellationToken);
            duration = TimeSpan.FromMilliseconds(probe.Duration > 0 ? probe.Duration : 0);
        }
        return new VlcClip(_libVlc, new Media(_libVlc, new StreamMediaInput(new MemoryStream(data))), duration);
    }

    private static bool IsPcm(string url) => QueryOf(url).Get("pcm") != null;

    private static int PcmSampleRate(string url) =>
        int.TryParse(QueryOf(url).Get("sr"), out var sr) && sr > 0 ? sr : PcmDefaultSampleRate;

    private static System.Collections.Specialized.NameValueCollection QueryOf(string url)
    {
        var index = url.IndexOf('?');
        return index >= 0 ? HttpUtility.ParseQueryString(url[(index + 1)..]) : [];
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream is MemoryStream existing)
            return existing.ToArray();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    public void Dispose() => _libVlc?.Dispose();

    private sealed class SilenceClip(TimeSpan duration) : ISpeechAudioClip
    {
        public TimeSpan Duration => duration;
        public Task PlayAsync(CancellationToken cancellationToken) => Task.Delay(duration, cancellationToken);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class VlcClip(LibVLC libVlc, Media media, TimeSpan duration) : ISpeechAudioClip
    {
        public TimeSpan Duration => duration;

        public async Task PlayAsync(CancellationToken cancellationToken)
        {
            using var player = new MediaPlayer(libVlc);
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            player.EndReached += (_, _) => finished.TrySetResult();
            player.EncounteredError += (_, _) => finished.TrySetResult();

            player.Play(media);
            await using (cancellationToken.Register(() => { player.Stop(); finished.TrySetResult(); }))
                await finished.Task;
        }

        public ValueTask DisposeAsync()
        {
            media.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
