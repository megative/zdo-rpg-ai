# Настройка

Два файла в формате YAML: `.tmp/server-config.yaml` для сервера и `.tmp/client-config.yaml` для клиента. Начни с примеров из папки `example` в репозитории; всё, что не указано, берёт значение по умолчанию.

## Панель настроек

Пока сервер работает, его настройки открываются в браузере: [localhost:8080](http://localhost:8080/) (адрес есть в первых строках сервера). Всё, что поменяешь там, пишется прямо в `server-config.yaml`, твои комментарии остаются на месте, и почти всё действует сразу. Ещё панель проверяет связь с каждым сервисом, показывает модели и голоса, доступные по твоим ключам, и перезапускает сервер, когда настройка без этого не заработает.

Панель отвечает только на этом компьютере и пароля не спрашивает: она правит тот же файл, что и ты руками. Чтобы запустить сервер без неё, добавь `--no-panel`.

## Готовые наборы

Сервер использует две языковые модели: **main** для разговоров, где важно качество, и **simple** для мелких задач — набросать характер NPC, сочинить их болтовню, понять, к кому ты обращаешься, — где важны скорость и цена. Распознавание речи (`stt`) и голоса (`tts`) — отдельные сервисы.

=== "Claude + голоса OpenAI"

    Лучшие разговоры: говорит Claude Opus 5.5, мелочами занимается Claude Haiku 5.5, а OpenAI слышит тебя и даёт всем голоса. Нужны ключи [Anthropic](https://console.anthropic.com) и [OpenAI](https://platform.openai.com).

    ```yaml
    llm:
      main:
        provider: claude
        claude: { apiKey: "<ключ Anthropic>", model: claude-opus-5-5, effort: low }
      simple:
        provider: claude
        claude: { apiKey: "<ключ Anthropic>", model: claude-haiku-5-5, effort: low }

    stt:
      provider: openai
      openAi: { apiKey: "<ключ OpenAI>", model: whisper-1, language: ru, baseUrl: https://api.openai.com }

    tts:
      provider: openai
      openAi: { apiKey: "<ключ OpenAI>", model: tts-1, voice: alloy, baseUrl: https://api.openai.com }

    director:
      responseLanguage: Russian
    ```

    `effort: low` даёт быстрые ответы; на уровнях выше модель дольше думает перед ответом.

=== "Только OpenAI"

    Один ключ на всё. Основная модель должна уметь вызывать инструменты, иначе NPC смогут только говорить.

    ```yaml
    llm:
      main:
        provider: openai
        openAi: { apiKey: "<ключ OpenAI>", model: gpt-4o, baseUrl: https://api.openai.com }
      simple:
        provider: openai
        openAi: { apiKey: "<ключ OpenAI>", model: gpt-4o-mini, baseUrl: https://api.openai.com }

    stt:
      provider: openai
      openAi: { apiKey: "<ключ OpenAI>", model: whisper-1, language: ru, baseUrl: https://api.openai.com }

    tts:
      provider: openai
      openAi: { apiKey: "<ключ OpenAI>", model: tts-1, voice: alloy, baseUrl: https://api.openai.com }
    ```

=== "Gemini"

    Gemini для разговоров; речь и голоса по-прежнему от OpenAI.

    ```yaml
    llm:
      main:
        provider: gemini
        gemini: { apiKey: "<ключ Google AI>", model: gemini-2.5-flash, thinkingBudget: 0 }
      simple:
        provider: gemini
        gemini: { apiKey: "<ключ Google AI>", model: gemini-2.0-flash-lite, thinkingBudget: 0 }
    ```

=== "Всё локально"

    Ни ключей, ни счетов: [Ollama](https://ollama.com) для моделей, [speaches](https://github.com/speaches-ai/speaches) для речи и голосов. Любой провайдер `openai` говорит в формате OpenAI с `{baseUrl}/v1/...`, так что подойдёт любой совместимый локальный сервер. `apiKey` не должен быть пустым, но локальные серверы его не проверяют.

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
      openAi: { apiKey: local, model: "Systran/faster-whisper-medium", language: ru, baseUrl: "http://localhost:8000" }

    tts:
      provider: openai
      openAi: { apiKey: local, model: "<модель tts>", voice: "<голос>", baseUrl: "http://localhost:8000" }
    ```

    Для действий NPC нужна модель с вызовом инструментов, например Qwen 2.5 или Llama 3.1. Голос выбирай говорящий на языке твоих NPC — для русского подойдёт один из голосов Piper `ru_RU`.

## Сервер

### Язык и отыгрыш

```yaml
director:
  responseLanguage: Russian
  playerBackstory: "Бывший имперский заключённый, прибывший в Сейда Нин на корабле."
```

`responseLanguage`
:   Язык, на котором говорят NPC. Поставь такой же `stt.openAi.language` (например, `ru`), чтобы твоя речь распознавалась на том же языке.

`playerBackstory`
:   Необязательно: пара строк о том, кто твой персонаж, — NPC будут это учитывать.

### Приветствия и болтовня

```yaml
director:
  greetings: true
  greetingCooldownMinutes: 10
  banter: true
  banterIntervalSeconds: 180
  banterChance: 0.5
```

`greetings`
:   NPC, которые тебя помнят, заговаривают первыми, когда ты подходишь, — не чаще раза за `greetingCooldownMinutes`.

`banter`
:   Раз в интервал с заданной вероятностью два NPC рядом с тобой болтают между собой, пока ты молчишь.

### Реплики, спутники, квесты { #quests }

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
:   После ответа NPC с этой вероятностью кто-то, кто стоит рядом и слышал, может вставить своё. Если сказать нечего, промолчит.

`companions`
:   Кто идёт с тобой, скажет пару слов, когда вы придёте в другой город или регион, а ещё раз в интервал с этой вероятностью, если долго было тихо.

`questChains`
:   Альфа. NPC могут давать небольшие квесты в один-два шага: к тем, с кем ты уже говорил, и обратно за наградой. По умолчанию выключено.

### Голоса для каждой расы

С голосами OpenAI у каждой расы и пола может быть свой голос. Ключ — первая буква расы (по-английски) плюс `m` или `f`: `d` тёмный эльф, `h` высокий эльф, `w` лесной эльф, `i` имперец, `n` норд, `b` бретонец, `r` редгард, `o` орк, `a` аргонианин, `k` каджит. Если ключа нет, берётся `voice`.

```yaml
tts:
  provider: openai
  openAi:
    apiKey: "<ключ OpenAI>"
    model: tts-1
    voice: alloy
    baseUrl: https://api.openai.com
    voiceMapping:
      byRaceSex:
        dm: onyx      # тёмные эльфы
        df: nova      # тёмные эльфийки
        km: echo      # каджиты
        kf: shimmer   # каджитки
```

[ElevenLabs](https://elevenlabs.io) настраивается так же: `provider: elevenlabs` и `elevenLabs.voiceIdMapping` с их идентификаторами голосов. Для распознавания речи вместо OpenAI можно взять [Deepgram](https://deepgram.com): `provider: deepgram`.

### Скорость голоса

```yaml
tts:
  mp3Speed:
    ffmpegExePath: ffmpeg
    charactersPerSecond: 15
```

Реплики, которые звучали бы медленнее этой скорости чтения, ускоряются через ffmpeg, чтобы голос поспевал за субтитрами. Если ffmpeg нет в `PATH`, укажи полный путь к нему.

### Память

```yaml
director:
  compactThreshold: 30
  compactKeepRecent: 10
database:
  mainDbPath: main.db
  saveGameDbPath: save.db
```

Когда история NPC перерастает `compactThreshold` событий, старые сжимаются в краткий пересказ, а последние `compactKeepRecent` остаются дословно. У каждого сохранения свой файл рядом с `saveGameDbPath` (`save_<id>.db`): всё, что помнят NPC, и их характеры; в `mainDbPath` — общее для всех сохранений.

### Сеть

```yaml
httpServer:
  host: 127.0.0.1
  port: 8080
  clientToken: ""
```

`127.0.0.1` — если клиент работает на этом же компьютере, `0.0.0.0` — чтобы принимать клиентов из сети. Если сервер виден из сети, задай `clientToken` — секретную строку — и то же значение в `server.clientToken` клиента.

Новый порт или адрес начинают работать после перезапуска сервера, в панели для этого есть кнопка. Клиенту нужен тот же порт в `server.port`, а свой конфиг он читает только при запуске.

## Клиент { #client }

```yaml
server:
  host: localhost
  port: 8080
  clientToken: ""

mod:
  provider: openmw
  openmw:
    dataDir: /путь/к/zdo-rpg-ai-openmw-mod/zdorpgai
    logFilePath: /путь/к/openmw.log

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
:   Где сервер. Порт и токен должны совпадать с серверными.

`mod.openmw.dataDir`
:   Папка мода `zdorpgai` — та же, что в `openmw.cfg`. Клиент пишет в неё сообщения и файлы голосов.

`mod.openmw.logFilePath`
:   `openmw.log`, который пишет игра; из него клиент читает сообщения мода. Где он лежит — на страницах для [Windows](install/windows.md), [Linux](install/linux.md) и [macOS](install/macos.md).

`voiceCapture.deviceIndex`
:   `-1` — микрофон по умолчанию. При запуске клиент перечисляет все устройства с номерами.

`voiceCapture.pushToTalk.key`
:   Клавиша, которую держишь, когда говоришь: буква, цифра, `F1`–`F12`, `LeftAlt`, `CapsLock`, `Space` или любое другое имя из [`input.KEY`](https://openmw.readthedocs.io/en/latest/reference/lua-scripting/openmw_input.html) OpenMW. Её отслеживает игра, так что работает на любой системе.

`voiceCapture.pushToTalk.subMode`
:   `hold` — говоришь, пока клавиша зажата. `toggle` — нажал один раз, чтобы начать, второй — чтобы закончить.

`voiceCapture.pushToTalk.releaseDelayMs`
:   Сколько ещё идёт запись после того, как отпустил клавишу, чтобы не обрезать последнее слово.

`stripDiacritics`
:   Убирает диакритику из субтитров — для игровых шрифтов, где её нет.

`eventLog.key`
:   Переключает [лог](playing.md#log): что NPC для тебя сделали, потом разговор, потом закрыть. Имена клавиш — те же, что у `voiceCapture.pushToTalk.key`.

`textInput.key`
:   Открывает строку, чтобы [написать фразу](playing.md#typing) вместо того, чтобы говорить. Имена клавиш те же.
