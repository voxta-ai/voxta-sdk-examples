# Game integration example

Lets a game mod talk to Voxta characters. The module is the Voxta half; the game mod (in whatever the game is modded
with) calls it over HTTP on Voxta's own port, `http://localhost:5384/api/extensions/gameexample/`, with one of the
player's Voxta API keys.

- Replies come back as **wav files** in a folder the game reads, so it plays them on its own speakers and lip syncs from
  them.
- The player talks into **Voxta's microphone**, not the game's.
- What the character does in the game (here, `wave`) comes as **tool calls** the game acts out.
- The module ships its own **character** (Mira, `Assets/Companion.voxpak`), which installing the module puts in the
  user's library: no import step.
- The game shows in Voxta's **Play library**, with a Play button that starts the bridge first.

## The game's side

| Request | Body | What it does |
| --- | --- | --- |
| `POST talk` | `{"characterId": "..."}` | Starts a conversation, ending the one before. Without an id, the character picked in the module's settings, else Mira. |
| `GET events` | | Lines (`text`, `audioPath`), actions (`text` is the tool, `arguments`) and reply ends since the last poll. |
| `POST say` | `{"text": "..."}` | What the player did or said, when not through the microphone. |
| `POST scene` | `{"text": "..."}` | What the character can see now. Replaces the last one. |
| `POST played` | `{"messageId": "..."}` | The reply's audio finished playing. **Required**: the chat waits for it before the next reply. |
| `POST interrupt` | | The player walked off or started talking: stop the reply. |
| `POST bye` | | Ends the conversation. |

A game polls `events` every frame or so, plays each `line`'s wav, acts out each `action`, and posts `played` after the
last line of a reply.

Every request carries `Authorization: Bearer <key>`, and the conversation runs as that key's account. The mod takes the
key from its config, where the player can paste one from Voxta's API keys page (with the Chats permission). Without
one, or when Voxta answers 401 or 403, it asks for one by device authorization:

1. `POST /api/device/code` with `{"client_id":"voxta","scope":"chat","label":"My Game"}`: show the player "enter
   `user_code` at `verification_url`".
2. `POST /api/device/poll` with `{"device_code":"..."}` every few seconds: 204 while waiting, 200 with `token` once
   approved (save it to the config), 400 when the code expired (ask for a new one).

The player carries the code: show it, never open the device page with it filled in.

## Voxta's side

- `GameBridgeServer` is served on Voxta's own port (`IModuleHttpEndpoints`, `ModuleHttpAccess.ApiKey`: Voxta checks
  the key), runs each conversation as the account of the key the request came with
  (`IUserContexts.ForModuleAsync(ServiceName, request.UserId, ct)`, with that account's settings), and is the module's
  **chat bridge** (`IChatBridge`): its conversations show on the Chat Bridges page, where they can be stopped.
- `GameConversation` is one headless chat (`IUserContext.StartChatAsync`): the audio and tools the game handles, the
  scenario the module ships, and the messages read back as events.
- `ServiceInstallationProvider` runs when an account installs the module and when the module updates: it puts the
  module's `.voxpak` in that account's library (`IAppResourceDeployer.DeployPackageAsync`).
- `GameLauncher` is the **launcher** (`ILauncher`): it finds the game through the stores Voxta knows (`IInstalledGames`:
  Steam, GOG and Epic), or the executable set in the module's settings, which shows **Auto** while empty.

## Making it yours

- `StoreKeys` and `GameProcess` in `GameLauncher`: your game's id in each store (`steam:<app id>`, `gog:<product id>`,
  `epic:<app name>`) and its process name.
- `Assets/Cover`: exactly 16:9; the game's official art reads best.
- `Assets/Companion.voxpak`: your characters, books and scenarios. Make them in Voxta, export them as a package and drop
  the `.voxpak` here. Installing or updating the module writes what is missing or has a new version, and keeps what the
  user edited: give each resource a new version when you change it. Resources need ids of their own, not ones
  from Voxta's built-in package.
- The tools in `GameConversation`: one per thing the character can do in the game, each `ToolPermissionClass.InChat`,
  since nobody is at a keyboard to approve anything else.
- If the game needs a mod installed, say how in setup steps, which Voxta walks the user through when installing it: an
  `Onboarding.md` embedded in the module (`<EmbeddedResource Include="Onboarding.md" />`) and declared on its
  definition, `Onboarding = ModuleOnboarding.EmbeddedResource(typeof(VoxtaModule), "Onboarding.md")` (from Voxta
  1.13.0-alpha.10).
