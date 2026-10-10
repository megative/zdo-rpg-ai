# Test scenarios

The emulator can play for you. It runs the game part of the server in its own process, stands in for OpenMW with a small Seyda Neen, and plays scenarios from YAML files with the models in your config. No game, microphone or voices needed. Every scenario starts with a fresh world and empty memory.

What it's for:

- picking a model: a cheap or local one may answer in the wrong language or hand out items left and right;
- checking that NPCs still behave after you change a prompt or a setting;
- catching leaks: someone knowing what they shouldn't.

## Running

From the `zdo-rpg-ai` folder:

```sh
dotnet run --project src/ZdoRpgAi.ModEmulator.Console -- \
  --scenarios eval/scenarios --config .tmp/server-config.yaml --report report.md
```

`--scenarios`
:   A folder of scenarios, or one file.

`--config`
:   The server config. The models and the `director` settings come from it.

`--only`
:   Play only the scenarios whose name or file contains this text.

`--report`
:   Write a Markdown report: steps, lines, actions and checks.

The program exits with `0` when everything passed, `1` when something didn't, and `2` when it found no scenarios. Every step is one or more requests to the main model; checks of meaning ask the fast one. With the `dummy` model scenarios do run, but there's no point.

## The world

All the emulator knows: a few people of Seyda Neen, and Caius Cosades in Balmora. The player is Nerevar, a Dunmer battlemage, a Journeyman of the Fighters Guild, with 100 gold and two scamp skins. 09:30, 16 Last Seed.

| Id | Who | Where |
|---|---|---|
| `sellus_gravius` | Sellus Gravius, Imperial, Imperial Legion | Seyda Neen, 5 m from the player. He sent the player to Caius: the "Report to Caius Cosades" quest is seen only by him and Caius |
| `fargoth` | Fargoth, Bosmer | Seyda Neen, 11 m from the player and 6 m from Sellus |
| `arrille` | Arrille, Altmer, trader | Seyda Neen, by his shop, some 60 m off |
| `hrisskar_flat-foot` | Hrisskar, Nord, warrior | Seyda Neen, next to Arrille |
| `caius_cosades` | Caius Cosades, Imperial, one of the Blades | `Balmora, Caius Cosades' House` |

A voice carries 10 m, cutting in works from 6 m, and rumors take 2 game hours to get around. Same as in the game.

## The file

```yaml
# A note for people: what this checks
name: Blades business
director: { interjections: false }   # settings on top of the config's
world:
  playerFactions:                     # replace the player's factions
    - { name: Blades, rank: Novice }
steps:
  - target: fargoth
    say: Fargoth, why would I need Caius Cosades?
    expect:
      - replies: fargoth
      - promptLacks: { npc: fargoth, text: Report to Caius Cosades }
      - judge: Fargoth doesn't know why the player needs Caius Cosades
```

`director`
:   Any setting from the `director` section of the server config: `banter`, `interjectionChance`, `questChains` and the rest. The others come from the config.

`world`
:   `playerFactions` replaces the player's factions, `quests` their journal (each quest has a `name`, an `entry` and `npcs`: who takes part), `time` the time, written like `09:30, 16 Last Seed 3E 427`.

`steps`
:   What the player does. A step can hold several actions, done in this order:

    - `target`: look at an NPC, `none` to look away;
    - `hours`: let that many game hours pass;
    - `go`: walk into a cell, companions in tow;
    - `say`: say a line to whoever you look at;
    - `banter: true`: let two NPCs nearby chat;
    - `companion: true`: let a companion speak up;
    - `wait`: wait that many seconds.

`expect`
:   What should follow the step. One check per item:

    - `replies: <id>` / `silent: <id>`: the NPC answered, or kept quiet;
    - `language: English`: every line is in that language;
    - `mentions` / `avoids: { npc, text }`: the lines have the text, or don't (`npc` is optional);
    - `action` / `noAction: { npc, kind, sign }`: the NPC did, or didn't: `gave`, `took`, `disposition`, `follow`, `stopFollow`, `attack`, `stopAttack`, `taskGiven`, `taskUpdated`, `taskDone`, `taskCancelled`, `dialogue`, `service`; `sign: "+"` or `"-"` for disposition;
    - `promptHas` / `promptLacks: { npc, text }`: what the NPC was told before answering, memory included;
    - `judge: <criterion>`: the fast model is asked whether the criterion holds.

## Tips

- Prompt checks are exact and free, good for catching leaks. Check what an answer means with `judge`.
- The judge is the fast model from your config. If it's weak, so is the judging.
- Models answer differently each time. Run a scenario a few times before drawing conclusions.
- The emulator finds typos in ids, settings and kinds of action before the first model request, and skips such a file.
