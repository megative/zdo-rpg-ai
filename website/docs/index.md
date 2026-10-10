---
template: home.html
title: Morrowind talks back
hide:
  - navigation
  - toc
hero:
  kicker: Zdo RPG AI · a mod for OpenMW
  title: Morrowind
  title_accent: talks back.
  lead: Hold Y and talk to anyone in Vvardenfell. They answer in their own voice, remember you, and when you strike a deal, they actually do it.
  primary: Get started
  primary_link: install/
  secondary: Source on GitHub
  note: Windows · Linux · macOS · Claude, OpenAI, Gemini or local models
  image_alt: Fargoth answering the player in Seyda Neen, with a subtitle at the bottom of the screen
  chip_top: Fargoth, in his own voice
  chip_bottom: hold and speak
---

<div class="zdo-section" markdown>

<p class="zdo-kicker">What it does</p>

## What NPCs do

<p class="zdo-intro">Nothing is scripted: every line comes from the model you pick. So sometimes it's funny, and sometimes it misses.</p>

<div class="grid cards zdo-features" markdown>

-   :material-microphone:{ .lg } __Voice or text__

    ---

    Hold ++y++ and speak, in any language. Rather not talk out loud? Press ++enter++ and type. The NPC stops, turns to you and answers with a voice that suits their race, with subtitles.

-   :material-brain:{ .lg } __They remember you__

    ---

    Everyone has a character of their own and a memory of you, separate for every save. Be rude today, and tomorrow they greet you through their teeth.

-   :material-eye-off-outline:{ .lg } __They know only their part__

    ---

    Your journal is seen by those who take part in the quest. That you're one of the Blades, only the Blades know. What you told a guard was heard by whoever stood close, not the whole village.

-   :material-sack:{ .lg } __They act on it__

    ---

    They hand over items and gold, take payment, follow you, fight, and open their trade, training and travel windows. The game checks everything: nobody gives what isn't in their pocket.

-   :material-hiking:{ .lg } __Companions__

    ---

    Take someone along. In a new town they have a word about the place, and on a long quiet road they speak up first.

-   :material-account-group:{ .lg } __The town has a life__

    ---

    Stand by quietly, and the neighbours start chatting. Whoever stands close may cut into your conversation. A fight or a generous gift reaches the whole town a couple of game hours later.

-   :material-book-open-variant:{ .lg } __Tasks and quests__

    ---

    NPCs give tasks of their own, with real rewards. The game's quests stay intact: NPCs talk about them in their own words and open the game's dialogue. Small multi-step quests are there to try, too.

-   :material-robot-outline:{ .lg } __Any model__

    ---

    Claude, OpenAI, Gemini, or everything on your own PC through Ollama and speaches. Set it up in the browser: the server lists the models and checks your keys.

</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Controls</p>

## Three keys and a browser

<p class="zdo-intro">The keys change in the client's config. Server settings are edited in the browser and written into the same YAML file, comments and all.</p>

<div class="zdo-keys">
  <div class="zdo-key"><kbd>Y</kbd><p>Hold and speak. Let go, and the NPC thinks of an answer.</p></div>
  <div class="zdo-key"><kbd>Enter</kbd><p>Opens a line. Click it, type, and press Enter again.</p></div>
  <div class="zdo-key"><kbd>L</kbd><p>The log: what you got and were asked to do. Again: who said what. A third time closes it.</p></div>
  <div class="zdo-key"><kbd>localhost:8080</kbd><p>The settings panel: models, voices, how NPCs behave. Open while the server runs.</p></div>
</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Things to try</p>

## Scenes for a first evening

<p class="zdo-intro">Start in Seyda Neen, where everyone is close by. It turns out a little different every time, which is the point.</p>

<div class="grid cards" markdown>

-   :material-map-marker-path:{ .lg } __Hire an escort__

    ---

    Walk up to Hrisskar and offer twenty septims to see you to Balmora. If he agrees, he follows you, and in Balmora he has a word about the road. Changed your mind? Tell him to go home.

-   :material-incognito:{ .lg } __See who keeps a secret__

    ---

    Ask Fargoth why you need Caius Cosades. He has no idea. Sellus Gravius knows, though: he's the one who sent you.

-   :material-account-voice:{ .lg } __Make a scene__

    ---

    Insult a guard. He cools toward you, and Fargoth, if he stands close, may chip in. The words go no further, but a fight is the talk of Seyda Neen a couple of game hours later.

-   :material-keyboard-outline:{ .lg } __Type instead of talking__

    ---

    Press ++enter++, click the line and ask what an iron dagger costs. Handy at night when the house is asleep, and for names that speech recognition mangles.

-   :material-script-text-outline:{ .lg } __Read back what happened__

    ---

    Press ++l++: your tasks and everything you were given. Again: the latest lines, who said what to whom. The log lives in your save, so after a load it matches your inventory again.

-   :material-chart-timeline-variant:{ .lg } __Take a quest that goes places__

    ---

    Turn on "NPCs give quests" in the panel. Talk to someone in Balmora, come back, and ask Sellus for work. He may send you to someone you've met, then back to him for the reward. It's an alpha for now.

</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Before you play</p>

## Test a model on scenarios

<p class="zdo-intro">Not sure which model to pick? The emulator plays a little Seyda Neen for you: it talks, walks to Balmora, waits a few hours. Then it checks that the NPCs answered in your language, kept their secrets and didn't start fights for nothing.</p>

<div class="zdo-run" markdown>

```sh
dotnet run --project src/ZdoRpgAi.ModEmulator.Console -- \
  --scenarios eval/scenarios --config .tmp/server-config.yaml --report report.md
```

You get a report: what passed, what didn't, and how long the model thought. A run spends a few tokens of the model in your config. Scenarios are plain YAML; how to write your own is [on a page of its own](scenarios.md).

</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Under the hood</p>

## Three parts, one conversation

<p class="zdo-intro">A small mod lives in the game. Two console programs on your PC do the listening and the thinking.</p>

<div class="zdo-flow" markdown>

<div class="zdo-node" markdown>
:material-sword-cross:

### The game

<p>OpenMW with the Lua mod. Knows who is around and what they carry, shows subtitles, plays voices and does what you agreed on with an NPC.</p>
</div>

<div class="zdo-arrow">⇄</div>

<div class="zdo-node" markdown>
:material-microphone-outline:

### The client

<p>Records your voice while you hold the key and carries messages between the game and the server.</p>
</div>

<div class="zdo-arrow">⇄</div>

<div class="zdo-node" markdown>
:material-head-cog-outline:

### The server

<p>Turns speech into text, asks the model what the NPC says and does, voices the answer and keeps the memory. Can live on another machine.</p>
</div>

</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Get started</p>

## Pick your system

<p class="zdo-intro">You need Morrowind, OpenMW 0.50 or newer, a microphone or at least a keyboard, and keys for AI services or local models.</p>

<div class="grid cards" markdown>

-   :material-microsoft-windows:{ .lg } __Windows__

    ---

    .NET and ffmpeg from winget, two PowerShell windows.

    [:octicons-arrow-right-24: Install on Windows](install/windows.md)

-   :material-linux:{ .lg } __Linux__

    ---

    Native OpenMW, Flatpak, or the Windows build under Wine or Proton.

    [:octicons-arrow-right-24: Install on Linux](install/linux.md)

-   :material-apple:{ .lg } __macOS__

    ---

    OpenMW from GitHub, .NET and ffmpeg from Homebrew, one script to start.

    [:octicons-arrow-right-24: Install on macOS](install/macos.md)

</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Credits</p>

## Built by many hands

<div class="zdo-credits" markdown>

- [drzdo](https://github.com/drzdo) created Zdo RPG AI and its OpenMW mod, an evolution of [immersive_morrowind_llm_ai](https://github.com/drzdo/immersive_morrowind_llm_ai).
- [waterbobo24](https://github.com/waterbobo24) brought richer NPC and player context, in-game time, item transfers and per-save memory in their forks [Morrowind-llm-server-client](https://github.com/waterbobo24/Morrowind-llm-server-client) and [Morrowind-llm-mod](https://github.com/waterbobo24/Morrowind-llm-mod).
- [megative](https://github.com/megative) maintains this fork: voices, Claude, tasks and quests, banter, rumors, companions, the in-game log, typing, the settings panel and test scenarios.

Zdo RPG AI is free software under the [GPL-3.0](https://github.com/megative/zdo-rpg-ai/blob/main/LICENSE). Morrowind is a trademark of Bethesda Softworks; this project is not affiliated with Bethesda or the OpenMW team.

</div>

</div>
