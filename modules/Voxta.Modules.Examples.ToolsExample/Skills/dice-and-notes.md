---
name: dice-and-notes
description: "When to roll dice and how to read the result back, and what belongs in the user's notes file. Load before a game of chance or before writing a note."
metadata:
  augmentations: examples.tools
  tools: roll_dice write_note
---

# Dice and notes

## Rolling

- Roll whenever chance should decide: a game, a skill check, a choice the user left to fate. Never make up a number.
- `roll_dice` takes `sides` (4, 6, 8, 10, 12, 20 or 100) and `count`. "2d6" is `sides` 6, `count` 2.
- Say what was rolled before what it means: "You rolled 4 and 5, that's 9. The door holds."
- A modifier ("+2") is added by you, after the roll, and said out loud.

## Notes

- `write_note` appends one line to the user's notes file. It asks the user first.
- Write it only when the user asks for something to be kept: a score, a name, a decision.
- One line, readable without the conversation: "Game night: Sam won 3 rounds to 2."
