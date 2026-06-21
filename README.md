# Voxta SDK Examples

Sample projects showing how to build on top of [Voxta](https://voxta.ai) using the
published `Voxta.*` NuGet packages. Each example is self-contained and references
the SDK packages directly — clone it, point it at a running Voxta Server, and run.

## Examples

| Project | What it shows |
| --- | --- |
| [`Voxta.ConsoleChat`](Voxta.ConsoleChat) | A terminal UI client that connects to Voxta, lets you pick a character, then chat with text, speech recognition, and audio playback. |
| [`Voxta.SampleProviderApp`](Voxta.SampleProviderApp) | A provider app that extends a live session: register actions, react to messages, update context, and send messages on behalf of the user. |
| [`Voxta.Modules.Examples.ChatAugmentationExample`](Voxta.Modules.Examples.ChatAugmentationExample) | A server-side module (chat augmentation) packaged with the `Voxta.Sdk.Modules` SDK. |

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- A running Voxta Server (the console and provider examples connect to `http://127.0.0.1:5384` by default — see each project's `appsettings.json`)

## Running an example

```bash
cd Voxta.SampleProviderApp
dotnet run
```

The Voxta SDK packages are restored automatically from NuGet.

## License

These examples are released under the [MIT License](LICENSE) — use them freely as a
starting point for your own projects.

> Note: the MIT license covers **only the example code in this repository**. Voxta
> Server itself is a separate, proprietary product and is **not** MIT licensed. The
> `Voxta.*` NuGet packages are distributed under their own license terms.
