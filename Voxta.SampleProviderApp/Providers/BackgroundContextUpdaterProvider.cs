using Microsoft.Extensions.Logging;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Providers.Host;

namespace Voxta.SampleProviderApp.Providers;

// This runs periodically in the background and updates the context
// Here we query the room temperature and update the context
public class BackgroundContextUpdaterProvider : ProviderBase
{
    private double _lastTemperature;

    public BackgroundContextUpdaterProvider(
        IRemoteChatSession session,
        ILogger<BackgroundContextUpdaterProvider> logger
        )
        : base(session, logger)
    {
    }

    protected override async Task OnStartAsync()
    {
        await base.OnStartAsync();
        _lastTemperature = await GetTemperatureAsync();
        // Let the AI know about the temperature change
        Send(new ClientUpdateContextMessage
        {
            SessionId = SessionId,
            ContextKey = "Home/Temperature",
            Contexts = [new() { Text = "{{ user }}'s home temperature is " + _lastTemperature + " degrees" }],
        });

        StartBackgroundLoop(TimeSpan.FromSeconds(10), UpdateTemperature);
    }

    private async Task UpdateTemperature()
    {
        // Get the temperature
        var temperature = await GetTemperatureAsync();
        var temperatureDifference = Math.Abs(temperature - _lastTemperature);

        // Ignore small temperature changes
        if (!(temperatureDifference > 0.5))
            return;

        // Let the AI know about the temperature change
        Logger.LogInformation("Temperature changed to {Temperature} degrees", temperature);
        Send(new ClientUpdateContextMessage
        {
            SessionId = SessionId,
            ContextKey = "Home/Temperature",
            Contexts = [new() { Text = "{{ user }}'s home temperature is " + temperature + " degrees" }],
        });

        // Example of an immediate interruption
        if (temperatureDifference > 2)
        {
            Logger.LogInformation("Temperature changed by more than two degrees, sending user urgent notification");
            Send(new ClientSendMessage
            {
                SessionId = SessionId,
                Text = "[The temperature of {{ user }}'s home has changed by more than two degrees and is now " + temperature + " degrees]",
            });
        }
        // Example of sending the message whenever the AI is finished saying what it's saying
        else if (temperatureDifference > 1)
        {
            Logger.LogInformation("Temperature changed by more than one degree, sending user notification");
            SendWhenFree(new ClientSendMessage
            {
                SessionId = SessionId,
                Text = "[The temperature of {{ user }}'s home has changed by more than one degree and is now " + temperature + " degrees]",
            });
        }
        // Example of only sending if the AI is free
        else if (!IsBusy)
        {
            Logger.LogInformation("Temperature changed by less than one degree, sending optional user notification");
            Send(new ClientSendMessage
            {
                SessionId = SessionId,
                Text = "[The temperature of {{ user }}'s home has changed by less than one degree and is now " + temperature + " degrees]",
            });
        }

        _lastTemperature = temperature;
    }

    private Task<double> GetTemperatureAsync()
    {
        // TODO: Implement the function that reads the temperature
        return Task.FromResult(24.5);
    }
}