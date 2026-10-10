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

<p class="zdo-kicker">How to use it</p>

## The new features, step by step { #guide }

<p class="zdo-intro">Each tab takes you from the first step to what you'll see in the game, with an example from Seyda Neen. The server and the client are running, and a save is loaded.</p>

<div class="zdo-guide" markdown>

=== "Typing"

    **Why.** To say something without saying it out loud: at night, without a microphone, or when speech recognition mangles names like Caius Cosades and Hlaalu.

    1. Look at whoever you're writing to and press ++enter++.
    2. The game pauses, the cursor appears, and a box with a line opens at the bottom. Its header says who gets the line: an NPC's name, or "whoever is near" if you weren't looking at anyone.
    3. Click the line. Without a click, nothing you type gets in: Lua in OpenMW can't put the cursor there on its own.
    4. Type, and press ++enter++. The box closes and the game goes on.

    **What you'll see.** Your line flashes at the bottom right, like recognized speech; the NPC stops, thinks and answers out loud. It also stays in the log, on the conversation page.

    **Example.** Walk up to Arrille in his shop, press ++enter++, click the line and type: "How much for an iron dagger?"

    **If it doesn't work.**

    - The line won't open: close the inventory, the journal or the console first; it doesn't open over other windows. If ++enter++ already does something for you, set another key in the client's `textInput.key`.
    - Nothing types: click the line itself.
    - Changed your mind: ++esc++ closes the box and sends nothing. An empty line sends nothing either.
    - While the line is open, the talk key is off, so a ++y++ in your text won't start recording.

=== "The log"

    **Why.** To recall what you were given, what you were asked to do and who said what, without leaving the game.

    1. Press ++l++. Your open tasks with their rewards show up, and the latest 12 events, newest on top: items and gold, changes of heart with the reason, who came along, who started a fight.
    2. Press ++l++ again. The conversation shows up: the latest 14 lines in order, with the time, who said what to whom. Your lines are signed "You", NPCs chatting among themselves get an arrow, like "Fargoth → Arrille".
    3. Press ++l++ a third time to close it.

    **What else you'll see.** Every event first pops up as a notification. When a quest chain moves on, its task in the log is rewritten to the next step, and the notification starts with "Next in ... task".

    **Good to know.**

    - The log lives in your save: load an old game and it shows what was true then. It keeps 50 events and 50 lines.
    - The key does nothing in menus or the console, so it won't get in the way.
    - Change the key in the client's `eventLog.key`.

=== "Companions"

    **Why.** To take someone along and not walk in silence.

    1. Find someone to ask. Hrisskar, say, hangs around Arrille's shop.
    2. Ask them to come along. Many will want money: "Will you see me to Balmora? I'll pay twenty septims." If you agree on a price, the NPC takes the gold themselves.
    3. If they agree, "Hrisskar follows you" pops up and they fall in behind. From now on they're your companion, and the server remembers them in this save.
    4. Go where you were going. When you arrive together in another town or region, the companion has a word about the place or the road. Doors within one town don't count.
    5. On a long quiet road they sometimes speak up first: when you've been silent for at least 45 seconds and they're within 15 metres.
    6. Enough? Say "Go back home" or "Wait here". "Hrisskar no longer follows you" pops up.

    **Good to know.**

    - A companion knows they travel with you and remembers the road. Ask what they think of Balmora.
    - Fall out badly enough to fight, and they're no longer a companion.
    - A companion who fell behind stays quiet: only one close by can speak.
    - How often they talk is set in the panel, under "Companions", where you can also turn them off.

=== "Cutting in"

    **Why.** So that a conversation in a crowd is a conversation in a crowd.

    1. Talk to someone while others stand around: in a tavern, a shop, by a gate.
    2. The NPC answers as usual.
    3. Someone within six metres of them who heard the answer may add one line: agree, object, chip in. With nothing to say, they keep quiet.

    **Good to know.**

    - It doesn't happen every time. The chance is set in the panel, under "Cutting in", 0.3 by default. Set it to 1 to hear how it sounds.
    - Only after your line, not after greetings. Speak again first, and nobody cuts in.
    - Whoever cuts in speaks from what they know, and does nothing: no items, no fights.

=== "Who knows what"

    **Why.** So that secrets stay secret and gossip travels the way it would.

    - **A voice carries 10 metres.** What you say is remembered by those within ten metres of you; what an NPC answers, by those close to them.
    - **Your journal.** An NPC sees your quest only if they take part in it, through their own lines in the game's dialogue.
    - **Secret factions.** That you're one of the Blades, the Thieves Guild or the Morag Tong is known only to fellow members. The Fighters and Mages Guilds, the Great Houses and the Legion are plain for all.
    - **Memory is everyone's own,** and separate for every save.

    **Example.** Ask Fargoth why you need Caius Cosades. He has no idea. Sellus Gravius knows, though: he's the one who sent you to Balmora.

    **How to use it.** Want to say something in secret? Step more than ten metres away from the crowd. Want everyone to hear? Say it in the square.

=== "Rumors"

    **Why.** So that the town notices what you do, but not the very second you do it.

    1. Do something others can see: give someone gold, pay someone, take someone along, get into a fight.
    2. Let a couple of game hours pass. Waiting or resting with ++t++ is easiest.
    3. Walk to someone at the other end of the same town and ask: "What's new?"

    **What you'll see.** Someone who didn't see it themselves tells it as news. NPCs chatting among themselves pick up rumors too.

    **What doesn't travel.** Words, tasks and grudges: only those involved and whoever stood close know them.

    **Good to know.** A town is everything before the comma in a cell's name: the streets of Seyda Neen and the houses in it share their gossip. An NPC tells of the latest three.

=== "Quest chains"

    **Why.** So that NPCs give not just "bring me two scamp skins" but small stories that send you to other people. It's an alpha for now: it may act up.

    1. Turn it on in the panel: "Director" → "Quests" → "NPCs give quests". No restart needed.
    2. Meet people: talk to whoever a quest could send you to. The server remembers where you talked with whom, and offers the 12 latest acquaintances to the quest giver.
    3. Ask someone for work: "Got anything for me?" If the NPC decides to give a quest, "New task from ..." pops up with its name and first step, like "A word for Caius: tell Caius Cosades in Balmora the package is on its way".
    4. Go to the person in the step and do what it says: pass the word on, ask, strike a deal.
    5. If they decide you really did it, the step counts: "Next in ... task" pops up with the next step, and the log is updated.
    6. When the steps run out, go back to whoever gave the quest. They close it and pay what they promised. Not a moment earlier.

    **Good to know.**

    - One or two steps, plus the trip back for the reward. A quest creates no new items: its steps are conversations.
    - The quest is known to the giver, the people in its steps and whoever stood close when it was given.
    - Stuck? Ask the giver to call it off, or turn the switch off.

=== "Settings panel"

    **Why.** To set up the server without opening the YAML by hand.

    1. While the server runs, open [localhost:8080](http://localhost:8080/) in your browser. The address is also in the server's first lines.
    2. "Language models": pick a service for the main and the fast model. The model list comes from the service itself, for your key; Claude's show their prices. Local servers have presets for Ollama and LM Studio.
    3. Press "Test connection". The model answers "Ping", speech recognition is tried on silence, and the voice says a sample line.
    4. "Player speech" and "NPC voices": the same for speech recognition and voices. Voices come as a list, and the test plays a sample.
    5. "Director": the NPCs' language, who your character is, greetings, banter, cutting in, companions, quests.
    6. Press "Save". Most of it works right away.

    **Good to know.**

    - Changes are written into `server-config.yaml`, with your comments left in place. Edit the file by hand and the panel picks it up.
    - The port and the database paths need a restart. For that, a bar with a "Restart the server" button shows at the top. Give the client the new port too.
    - Keys hide behind an eye. The panel answers on this computer only. Start the server with `--no-panel` to go without it.

=== "Testing a model"

    **Why.** To pick a model before playing, rather than finding out in Balmora that it answers in the wrong language and hands out daggers.

    1. Put the model into the server config, through the panel or by hand.
    2. In the `zdo-rpg-ai` folder, run:

        ```sh
        dotnet run --project src/ZdoRpgAi.ModEmulator.Console -- \
          --scenarios eval/scenarios --config .tmp/server-config.yaml --report report.md
        ```

    3. The emulator plays a little Seyda Neen for you: talks to Fargoth and Sellus, walks to Balmora, waits a couple of hours. You can follow along in the console.
    4. Open `report.md`: step by step, what the NPCs said and did, and which checks passed.

    **Good to know.**

    - A run spends a few tokens: a few dozen model requests.
    - `--only companion` plays only the scenarios with that word in their name.
    - Your own scenarios are plain YAML; the format is [on a page of its own](scenarios.md).

</div>

<p class="zdo-farewell">From here on, it's yours. Talk to anyone about anything, haggle, pick quarrels, hire escorts, cover your tracks. May the road of adventure keep leading you on. And mind your knees.</p>

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
