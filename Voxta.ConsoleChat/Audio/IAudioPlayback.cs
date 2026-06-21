using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat.Audio;

public interface IAudioPlayback
{
    event EventHandler<(ServerReplyChunkMessage Chunk, TimeSpan Duration)>? ChunkPlaying;
    event EventHandler<Guid>? Completed;
    event EventHandler<Exception>? Failed;

    void Begin(Guid messageId, CancellationToken cancellationToken);
    void Enqueue(ServerReplyChunkMessage chunk);
    void End(Guid messageId);
}
