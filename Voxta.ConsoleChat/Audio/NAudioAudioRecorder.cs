using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.Wave;
using Voxta.Client;
using Voxta.Model.Serialization;
using Voxta.Model.Shared;

namespace Voxta.ConsoleChat.Audio;

public class NAudioAudioRecorder : IAudioRecorder
{
    private readonly ClientWebSocket _socket = new();
    private readonly WaveInEvent _waveIn;
    private readonly VoxtaClientOptions _options;
    private readonly ILogger<NAudioAudioRecorder> _logger;
    private bool _ready;
    private bool _recording;

    public NAudioAudioRecorder(IOptions<VoxtaClientOptions> options, ILogger<NAudioAudioRecorder> logger)
    {
        _options = options.Value;
        _logger = logger;
        _waveIn = new WaveInEvent { BufferMilliseconds = 30, WaveFormat = new WaveFormat(16_000, 1) };
        _waveIn.DataAvailable += OnDataAvailable;
    }

    public async Task ConnectAsync(IVoxtaTokenProvider tokenProvider)
    {
        _socket.Options.SetRequestHeader("Authorization", $"Bearer {tokenProvider.GetToken()}");
        await _socket.ConnectAsync(WebsocketsUrlHelper.HttpToWs(_options.Url, "/ws/audio/input/stream"), CancellationToken.None);

        var specifications = new AudioInputSpecifications
        {
            SampleRate = _waveIn.WaveFormat.SampleRate,
            Channels = _waveIn.WaveFormat.Channels,
            BufferMilliseconds = _waveIn.BufferMilliseconds,
            BitsPerSample = _waveIn.WaveFormat.BitsPerSample,
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(specifications, VoxtaJsonSerializer.SerializeOptions);
        await _socket.SendAsync(json, WebSocketMessageType.Text, true, CancellationToken.None);

        _ready = true;
        _logger.LogDebug("Audio input WebSocket connected");
    }

    public void Start()
    {
        if (_recording) return;
        _recording = true;
        _waveIn.StartRecording();
    }

    public void Stop()
    {
        if (!_recording) return;
        _recording = false;
        _waveIn.StopRecording();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!_ready || e.BytesRecorded == 0 || !_socket.State.HasFlag(WebSocketState.Open))
            return;

        _socket.SendAsync(new ArraySegment<byte>(e.Buffer, 0, e.BytesRecorded), WebSocketMessageType.Binary, true, CancellationToken.None)
            .ContinueWith(t => _logger.LogError(t.Exception, "Failed to send audio input data"), TaskContinuationOptions.OnlyOnFaulted);
    }

    public void Dispose()
    {
        _ready = false;
        _socket.Dispose();
        _waveIn.Dispose();
        GC.SuppressFinalize(this);
    }
}
