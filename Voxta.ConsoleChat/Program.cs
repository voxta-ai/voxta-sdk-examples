using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Voxta.Client;
using Voxta.ConsoleChat.Api;
using Voxta.ConsoleChat.Audio;
using Voxta.ConsoleChat.Auth;
using Voxta.ConsoleChat.Functions;
using Voxta.ConsoleChat.Hosting;
using Voxta.ConsoleChat.Persistence;
using Voxta.ConsoleChat.Presentation;
using Voxta.ConsoleChat.Presentation.Tui;
using Voxta.ConsoleChat.Session;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
services.AddHttpClient();
services.AddVoxtaApiClient();

services.AddSingleton<VoxtaTokenProvider>();
services.AddSingleton<IVoxtaTokenProvider>(sp => sp.GetRequiredService<VoxtaTokenProvider>());
services.AddSingleton<ITokenStore, FileTokenStore>();
services.AddSingleton<ILastChatStore, FileLastChatStore>();
services.AddSingleton<ChatFunctions>();

if (OperatingSystem.IsWindows())
{
    services.AddSingleton<IAudioRecorder, NAudioAudioRecorder>();
    services.AddSingleton<IAudioPlayback>(sp =>
        new NAudioAudioPlayback(sp.GetRequiredService<IVoxtaApiClientFactory>().Create(sp.GetRequiredService<VoxtaTokenProvider>())));
}
else
{
    services.AddSingleton<IAudioRecorder, NullAudioRecorder>();
    services.AddSingleton<IAudioPlayback, NullAudioPlayback>();
}

services.AddHttpClient<CharactersApiClient>((sp, client) =>
    client.BaseAddress = sp.GetRequiredService<IOptions<VoxtaClientOptions>>().Value.Url);
services.AddHttpClient<ChatsApiClient>((sp, client) =>
    client.BaseAddress = sp.GetRequiredService<IOptions<VoxtaClientOptions>>().Value.Url);

services.AddSingleton<IChatView, TuiChatView>();
services.AddSingleton<VoxtaConnection>();
services.AddSingleton<ChatAppHost>();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    // Disposed before the catch runs, so the terminal UI is torn down (screen restored)
    // before any error is printed, and singletons are disposed exactly once.
    await using var provider = services.BuildServiceProvider();
    await provider.GetRequiredService<ChatAppHost>().RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Voxta.ConsoleChat stopped: {exception.Message}");
}
