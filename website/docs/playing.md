# Playing

## How to talk

1. Walk up to someone and look at them.
2. Hold ++y++ (or [your key](configuration.md#client)) and say what you want.
3. Let go of the key.

The NPC stops and turns to you; while they think, their name and "…" show above the subtitles. Then they answer out loud, with subtitles. A long answer plays sentence by sentence; speak again and they won't finish it.

If you aren't looking at anyone, everyone within about ten metres hears you, and the one you most likely meant answers: by name, by what you said, or simply the closest.

### By typing { #typing }

Press ++enter++ (or [your key](configuration.md#client)). The game pauses and a line appears at the bottom. Click it, type, and press ++enter++ again: whoever you were looking at gets it. ++esc++ closes the line without sending anything.

You have to click the line yourself: Lua in OpenMW can't put the cursor there on its own.

## Things to try

| Say something like | What happens |
|---|---|
| "What's new around here?" | Local news, rumors, and what the NPC knows from the game's own dialogue. |
| "Here's twenty gold for your trouble." | The gold really leaves your purse, if you have it. |
| "Got a potion to spare?" | If the NPC agrees, the item shows up in your inventory. They can only give what they carry. |
| "Show me your wares." / "Can you train me?" | Their trade, training, travel, spell or repair window opens, if they offer that service. |
| "Come with me." / "Wait here." | The NPC follows you, or stops. One who comes along becomes a [companion](#companions). |
| An insult | The NPC likes you less, and whoever stood close remembers. Push too far and they attack. |
| "Any work for me?" | A task of their own, or a game quest — see below. |

## Tasks and quests

**Tasks** are small requests NPCs make themselves: bring two scamp skins, apologize to someone, deliver a message. You get a notification when you take one on and when you finish it. The NPC remembers the task, checks that you really did it, and pays what they promised. Tasks don't go into your journal, but the open ones are in the [log](#log).

**Quests** are the game's own, and the game still runs them. An NPC knows of your quest only if they take part in it, and they know the steps the game gives through them. When a quest is at stake, the NPC says a few words, names the topic, and opens the usual dialogue window after their line. Pick that topic and the quest moves on just as in the original game.

**Quest chains** are an alpha, off by default ([`questChains`](configuration.md#quests)). An NPC may give a small quest of one or two steps: go to someone you've already talked to, then come back for the reward. The step counts when the one you were sent to sees you really did what it asks. The log shows what to do next.

## The log { #log }

Everything an NPC does for or to you shows up as a notification: the torch Fargoth gave you, the gold you paid, a new task and its reward, a change of heart, a companion joining you, a fight breaking out.

++l++ (or [your key](configuration.md#client)) steps through the log window:

1. First press: your open tasks with their rewards, and the latest notifications, newest on top.
2. Second press: the conversation, the latest lines in order, who said what to whom, yours included.
3. Third press: closed.

Both are kept in your save, so after a load they match what happened in it.

## A living town

- **Memory.** Every NPC remembers your conversations and what you did, separately for each save. Come back later, and those who remember you greet you first.
- **Character.** At the first talk each NPC gets a short character sketch to fit their race, class and place, and sticks to it.
- **Who knows what.** An NPC knows what they saw and heard themselves: a conversation carries about ten metres. Your journal is seen only by those taking part in a quest, and membership in a secret faction (the Blades, the Thieves Guild, the Morag Tong) only by fellow members.
- **Banter.** Stand by quietly, and the neighbours talk among themselves: news, each other, you.
- **Cutting in.** Whoever stands close and heard an NPC answer you may chime in. With nothing to say, they keep quiet.
- **Rumors.** What others can see (gifts, payments, fights, who left with whom) is known around town a couple of game hours later. Tasks and grudges stay between you, the NPC, and whoever stood close.

## Companions { #companions }

Whoever agreed to come with you becomes a companion. When you arrive together in another town or region, they have a word about the place or the road, and on a long quiet walk they sometimes speak up first. The server remembers companions in your save. Send them home or fall out badly enough to fight, and they stop being one.

## Good to know

- Speak the language set in `director.responseLanguage`, and set speech recognition to the same language.
- Changes to the mod's scripts take effect after restarting the game, or after `reloadlua` in its console.
- Something wrong? See [Troubleshooting](troubleshooting.md).
