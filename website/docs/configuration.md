# Configure

Two files, both in YAML: `.tmp/server-config.yaml` for the server and `.tmp/client-config.yaml` for the client. Start from the examples in the repository's `example` folder; anything you leave out keeps its default.

## The settings panel

While the server runs, its settings open in the browser at [localhost:8080](http://localhost:8080/) (the address is in the server's first lines). Whatever you change there is written straight into `server-config.yaml`, with your comments left in place, and most of it takes effect at once. The panel also tests the connection to each service, lists the models and voices your keys give access to, and restarts the server when a setting needs it.

The panel answers on this computer only and asks for no password: it edits the same file you could edit by hand. To start the server without it, add `--no-panel`.

## Ready-made setups

The server uses two language models: **main** for conversations, where quality matters, and **simple** for small jobs like sketching an NPC's character, writing their banter, or guessing whom you're talking to, where speed and price matter. Speech recognition (`stt`) and voices (`tts`) are separate services.

=== "Claude + OpenAI voices"

    The best conversations: Claude Opus 5.5 talks, Claude Haiku 5.5 does the small jobs, OpenAI hears you and gives everyone a voice. Needs an [Anthropic](https://console.anthropic.com) key and an [OpenAI](https://platform.openai.com) key.

    ```yaml
    llm:
      main:
        provider: claude
        claude: { apiKey: "<Anthropic key>", model: claude-opus-5-5, effort: low }
      simple:
        provider: claude
        claude: { apiKey: "<Anthropic key>", model: claude-haiku-5-5, effort: low }

    stt:
      provider: openai
      openAi: { apiKey: "<OpenAI key>", model: whisper-1, language: en, baseUrl: https://api.openai.com }

    tts:
      provider: openai
      openAi: { apiKey: "<OpenAI key>", model: tts-1, voice: alloy, baseUrl: https://api.openai.com }
    ```

    `effort: low` keeps answers quick; higher levels think longer before answering.

=== "OpenAI only"

    One key for everything. The main model has to support tool calling, or NPCs can only talk.

    ```yaml
    llm:
      main:
        provider: openai
        openAi: { apiKey: "<OpenAI key>", model: gpt-4o, baseUrl: https://api.openai.com }
      simple:
        provider: openai
        openAi: { apiKey: "<OpenAI key>", model: gpt-4o-mini, baseUrl: https://api.openai.com }

    stt:
      provider: openai
      openAi: { apiKey: "<OpenAI key>", model: whisper-1, language: en, baseUrl: https://api.openai.com }

    tts:
      provider: openai
      openAi: { apiKey: "<OpenAI key>", model: tts-1, voice: alloy, baseUrl: https://api.openai.com }
    ```

=== "Gemini"

    Gemini for conversations; speech and voices still come from OpenAI.

    ```yaml
    llm:
      main:
        provider: gemini
        gemini: { apiKey: "<Google AI key>", model: gemini-2.5-flash, thinkingBudget: 0 }
      simple:
        provider: gemini
        gemini: { apiKey: "<Google AI key>", model: gemini-2.0-flash-lite, thinkingBudget: 0 }
    ```

=== "Fully local"

    No keys and no bills: [Ollama](https://ollama.com) for the models, [speaches](https://github.com/speaches-ai/speaches) for speech and voices. Every `openai` provider speaks the OpenAI format to `{baseUrl}/v1/...`, so any compatible local server works. The `apiKey` must not be empty, but local servers ignore it.

    ```yaml
    llm:
      main:
        provider: openai
        openAi: { apiKey: local, model: "qwen2.5:14b", baseUrl: "http://localhost:11434" }
      simple:
        provider: openai
        openAi: { apiKey: local, model: "qwen2.5:7b", baseUrl: "http://localhost:11434" }

    stt:
      provider: openai
      openAi: { apiKey: local, model: "Systran/faster-whisper-medium", language: en, baseUrl: "http://localhost:8000" }

    tts:
      provider: openai
      openAi: { apiKey: local, model: "<tts model>", voice: "<voice>", baseUrl: "http://localhost:8000" }
    ```

    NPC actions need a model with tool calling, such as Qwen 2.5 or Llama 3.1. Pick a voice that speaks your NPCs' language.

## The server

### Language and role-play

```yaml
director:
  responseLanguage: English
  playerBackstory: "A former Imperial prisoner who arrived in Seyda Neen by boat."
```

`responseLanguage`
:   The language NPCs speak. Set `stt.openAi.language` to match, so your speech is recognized in the same language.

`playerBackstory`
:   Optional: a line or two about who your character is, for NPCs to take into account.

### Greetings and banter

```yaml
director:
  greetings: true
  greetingCooldownMinutes: 10
  banter: true
  banterIntervalSeconds: 180
  banterChance: 0.5
```

`greetings`
:   NPCs who remember you speak first when you walk up, at most once per cooldown each.

`banter`
:   Every interval, with this chance, two NPCs near you chat with each other while you're quiet.

### Cutting in, companions, quests { #quests }

```yaml
director:
  interjections: true
  interjectionChance: 0.3
  companions: true
  companionTalkIntervalSeconds: 240
  companionTalkChance: 0.5
  questChains: false
```

`interjections`
:   After an NPC answers you, with this chance someone standing close who heard it may chime in, or keep quiet if they have nothing to say.

`companions`
:   Whoever follows you has a word on arriving in another town or region, and every interval, with this chance, when it has been quiet for a while.

`questChains`
:   Alpha. NPCs may give small quests of one or two steps that send you to people you've already talked to, then back for the reward. Off by default.

### Voices for every race

With OpenAI voices, each race and sex can get a voice of its own. The key is the first letter of the race plus `m` or `f`: `d` Dark Elf, `h` High Elf, `w` Wood Elf, `i` Imperial, `n` Nord, `b` Breton, `r` Redguard, `o` Orc, `a` Argonian, `k` Khajiit. Missing keys fall back to `voice`.

```yaml
tts:
  provider: openai
  openAi:
    apiKey: "<OpenAI key>"
    model: tts-1
    voice: alloy
    baseUrl: https://api.openai.com
    voiceMapping:
      byRaceSex:
        dm: onyx      # Dark Elf men
        df: nova      # Dark Elf women
        km: echo      # Khajiit men
        kf: shimmer   # Khajiit women
```

[ElevenLabs](https://elevenlabs.io) works the same way with `provider: elevenlabs` and `elevenLabs.voiceIdMapping`, using their voice ids. For speech recognition, [Deepgram](https://deepgram.com) is the alternative to OpenAI: `provider: deepgram`.

### Voice speed

```yaml
tts:
  mp3Speed:
    ffmpegExePath: ffmpeg
    charactersPerSecond: 15
```

Lines that would play slower than this reading speed get sped up with ffmpeg, so voices keep pace with the subtitles. If ffmpeg isn't on your `PATH`, give its full path.

### Memory

```yaml
director:
  compactThreshold: 30
  compactKeepRecent: 10
database:
  mainDbPath: main.db
  saveGameDbPath: save.db
```

When an NPC's history grows past `compactThreshold` events, older ones are summarized and the latest `compactKeepRecent` stay word for word. Each save game gets its own file next to `saveGameDbPath` (`save_<id>.db`) with everything NPCs remember and the characters they were given; `mainDbPath` holds what all saves share.

### Network

```yaml
httpServer:
  host: 127.0.0.1
  port: 8080
  clientToken: ""
```

Use `127.0.0.1` when the client runs on the same machine, `0.0.0.0` to accept clients from your network. With the server reachable from the network, set `clientToken` to a secret and put the same value into the client's `server.clientToken`.

A new port or host takes a server restart: the panel has a button for it. The client needs the same port in its `server.port`, and it reads its config only when it starts.

## The client { #client }

```yaml
server:
  host: localhost
  port: 8080
  clientToken: ""

mod:
  provider: openmw
  openmw:
    dataDir: /path/to/zdo-rpg-ai-openmw-mod/zdorpgai
    logFilePath: /path/to/openmw.log

voiceCapture:
  enabled: true
  deviceIndex: -1
  pushToTalk:
    key: Y
    subMode: hold
    releaseDelayMs: 400

stripDiacritics: false

eventLog:
  key: L

textInput:
  key: Enter
```

`server`
:   Where the server is. The port and the token must match the server's.

`mod.openmw.dataDir`
:   The mod's `zdorpgai` folder, the same one that's in `openmw.cfg`. The client writes messages and voice files into it.

`mod.openmw.logFilePath`
:   The `openmw.log` the game writes; the client reads the mod's messages from it. See where it is on [Windows](install/windows.md), [Linux](install/linux.md) and [macOS](install/macos.md).

`voiceCapture.deviceIndex`
:   `-1` takes the default microphone. The client lists all devices with their numbers when it starts.

`voiceCapture.pushToTalk.key`
:   The key you hold to speak: a letter, a digit, `F1` to `F12`, `LeftAlt`, `CapsLock`, `Space`, or any other name from OpenMW's [`input.KEY`](https://openmw.readthedocs.io/en/latest/reference/lua-scripting/openmw_input.html). The game watches it, so it works on any system.

`voiceCapture.pushToTalk.subMode`
:   `hold`: talk while the key is down. `toggle`: press once to start, once more to stop.

`voiceCapture.pushToTalk.releaseDelayMs`
:   How long recording goes on after you release the key, so the last word isn't cut off.

`stripDiacritics`
:   Removes accents from subtitles, for game fonts that lack them.

`eventLog.key`
:   Steps through the [log](playing.md#log): what NPCs did for you, then the conversation, then closed. Takes the same key names as `voiceCapture.pushToTalk.key`.

`textInput.key`
:   Opens a line to [type what to say](playing.md#typing) instead of speaking. Same key names.
