using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Voxta.Client;
using Voxta.ConsoleChat;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Warning);
    // builder.AddFilter("Voxta.Client", LogLevel.Debug);
    builder.AddSimpleConsole(options =>
    {
        options.IncludeScopes = false;
        options.SingleLine = true;
    });
});

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddSingleton(loggerFactory);
services.AddHttpClient();
services.AddLogging();
services.AddVoxtaApiClient();
services.AddTransient<VoxtaTokenProvider>();
services.AddSingleton<ConsoleChatSessionFactory>();
services.AddSingleton<IConsoleOutput, ConsoleOutput>();
services.AddSingleton<IPlaybackManagerFactory, NAudioPlaybackManagerFactory>();
services.AddSingleton<IRecordingManager, NAudioRecordingManager>();
services.AddSingleton<IDeviceAuthorization, DeviceAuthorization>();
services.AddSingleton<ConsoleChatApp>();
services.AddSingleton<VoxtaFunctions>();
services.AddSingleton<ITokenStorage, TokenStorage>();

var provider = services.BuildServiceProvider();

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var client = provider.GetRequiredService<ConsoleChatApp>();
await client.RunAsync(cts.Token);
