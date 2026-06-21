using System.Collections.Concurrent;
using NAudio.Wave;
using Voxta.Client;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat.Audio;

public class NAudioAudioPlayback(IVoxtaApiClient apiClient) : IAudioPlayback
{
    public static readonly string[] SupportedContentTypes = ["audio/x-wav", "audio/mpeg"];

    public event EventHandler<(ServerReplyChunkMessage Chunk, TimeSpan Duration)>? ChunkPlaying;
    public event EventHandler<Guid>? Completed;
    public event EventHandler<Exception>? Failed;

    private readonly ConcurrentDictionary<Guid, BlockingCollection<ServerReplyChunkMessage>> _queues = new();

    public void Begin(Guid messageId, CancellationToken cancellationToken)
    {
        if (!_queues.TryAdd(messageId, new BlockingCollection<ServerReplyChunkMessage>()))
            return;

        _ = Task.Run(() => PlayQueueAsync(messageId, cancellationToken), cancellationToken)
            .ContinueWith(
                task => Failed?.Invoke(this, task.Exception ?? new Exception("Audio playback failed")),
                TaskContinuationOptions.OnlyOnFaulted);
    }

    public void Enqueue(ServerReplyChunkMessage chunk)
    {
        if (_queues.TryGetValue(chunk.MessageId, out var queue))
            queue.Add(chunk);
    }

    public void End(Guid messageId)
    {
        if (_queues.TryGetValue(messageId, out var queue))
            queue.CompleteAdding();
    }

    private async Task PlayQueueAsync(Guid messageId, CancellationToken cancellationToken)
    {
        if (!_queues.TryGetValue(messageId, out var queue))
            return;

        try
        {
            foreach (var chunk in queue.GetConsumingEnumerable(cancellationToken))
            {
                if (string.IsNullOrEmpty(chunk.AudioUrl))
                {
                    ChunkPlaying?.Invoke(this, (chunk, TimeSpan.Zero));
                    continue;
                }

                var stream = await apiClient.GetAudioStreamAsync(chunk.AudioUrl, cancellationToken);
                using var output = new WaveOutEvent();
                await using var reader = new StreamMediaFoundationReader(stream);

                output.Init(reader);
                output.Play();
                ChunkPlaying?.Invoke(this, (chunk, reader.TotalTime));

                while (output.PlaybackState == PlaybackState.Playing)
                    await Task.Delay(50, cancellationToken);
            }
        }
        finally
        {
            _queues.TryRemove(messageId, out _);
            Completed?.Invoke(this, messageId);
        }
    }
}
