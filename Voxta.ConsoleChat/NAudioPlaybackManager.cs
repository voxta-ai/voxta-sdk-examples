using System.Collections.Concurrent;
using NAudio.Wave;
using Voxta.Client;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat;

public interface IPlaybackManagerFactory
{
    IPlaybackManager Create(IVoxtaApiClient voxtaApiClient);
}

public class NAudioPlaybackManagerFactory : IPlaybackManagerFactory
{
    public IPlaybackManager Create(IVoxtaApiClient voxtaApiClient)
    {
        return new NAudioPlaybackManager(voxtaApiClient);
    }
}

public interface IPlaybackManager
{
    event EventHandler<Guid>? OnSpeechStart;
    event EventHandler<(ServerReplyChunkMessage Chunk, TimeSpan Duration)>? OnSpeechChunk;
    event EventHandler<Guid>? OnSpeechComplete;
    event EventHandler<Exception>? OnSpeechError;

    void Start(Guid messageId, CancellationToken cancellationToken);
    void PlayChunk(ServerReplyChunkMessage speech);
    void Complete(Guid messageId);
}

public class NAudioPlaybackManager(IVoxtaApiClient voxtaApiClient) : IPlaybackManager
{
    public static readonly string[] SupportedAudioContentTypes = ["audio/x-wav", "audio/mpeg"];
    
    public event EventHandler<Guid>? OnSpeechStart;
    public event EventHandler<(ServerReplyChunkMessage Chunk, TimeSpan Duration)>? OnSpeechChunk;
    public event EventHandler<Guid>? OnSpeechComplete;
    public event EventHandler<Exception>? OnSpeechError;

    private readonly Dictionary<Guid, BlockingCollection<ServerReplyChunkMessage>> _messageQueues = new();

    public void Start(Guid messageId, CancellationToken cancellationToken)
    {
        if (_messageQueues.ContainsKey(messageId))
            return;
        
        _messageQueues.Add(messageId, new BlockingCollection<ServerReplyChunkMessage>());
        
        _ = Task.Run(() => ProcessQueue(messageId, cancellationToken), cancellationToken)
            .ContinueWith(task =>
            {
                if (task.IsFaulted) OnSpeechError?.Invoke(this, task.Exception ?? new Exception());
            }, cancellationToken);
    }

    public void PlayChunk(ServerReplyChunkMessage speech)
    {
        if (!_messageQueues.TryGetValue(speech.MessageId, out var queue))
            return;
        queue.Add(speech);
    }

    public void Complete(Guid messageId)
    {
        if (!_messageQueues.TryGetValue(messageId, out var queue))
            return;
        queue.CompleteAdding();
    }

    private async Task ProcessQueue(Guid messageId, CancellationToken cancellationToken)
    {
        if (!_messageQueues.TryGetValue(messageId, out var queue))
            return;

        try
        {
            OnSpeechStart?.Invoke(this, messageId);

            foreach (var speech in queue.GetConsumingEnumerable(cancellationToken))
            {
                if (voxtaApiClient == null) continue;
                if (speech.StartIndex == -1)
                {
                    
                }
                if (!string.IsNullOrEmpty(speech.AudioUrl))
                {
                    var stream = await voxtaApiClient.GetAudioStreamAsync(speech.AudioUrl, cancellationToken);
                    using var waveOut = new WaveOutEvent();
                    await using var reader = new StreamMediaFoundationReader(stream);

                    waveOut.Init(reader);
                    waveOut.Play();

                    OnSpeechChunk?.Invoke(this, (speech, reader.TotalTime));

                    while (waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        await Task.Delay(50, cancellationToken);
                    }
                }
                else
                {
                    OnSpeechChunk?.Invoke(this, (speech, TimeSpan.Zero));
                }
            }
        }
        finally
        {
            _messageQueues.Remove(messageId);
            OnSpeechComplete?.Invoke(this, messageId);
        }
    }
}