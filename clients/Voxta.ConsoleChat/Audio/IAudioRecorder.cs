using Voxta.Client;

namespace Voxta.ConsoleChat.Audio;

public interface IAudioRecorder : IDisposable
{
    Task ConnectAsync(IVoxtaTokenProvider tokenProvider);
    void Start();
    void Stop();
}
