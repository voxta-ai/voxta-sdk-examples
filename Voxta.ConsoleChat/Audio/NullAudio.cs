using Voxta.Client;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat.Audio;

public class NullAudioPlayback : IAudioPlayback
{
    public event EventHandler<(ServerReplyChunkMessage Chunk, TimeSpan Duration)>? ChunkPlaying;
    public event EventHandler<Guid>? Completed;
    public event EventHandler<Exception>? Failed { add { } remove { } }

    public void Begin(Guid messageId, CancellationToken cancellationToken)
    {
    }

    public void Enqueue(ServerReplyChunkMessage chunk) => ChunkPlaying?.Invoke(this, (chunk, TimeSpan.Zero));

    public void End(Guid messageId) => Completed?.Invoke(this, messageId);
}

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
