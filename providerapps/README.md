# Provider apps

A provider app joins a chat that someone else is already having. It does not show a UI and it does
not sign in as a user — it connects with an API key, attaches to the sessions running on the
server, and adds behavior to them.

Use one to react to a live chat from outside Voxta: register actions the character can call, watch
what is said, keep the context up to date, or send messages on the user's behalf. Driving hardware,
a game, or a stream overlay from what the character does is the usual reason to write one.

Provider apps use the `Voxta.Providers.Host` package and talk to the server over a websocket. If
you want to *replace* one of the server's building blocks instead — the model that writes replies,
the voice that speaks them — write a [module](../modules).

It is recommended to use [modules](../modules) instead, since you don't need to run a separate program
to connect to.

## Examples

| Project | What it shows |
| --- | --- |
| [`Voxta.SampleProviderApp`](Voxta.SampleProviderApp) | Five providers, one per idea: register an action, parse commands out of what is said, update the context in the background, reply automatically after a pause, and expose a function to the user. |

## Running one

```bash
cd providerapps/Voxta.SampleProviderApp
dotnet run
```

It connects to the server in `appsettings.json` and waits. Start a chat from any client and the
providers attach to it.

## Writing your own

A provider is a class inheriting `ProviderBase`, registered at startup:

```csharp
services.AddVoxtaProvider(builder =>
{
    builder.AddProvider<MyCustomProvider>();
});
```

See [the sample app's README](Voxta.SampleProviderApp/README.md) for the shape of a provider.
