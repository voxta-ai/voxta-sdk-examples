# Voxta SDK Examples

Learn how to build [Voxta](https://voxta.ai) modules and clients using the published
`Voxta.*` NuGet packages. Each example is self-contained and references the SDK packages directly —
clone it, point it at a running Voxta Server, and run.

## Three ways to build on Voxta

| Type | Description                                                                                        |
| --- |----------------------------------------------------------------------------------------------------|
| [**Client app**](clients) | Build your own chat app connecting to Voxta (e.g. Voxy, VaM)                                       |
| [**Provider app**](providerapps) | Integrate with Voxta chats by running a separate program (mostly legacy)                           |
| [**Module**](modules) | Install custom modules inside Voxta, e.g. text gen and tts services, tools and other augmentations |

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- A running Voxta Server (the client and provider examples connect to `http://127.0.0.1:5384` by default — see each project's `appsettings.json`)

## Running an example

```bash
cd clients/Voxta.ConsoleChat
dotnet run
```

The Voxta SDK packages are restored automatically from NuGet. Modules are not run directly; see
[the modules README](modules) for how to install one on a server.

## License

These examples are released under the [MIT License](LICENSE) — use them freely as a
starting point for your own projects.

> Note: the MIT license covers **only the example code in this repository**. Voxta
> Server itself is a separate, proprietary product and is **not** MIT licensed. The
> `Voxta.*` NuGet packages are distributed under their own license terms.
