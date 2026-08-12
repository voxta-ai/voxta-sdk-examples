using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Providers.Host;

namespace Voxta.SampleProviderApp.Providers;

// This example shows how to use auto-reply and read from the appsettings.json
public class AutoReplyProvider(
    IRemoteChatSession session,
    ILogger<AutoReplyProvider> logger,
    IOptions<SampleProviderAppOptions> options
)
    : ProviderBase(session, logger)
{
    protected override async Task OnStartAsync()
    {
        await base.OnStartAsync();
        
        // Automatically reply when the user does not speak
        ConfigureAutoReply(TimeSpan.FromMilliseconds(options.Value.AutoReplyDelay), OnAutoReply);
    }

    private void OnAutoReply()
    {
        Logger.LogInformation("Auto-replying after delay of {Delay}ms of inactivity", options.Value.AutoReplyDelay);
        Send(new ClientSendMessage
        {
            SessionId = SessionId,
            Text = "*{{ char }} continues talking to {{ user }}*"
        });
    }
}
