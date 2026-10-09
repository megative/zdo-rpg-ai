# Play

## Talking

1. Walk up to someone and look at them.
2. Hold ++y++ (or [your key](configuration.md#client)) and say what you want.
3. Release the key.

The NPC stops and turns to you, and their name with "…" shows while they think. Then they answer out loud, with subtitles. A long answer comes sentence by sentence; start talking again and they drop the rest of it.

If you aren't looking at anyone, everyone within earshot hears you, and the one you most likely addressed answers: by name, by what you said, or simply the closest.

## Things to try

| Say something like | What happens |
|---|---|
| "What's new around here?" | Local news, rumors about what you did in town, and what they know from the game's own dialogue. |
| "Here's twenty gold for your trouble." | The gold really leaves your purse, if you have it. |
| "Can you spare a potion?" | If they agree, the item lands in your inventory. They only give what they carry. |
| "Show me your wares." / "Can you train me?" | Their trade, training, travel, spell or repair window opens, if they offer that service. |
| "Come with me." / "Wait here." | They follow you, or stop following. |
| An insult | They like you less, the guards remember, the town gossips. Push it, and they attack. |
| "Do you have work for me?" | A task of their own, or a quest from the game. See below. |

## Tasks and quests

**Tasks** are the NPCs' own small requests: bring two scamp skins, apologize to someone, deliver a word. A notification shows when you get one, and again when it's done. The NPC remembers it, checks that you really did it, and pays what they promised. Tasks don't go into the journal.

**Quests** are the game's own, and the game stays in charge of them. NPCs see the open quests in your journal and know which quest steps the game gives through them. When a quest is at stake, the NPC says a few words, names the topic to pick and opens the game's dialogue window after their line. Pick that topic, and the quest moves on just as in the original game.

## A living town

- **Memory.** Each NPC remembers your conversations and deeds, separately for every save. Walk up again later, and those who remember you greet you first.
- **Character.** On the first conversation, every NPC gets a short character sketch that fits their race, class and place, and keeps it.
- **Banter.** Stand around quietly, and people nearby start talking to each other: about the news, about each other, about you.
- **Rumors.** What you say and do travels around town, so people you never met may already know about you.

## Good to know

- Speak in the language you set in `director.responseLanguage`, and set the same language for speech recognition.
- Changes to the mod's scripts take effect after a game restart, or after `reloadlua` in the game's console.
- Something off? See [Troubleshooting](troubleshooting.md).
