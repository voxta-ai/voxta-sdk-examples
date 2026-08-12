using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Voxta.Client;
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
services.AddSingleton<IChatFunction, OpenWebBrowserFunction>();
services.AddSingleton<IChatFunction, RunCommandFunction>();
services.AddSingleton<IChatFunction, ExitFunction>();
services.AddSingleton<ChatFunctions>();
services.AddSingleton<ChatSessionFactory>();

services.AddSingleton<IConsoleAudioOutput>(sp =>
    new VlcAudioOutput(sp.GetRequiredService<IVoxtaApiClientFactory>().Create(sp.GetRequiredService<VoxtaTokenProvider>())));

if (OperatingSystem.IsWindows())
    services.AddSingleton<IAudioRecorder, NAudioAudioRecorder>();
else
    services.AddSingleton<IAudioRecorder, NullAudioRecorder>();

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
    await using var provider = services.BuildServiceProvider();
    var resume = args.Contains("--resume");
    await provider.GetRequiredService<ChatAppHost>().RunAsync(resume, cts.Token);
}
catch (OperationCanceledException)
{
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Voxta.ConsoleChat stopped: {exception.Message}");
}
