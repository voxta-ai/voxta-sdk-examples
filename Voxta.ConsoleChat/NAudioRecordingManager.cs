using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NAudio.Wave;
using Voxta.Client;
using Voxta.Model.Serialization;
using Voxta.Model.Shared;

namespace Voxta.ConsoleChat;

public interface IRecordingManager : IDisposable
{
    Task ConnectAsync(IVoxtaTokenProvider voxtaTokenProvider);
    void Start();
    void Stop();
}

public class NAudioRecordingManager : IRecordingManager
{
    private readonly ClientWebSocket _audioInputWebSocket;
    private readonly WaveInEvent _waveIn;
    private readonly IConsoleOutput _output;
    private readonly IOptions<VoxtaClientOptions> _options;
    private bool _ready;
    private bool _recording;

    public NAudioRecordingManager(IConsoleOutput output, IOptions<VoxtaClientOptions> options)
    {
        _output = output;
        _options = options;
        _audioInputWebSocket = new ClientWebSocket();
        _waveIn = new WaveInEvent { BufferMilliseconds = 30 };
        _waveIn.WaveFormat = new WaveFormat(16_000, 1);
        _waveIn.DataAvailable += OnDataAvailable;
    }

    public async Task ConnectAsync(IVoxtaTokenProvider voxtaTokenProvider)
    {
        _audioInputWebSocket.Options.SetRequestHeader("Authorization", $"Bearer {voxtaTokenProvider.GetToken()}");

        await _audioInputWebSocket.ConnectAsync(WebsocketsUrlHelper.HttpToWs(_options.Value.Url, "/ws/audio/input/stream"), CancellationToken.None);
            
        try
        {
            var specifications = GetSpecifications();
            var json = JsonSerializer.Serialize(specifications, VoxtaJsonSerializer.SerializeOptions);
            var buffer = Encoding.UTF8.GetBytes(json);
            await _audioInputWebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception exc)
        {
            throw new Exception("Failed to send audio input specifications", exc);
        }

        _ready = true;
        _output.WriteLine("Audio input WebSocket connection established", ConsoleColor.DarkGray);
    }

    private AudioInputSpecifications GetSpecifications()
    {
        return new AudioInputSpecifications
        {
            SampleRate = _waveIn.WaveFormat.SampleRate,
            Channels = _waveIn.WaveFormat.Channels,
            BufferMilliseconds = _waveIn.BufferMilliseconds,
            BitsPerSample = _waveIn.WaveFormat.BitsPerSample,
        };
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!_ready || e.BytesRecorded == 0 || !_audioInputWebSocket.State.HasFlag(WebSocketState.Open))
            return;
        _audioInputWebSocket.SendAsync(new ArraySegment<byte>(e.Buffer, 0, e.BytesRecorded), WebSocketMessageType.Binary, true, CancellationToken.None)
            .ContinueWith(
                t =>
                {
                    if (t.IsFaulted)
                        _output.WriteErrorLine(t.Exception?.ToString() ?? "Failed to send audio input data");
                }
            );
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

    public void Dispose()
    {
        _ready = false;
        _audioInputWebSocket.Dispose();
        _waveIn.Dispose();
    }
}
