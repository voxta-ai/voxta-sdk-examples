# Tool calling example

Two tool examples the AI can call while it is writing a reply:

- `roll_dice` — rolls dice and reports the results.
- `write_note` — appends a line to a text file. Touches the user's disk, so it is gated.

## Tools are not actions

Voxta has two ways for a character to *do* something:

- Action Inference: Runs before or after the reply, every time, and can be gated by flags.
- Tool Calling: Runs while the model is writing a reply, zero or more times.

## Permission classes

Whether the user will be asked for permission.

| Class | Means | Gated? |
| --- | --- | --- |
| `InChat` | changed the conversation and nothing else | no |
| `Read` | looked at something outside the chat, changed nothing | no |
| `Act` | something outside the chat happened, and it is undoable | yes |
| `Destructive` | may wreck or overwrite something | yes |

## Remembering "yes, always"

The text generation module is responsible for saving whether a user has been accepted.

## Important notes

- **Never throw an exception with a tool call.** Instead, return text with `ChatToolCallResult(..., IsError: true)`
  so the LLM can retry or do something different.
- **Do not trust the arguments.** They are the model's own JSON, untouched. A model that was told
  `"type": "integer"` still sends `"6"` on some backends, so read both — see `GetInt`.

## Building it

```bash
dotnet build
```

Then add this folder in **Add Modules → Add Custom Module** and **Apply** (see the
[modules README](../README.md#installing-one-on-a-server)), and enable the `examples.tools` augmentation on a
character or in a chat. You need a model that supports tool calling.
