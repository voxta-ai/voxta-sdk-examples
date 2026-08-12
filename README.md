# Voxta SDK Examples

Sample projects showing how to build on top of [Voxta](https://voxta.ai) using the
published `Voxta.*` NuGet packages. Each example is self-contained and references
the SDK packages directly — clone it, point it at a running Voxta Server, and run.

## Examples

| Project | What it shows |
| --- | --- |
| [`Voxta.ConsoleChat`](clients/Voxta.ConsoleChat) | A terminal UI client that connects to Voxta, lets you pick a character, then chat with text, speech recognition, and audio playback. |
| [`Voxta.SampleProviderApp`](providerapps/Voxta.SampleProviderApp) | A provider app that extends a live session: register actions, react to messages, update context, and send messages on behalf of the user. |
| [`Voxta.Modules.Examples.ChatAugmentationExample`](modules/Voxta.Modules.Examples.ChatAugmentationExample) | A server-side module (chat augmentation) packaged with the `Voxta.Sdk.Modules` SDK. |
| [`Voxta.Modules.Examples.TextGenExample`](modules/Voxta.Modules.Examples.TextGenExample) | A text generation module: how a prompt is built from a chat, and how tokens are streamed back. Echoes the last user message instead of running a model. |
| [`Voxta.Modules.Examples.TextToSpeechExample`](modules/Voxta.Modules.Examples.TextToSpeechExample) | A text to speech module: how voices are listed, and how audio is streamed to the client. Generates beeps instead of speech. |

## Writing a module

A module is a class library referencing `Voxta.Sdk.Modules`, with a single
`VoxtaModule : IVoxtaModule` entry point that declares what the module supports and binds
its implementations. The three module examples above are the same shape, and the smallest
one to start from depends on what you are building:

- **A service** (text generation, speech, vision, image generation…) replaces one of Voxta's
  own building blocks — see the text generation and text to speech examples.
- **A chat augmentation** adds behavior on top of whatever services the user already has —
  see the chat augmentation example.

Settings live in two places, and the text generation example shows both. *Module
configuration* answers "how do I reach this backend" — endpoint, API key, which engine to
run — and there is one set per installed module. A *preset* answers "how should it behave" —
model, reply length, sampling — and a user can keep several and switch between them per chat.
Anything worth tuning belongs in a preset.

To install one on a server, build it and copy the output next to the server's own modules:

```bash
cd modules/Voxta.Modules.Examples.TextGenExample
dotnet build -c Release
# then copy bin/Release/net10.0/Voxta.Modules.Examples.TextGenExample.dll
# into the server's Modules folder, and restart it
```

The assembly name must start with `Voxta.Modules.` — that is how the server finds it. The
module then shows up in the server's module list, where it can be added and configured.

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- A running Voxta Server (the console and provider examples connect to `http://127.0.0.1:5384` by default — see each project's `appsettings.json`)

## Running an example

```bash
cd providerapps/Voxta.SampleProviderApp
dotnet run
```

The Voxta SDK packages are restored automatically from NuGet.

## License

These examples are released under the [MIT License](LICENSE) — use them freely as a
starting point for your own projects.

> Note: the MIT license covers **only the example code in this repository**. Voxta
> Server itself is a separate, proprietary product and is **not** MIT licensed. The
> `Voxta.*` NuGet packages are distributed under their own license terms.
