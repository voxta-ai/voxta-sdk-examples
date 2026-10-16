# Modules

A module runs *inside* Voxta Server. It is a class library the server loads in its own load context,
with its own DI container, and it becomes part of the server. There is no websocket and no
connection to manage.

Write one when you want to plug something into Voxta itself: a model that writes the replies, a
voice that speaks them, speech recognition, vision, image generation — or extra behavior applied
to every reply. Anything that talks to a running chat from the outside is a
[provider app](../providerapps) instead.

Modules use the `Voxta.Sdk.Modules` package, referenced with `ExcludeAssets="runtime"`: Voxta
provides it, and every other `Voxta.*` assembly, at runtime.

Some kinds of module have a companion package with base classes for the heavy lifting —
`Voxta.Shared.LLMUtils` for text generation, `Voxta.Shared.TextToSpeechUtils` for speech. Use them.
They are not convenience wrappers: they are where the behavior every module is expected to share
lives, so a module that reimplements them is a module that quietly behaves differently from the
rest.

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
| [`Voxta.Modules.Examples.TextGenExample`](Voxta.Modules.Examples.TextGenExample) | A text generation service, built on the `Voxta.Shared.LLMUtils` base classes: what a backend declares it can do, how a request body is built, how tokens and tool calls are streamed back, and where the shared preset fields come from. Echoes the last user message instead of running a model. |
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

Every module has one `VoxtaModule : IVoxtaModule` class. Voxta calls its `Configure` once when the
module loads, and that is where the module declares what it supports and binds its implementations:

```csharp
public class VoxtaModule : IVoxtaModule
{
    public const string ServiceName = "MyModule";

    public void Configure(IVoxtaModuleBuilder builder)
    {
        builder.Register(new ModuleDefinition { Version = ModuleInfo.Version, /* name, logo, settings pages... */ });
        builder.AddTextGenService<MyTextGenService>(ServiceName);
    }
}
```

The `ServiceName` is what user settings are saved against, so do not change it once published.

`builder.Services` is the module's own container: Voxta's services are visible to it, other
modules' are not. Things the host asks every module for (chat bridges, model repositories, HTTP
endpoints) are given with `builder.Contribute<TContract, TImpl>()`.

## module.json

Next to the csproj. The package's build targets embed it and generate `ModuleInfo.Version` from it.

```json
{
  "id": "Voxta.Modules.MyModule",
  "version": "1.0.0",
  "voxta": ">=1.13.0",
  "label": "My Module",
  "description": "What it does, in a line."
}
```

- `id`: the assembly name.
- `version`: the module's only version; a new one offers users the update.
- `voxta`: the Voxta versions it runs on. Build against the oldest one.
- `label`, `description`: how a GitHub source lists it.

## Settings

Settings live in two places, and the text generation example shows both.

- **Module configuration** contains configuration fields for how to reach this backend - endpoint, API key, which engine
  to run. There is one set per installed module.
- **A preset**, or "service settings", contains configuration fields for options of this backend - model, reply length, sampling. A user can keep
  several and switch between them per chat.

## Installing one on a server

```bash
cd modules/Voxta.Modules.Examples.TextGenExample
dotnet build
```

1. In Voxta, open **Add Modules → Add Custom Module** and add the project folder (or its build
   output) as a dev folder, then **Apply**. The module shows up in Add Modules with a Dev badge.
2. Change code, build again with Voxta running, **Apply**: the new build replaces the old one, no
   restart.

Without the source, **Install from .zip or .dll** on the same page: the DLL alone, or a zip of the
`dotnet publish` output when the module ships libraries of its own.

Custom modules need Voxta Full and an administrator. The assembly name must start with
`Voxta.Modules.`. Load errors show on the Custom Modules page.

## Publishing one

This repository is set up like the
[module template](https://github.com/voxta-ai/voxta-module-template), which is the better start for a
module of your own: same build settings, same workflows.

- `dotnet publish -c Release -p:VoxtaModulePack=dist` writes what Voxta installs: the zip and its
  `voxta-module.json`.
- Tagging `<module id>/v<version>` (`v<version>` in a repository with one module) runs
  `.github/workflows/release.yml`: it builds, attests and creates the GitHub release.
- Voxta users add the repository in **Add Modules → Module Registries** (`owner/repo`) and install
  from there.
- `packages.lock.json` pins every package; CI restores with `--locked-mode`.
