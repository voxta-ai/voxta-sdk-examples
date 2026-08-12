using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Voxta.Client;
using Voxta.Providers.Host;
using Voxta.SampleProviderApp;
using Voxta.SampleProviderApp.Providers;

// Dependency Injection
var services = new ServiceCollection();

// Configuration
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", true)
    .Build();
services.AddSingleton<IConfiguration>(configuration);
services.AddOptions<VoxtaClientOptions>()
    .Bind(configuration.GetSection("Voxta.Client"))
    .ValidateDataAnnotations();
services.AddOptions<SampleProviderAppOptions>()
    .Bind(configuration.GetSection("SampleProviderApp"))
    .ValidateDataAnnotations();

// Logging
await using var log = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Filter.ByExcluding(logEvent => logEvent.Exception?.GetType().IsSubclassOf(typeof(OperationCanceledException)) ?? false)
    .CreateLogger();
services.AddLogging(builder =>
{
    // ReSharper disable once AccessToDisposedClosure
    builder.AddSerilog(log);
});

// Dependencies
services.AddHttpClient();

// Voxta Providers
services.AddVoxtaProvider(builder =>
{
    // Add the providers you want
    builder.AddProvider<AutoReplyProvider>();
    builder.AddProvider<BackgroundContextUpdaterProvider>();
    builder.AddProvider<CommandsParserProvider>();
    builder.AddProvider<ActionProvider>();
    builder.AddProvider<UserFunctionProvider>();
});

// Build the application
var sp = services.BuildServiceProvider();
var runtime = sp.GetRequiredService<IProviderAppHandler>();

// Run the application
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};
await runtime.RunAsync(cts.Token);
