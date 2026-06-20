# Voxta Provider App Example

This example should get you started if you want to extend Voxta. Some typical examples:

- Listen to what the user or the AI says
- Register actions (character action inference)
- Assistant-like actions (user action inference)
- Update the context continuously
- Send messages on behalf of the user

## How to run

Simply use `dotnet run` and the app should start. It will connect to Voxta sessions on the current machine.

## How to make it yours

In `Program.cs`, register your own providers. For example:

```csharp
// Voxta Providers
services.AddVoxtaProvider(builder =>
{
    // Add the providers you want
    builder.AddProvider<MyCustomProvider>();
});
```

You can always check out the examples in the `Providers` folder. Here's the basic structure of a provider:

```csharp
public class MyCustomProvider : ProviderBase
{
    // This uses dependency injection, feel free to add more dependencies
    public ActionProvider(IRemoteChatSession session, ILogger<ActionProvider> logger)
        : base(session, logger)
    {
    }
    
    protected override async Task OnStartAsync()
    {
        await base.OnStartAsync();
        // Here you can register events and initialize your provider
        // See the examples for more information
    }
}
```