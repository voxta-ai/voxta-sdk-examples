# Modules

A module runs *inside* Voxta Server. It is a class library you build and drop next to the server's
own modules; the server loads it at startup, and it becomes part of the server. There is no
websocket and no connection to manage.

Write one when you want to plug something into Voxta itself: a model that writes the replies, a
voice that speaks them, speech recognition, vision, image generation — or extra behavior applied
to every reply. Anything that talks to a running chat from the outside is a
[provider app](../providerapps) instead.

Modules use the `Voxta.Sdk.Modules` package.

## The two kinds

- **A service** replaces one of Voxta's building blocks: text generation, text to speech, speech to
  text, vision, image generation. The user picks it in their service settings, and from then on
  Voxta calls yours instead of the built-in one.
- **A chat augmentation** adds behavior on top of whatever services the user already has. It sees
  messages as they happen and can act on them.

## Examples

| Project | What it shows |
| --- | --- |
| [`Voxta.Modules.Examples.TextGenExample`](Voxta.Modules.Examples.TextGenExample) | A text generation service: how a prompt is built from a chat, how tokens are streamed back, how reasoning is declared, and how a tool call asks the user for permission. Echoes the last user message instead of running a model. |
| [`Voxta.Modules.Examples.TextToSpeechExample`](Voxta.Modules.Examples.TextToSpeechExample) | A text to speech service: how voices are listed and how audio is streamed to the client. Generates beeps instead of speech. |
| [`Voxta.Modules.Examples.ChatAugmentationExample`](Voxta.Modules.Examples.ChatAugmentationExample) | A chat augmentation: reacting to messages, inferring custom actions, and preprocessing what the character is about to say. |

The three are the same shape, so start from whichever is closest to what you are building.

## The entry point

Every module has one `VoxtaModule : IVoxtaModule` class. Voxta calls its `Configure` once at
startup, and that is where the module declares what it supports and binds its implementations:

```csharp
public class VoxtaModule : IVoxtaModule
{
    public const string ServiceName = "MyModule";

    public void Configure(IVoxtaModuleBuilder builder)
    {
        builder.Register(new ModuleDefinition { /* name, logo, settings pages... */ });
        builder.AddTextGenService<MyTextGenService>(ServiceName);
    }
}
```

The `ServiceName` is what user settings are saved against, so do not change it once published.

## Settings

Settings live in two places, and the text generation example shows both.

- **Module configuration** contains configuration fields for how to reach this backend - endpoint, API key, which engine
  to run. There is one set per installed module.
- **A preset**, or "service settings", contains configuration fields for options of this backend - model, reply length, sampling. A user can keep
  several and switch between them per chat.

## Installing one on a server

```bash
cd modules/Voxta.Modules.Examples.TextGenExample
dotnet build -c Release
# then copy bin/Release/net10.0/Voxta.Modules.Examples.TextGenExample.dll
# into the server's Modules folder, and restart it
```

The assembly name must start with `Voxta.Modules.` — that is how the server finds it. The module
then shows up in the server's module list, where it can be added and configured.
