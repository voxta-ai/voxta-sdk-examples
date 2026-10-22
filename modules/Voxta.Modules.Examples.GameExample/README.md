# Game integration example

Lets a game mod talk to Voxta characters. The module is the Voxta half; the game mod (in whatever the game is modded
with) calls it over HTTP on `http://localhost:5390/`.

- Replies come back as **wav files** in a folder the game reads, so it plays them on its own speakers and lip syncs from
  them.
- The player talks into **Voxta's microphone**, not the game's.
- What the character does in the game (here, `wave`) comes as **tool calls** the game acts out.
- The game shows in Voxta's **Play library**, with a Play button that starts the bridge first.

## The game's side

| Request | Body | What it does |
| --- | --- | --- |
| `POST /talk` | `{"characterId": "..."}` | Starts a conversation, ending the one before. Without an id, the character picked in the module's settings. |
| `GET /events` | | Lines (`text`, `audioPath`), actions (`text` is the tool, `arguments`) and reply ends since the last poll. |
| `POST /say` | `{"text": "..."}` | What the player did or said, when not through the microphone. |
| `POST /scene` | `{"text": "..."}` | What the character can see now. Replaces the last one. |
| `POST /played` | `{"messageId": "..."}` | The reply's audio finished playing. **Required**: the chat waits for it before the next reply. |
| `POST /interrupt` | | The player walked off or started talking: stop the reply. |
| `POST /bye` | | Ends the conversation. |

A game polls `/events` every frame or so, plays each `line`'s wav, acts out each `action`, and posts `/played` after the
last line of a reply.

## Voxta's side

- `GameBridgeServer` listens from startup, since the game is the client, and is the module's **chat bridge**
  (`IChatBridge`): its conversations show on the Chat Bridges page, where they can be stopped.
- `GameConversation` is one headless chat (`ILocalChatSessionHost`): the audio and tools the game handles, the scenario
  the module ships, and the messages read back as events.
- `GameLauncher` is the **launcher** (`ILauncher`): it finds the game through the stores Voxta knows (`IInstalledGames`:
  Steam, GOG and Epic), or the executable set in the module's settings, which shows **Auto** while empty.

## Making it yours

- `StoreKeys` and `GameProcess` in `GameLauncher`: your game's id in each store (`steam:<app id>`, `gog:<product id>`,
  `epic:<app name>`) and its process name.
- `Assets/Cover`: exactly 16:9; the game's official art reads best.
- The tools in `GameConversation`: one per thing the character can do in the game, each `ToolPermissionClass.InChat`,
  since nobody is at a keyboard to approve anything else.
- If the game needs a mod installed, say how in setup steps: an `Onboarding.md` in the module, which Voxta walks the user
  through after installing it (from Voxta 1.13.0-alpha.9).
