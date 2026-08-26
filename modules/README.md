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
  messages as they happen and can act on them. **Tools** — the functions a model can call mid-reply —
  are served this way too; see the tools example.

## Examples

| Project | What it shows |
| --- | --- |
| [`Voxta.Modules.Examples.TextGenExample`](Voxta.Modules.Examples.TextGenExample) | A text generation service: how a prompt is built from a chat, how tokens are streamed back, how reasoning is declared, and how a tool call asks the user for permission. Echoes the last user message instead of running a model. |
| [`Voxta.Modules.Examples.TextToSpeechExample`](Voxta.Modules.Examples.TextToSpeechExample) | A text to speech service: how voices are listed and how audio is streamed to the client. Generates beeps instead of speech. |
| [`Voxta.Modules.Examples.ChatAugmentationExample`](Voxta.Modules.Examples.ChatAugmentationExample) | A chat augmentation: reacting to messages, inferring custom actions, and preprocessing what the character is about to say. |
| [`Voxta.Modules.Examples.ToolsExample`](Voxta.Modules.Examples.ToolsExample) | Tool calling: declaring tools with a JSON schema, classifying what each one is allowed to do, running them, and remembering a "yes, always". |

The four are the same shape, so start from whichever is closest to what you are building.

## Keys: augmentation keys and context keys

Two different keys show up in augmentation code, and they answer different questions.

**An augmentation key** is what a user turns on. It is the unit of consent: a character names it, or
a chat adds it, and that is what decides whether your augmentation runs at all.

Use **one key for the whole augmentation**, not one per capability. A Google module has a single
`google` key covering search, image search and news — not three. Someone enabling Google means "let
the character use Google", and three checkboxes for one decision is three ways to get it half-on.
Split a key only when the parts are genuinely separate decisions, i.e. when somebody would sensibly
want one and not the other.

Several plan items may share one key. The runner gates each item on its own key, so items sharing a
key start and stop as a set — which is how one augmentation is assembled out of several instances.
Whether each part is *offered* is a module setting, which is a different question from what the
character asked for.

**A context key** is what a set of things is replaced under. Actions and contexts are registered with
one:

```csharp
new ClientUpdateContextMessage
{
    ContextKey = VoxtaModule.ServiceName,
    SessionId = session.SessionId,
    Actions = [ /* ... */ ],
}
```

Sending another message with the same context key replaces *everything* previously registered under
it. So the rule is: **one context key per set of things that are updated together.** If your search
actions and your news actions are always registered and withdrawn as one, they share a key. If they
come and go independently, they must not — otherwise updating one silently drops the other.

In practice a module has a single context key, its service name, for the same reason it has a single
augmentation key. It is also what a tool names as its source (`ChatTool`'s `Source`), which is how a
user finds out which of their modules answered when two of them offer a `web_search`.

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
