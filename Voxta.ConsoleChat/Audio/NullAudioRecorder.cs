using Voxta.Client;

namespace Voxta.ConsoleChat.Audio;

public class NullAudioRecorder : IAudioRecorder
{
    public Task ConnectAsync(IVoxtaTokenProvider tokenProvider) => Task.CompletedTask;

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
