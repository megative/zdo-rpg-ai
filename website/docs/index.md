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
  lead: Walk up to anyone in Vvardenfell, hold a key and say what you want. They answer in their own voice, remember you, haggle, follow you or pick a fight, gossip about what you did, and keep the game's quests intact.
  primary: Get started
  primary_link: install/
  secondary: Source on GitHub
  note: Windows · Linux · macOS — Claude, OpenAI, Gemini or local models
  image_alt: Fargoth answering the player in Seyda Neen, with a subtitle at the bottom of the screen
  chip_top: Fargoth, in his own voice
  chip_bottom: hold and speak
---

<div class="zdo-section" markdown>

<p class="zdo-kicker">What it does</p>

## Every NPC becomes a conversation

<p class="zdo-intro">Not a chatbot bolted onto the game: NPCs see the world around them and act in it, so what they say has weight.</p>

<div class="grid cards zdo-features" markdown>

-   :material-microphone:{ .lg } __Talk with your voice__

    ---

    Hold the push-to-talk key and speak, in any language. The NPC stops, turns to you, thinks for a moment and answers out loud, with subtitles and a voice that suits their race and sex.

-   :material-brain:{ .lg } __They remember you__

    ---

    Each NPC gets a character of their own and remembers what you said and did, separately for every save. Come back later and they greet you first.

-   :material-sack:{ .lg } __They act, not just talk__

    ---

    Gifts, payments and bribes move real items and gold, checked against both inventories. NPCs follow you, fight, warm up or cool down to you, and open their trade, training, travel, spell and repair windows.

-   :material-account-group:{ .lg } __The town talks__

    ---

    Go quiet, and people nearby chat among themselves. Your deeds travel as rumors: insult a guard, and the whole village knows by evening.

-   :material-book-open-variant:{ .lg } __Quests stay intact__

    ---

    NPCs read your journal and know the quest steps the game gives through them. When a quest is at stake, they hand you over to the game's own dialogue and name the topic to pick. They also give small tasks of their own, with real rewards.

-   :material-feather:{ .lg } __True to the lore__

    ---

    Mention a place or a topic, and the NPC recalls what they would say about it in the original dialogue, then tells it in their own words.

-   :material-robot-outline:{ .lg } __Your choice of AI__

    ---

    Claude, OpenAI or Gemini for conversations, OpenAI or Deepgram for speech, OpenAI or ElevenLabs for voices. Or everything local through Ollama and speaches, with nothing leaving your PC.

-   :material-monitor:{ .lg } __Windows, Linux, macOS__

    ---

    Runs wherever OpenMW does, including its Windows build under Wine or Proton. The game itself watches the push-to-talk key, so it works under Wayland too.

</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Unscripted</p>

## An evening in Seyda Neen

<p class="zdo-intro">The outlander fresh off the boat has called the guards dogs one time too many. Fargoth, who insists he is the mayor, sent him to apologize.</p>

<div class="zdo-chat">
  <div class="zdo-line zdo-line--you">
    <span class="zdo-who">You, to Sellus Gravius</span>
    <p>The mayor told me to apologize for calling the guards dogs. I'm really sorry. I won't do it again.</p>
  </div>
  <div class="zdo-line">
    <span class="zdo-who">Sellus Gravius</span>
    <p>I heard you. Apology accepted. That's how it's done, without any "bro". Just remember: call anyone a dog again and no apology will save you. Now off to the docks — Cosades is waiting for you in Balmora.</p>
    <div class="zdo-acts"><span class="zdo-act">disposition +5: a sincere apology</span></div>
  </div>
  <div class="zdo-line zdo-line--you">
    <span class="zdo-who">You, to Fargoth</span>
    <p>Fargoth, I apologized. I want my torch.</p>
  </div>
  <div class="zdo-line">
    <span class="zdo-who">Fargoth</span>
    <p>Yes, yes, I heard! Sellus says you mumbled like a drunken netch, but he accepted it. The mayor keeps his word: here's your torch, take it!</p>
    <div class="zdo-acts"><span class="zdo-act">gives you a torch</span><span class="zdo-act">task done: apologize to Sellus Gravius</span></div>
  </div>
  <p class="zdo-scene">A few minutes later, while you stand nearby</p>
  <div class="zdo-line zdo-line--aside">
    <span class="zdo-who">Fargoth, to a guard</span>
    <p>Did you hear? I apologized to Ganciel Duar myself! Properly, in front of witnesses. That's what a true mayor of Seyda Neen does, isn't it?</p>
  </div>
  <div class="zdo-line zdo-line--aside">
    <span class="zdo-who">Imperial guard</span>
    <p>Nobody elected you mayor, Fargoth. But you apologized, and rightly so. Fewer fights by the lighthouse.</p>
  </div>
</div>

<p class="zdo-caption">Lines from a real playthrough, translated from Russian. Nothing here is scripted: the town remembered the insults, passed the news around, and Fargoth took the credit.</p>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Under the hood</p>

## Three parts, one conversation

<p class="zdo-intro">A small mod lives in the game. Two console programs on your PC do the listening and the thinking.</p>

<div class="zdo-flow" markdown>

<div class="zdo-node" markdown>
:material-sword-cross:

### The game

<p>OpenMW with the Lua mod: knows who is around, what they carry and what your journal says. Shows subtitles, plays voices, carries out NPC actions.</p>
</div>

<div class="zdo-arrow">⇄</div>

<div class="zdo-node" markdown>
:material-microphone-outline:

### The client

<p>Records your voice while you hold the key and passes messages between the game and the server.</p>
</div>

<div class="zdo-arrow">⇄</div>

<div class="zdo-node" markdown>
:material-head-cog-outline:

### The server

<p>Turns speech into text, asks the AI what the NPC says and does, voices the answer and keeps every NPC's memory. Can run on another machine.</p>
</div>

</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Get started</p>

## Pick your system

<p class="zdo-intro">You need Morrowind with OpenMW 0.50 or newer, a microphone, and keys for the AI services or local models.</p>

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

    .NET and ffmpeg from Homebrew, one script to start.

    [:octicons-arrow-right-24: Install on macOS](install/macos.md)

</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Credits</p>

## Built by many hands

<div class="zdo-credits" markdown>

- [drzdo](https://github.com/drzdo) created Zdo RPG AI and its OpenMW mod, an evolution of [immersive_morrowind_llm_ai](https://github.com/drzdo/immersive_morrowind_llm_ai).
- [waterbobo24](https://github.com/waterbobo24) brought richer NPC and player context, in-game time, item transfers and per-save memory in their forks [Morrowind-llm-server-client](https://github.com/waterbobo24/Morrowind-llm-server-client) and [Morrowind-llm-mod](https://github.com/waterbobo24/Morrowind-llm-mod).
- [megative](https://github.com/megative) maintains this fork: voices, Claude, quests, tasks, banter, rumors and more.

Zdo RPG AI is free software under the [GPL-3.0](https://github.com/megative/zdo-rpg-ai/blob/main/LICENSE). Morrowind is a trademark of Bethesda Softworks; this project is not affiliated with Bethesda or the OpenMW team.

</div>

</div>
