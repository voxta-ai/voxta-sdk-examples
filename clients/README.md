# Client apps

A client app is what a person chats *with*. It signs in as the user, starts or resumes a chat,
sends what the user typed or said, and plays back what the character replies. It also tells the
server what it can do — microphone, speakers, camera — and the server adapts to that.

The Voxta desktop app is a client. So is a game or a VR app that talks to a character.

Client apps use the `Voxta.Client` package and talk to the server over a SignalR websocket. Only one
client drives a chat at a time; anything that wants to *join* a chat already running is a
[provider app](../providerapps) instead.

## Examples

| Project | What it shows |
| --- | --- |
| [`Voxta.ConsoleChat`](Voxta.ConsoleChat) | A terminal UI client: device authentication, character and chat selection, streaming replies, speech recognition, audio playback, character-triggered functions, and tool call permission prompts. |

## Running one

```bash
cd clients/Voxta.ConsoleChat
dotnet run
```

The first run opens a browser to authorize the app against your Voxta Server; the token is then
kept on disk. The server URL is in `appsettings.json`.
