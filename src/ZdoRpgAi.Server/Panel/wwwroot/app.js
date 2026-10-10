'use strict';

// The Zdo RPG AI web panel: a handy editor for the server's YAML config. Shows the settings as forms
// and sends only what changed; the server writes it into the file, which stays editable by hand, and
// the panel picks up such edits too.
(() => {
  // ------------------------------------------------------------------ texts

  const STRINGS = {
    ru: {
      subtitle: 'панель управления',
      contents: 'Разделы',
      clientOn: 'клиент подключён',
      clientOff: 'клиента нет',
      save: 'сохранение',
      fileNote: 'Всё пишется в этот файл, комментарии в нём не трогаются. Править его можно и руками: панель подхватит.',
      loading: 'Загрузка…',
      unreachable: 'Сервер не отвечает',
      unreachableText: 'Проверь, что сервер запущен, и обнови страницу.',
      changes: n => `${n} ${plural(n, 'изменение', 'изменения', 'изменений')}`,
      inFile: f => `запишется в ${f}`,
      discard: 'Отменить',
      saveBtn: 'Сохранить',
      saving: 'Сохраняю…',
      saved: 'Сохранено',
      applied: list => `Уже работает: ${list}.`,
      restartNeeded: list => `После перезапуска сервера: ${list}.`,
      pending: list => `Ждут перезапуска сервера: ${list}.`,
      restart: 'Перезапустить сервер',
      restarting: 'Перезапускаю…',
      restartNote: 'Клиент переподключится сам.',
      restartNotePort: port => `Клиент сам новый порт не узнает: поставь port: ${port} в разделе server файла client-config.yaml и перезапусти клиент.`,
      restartFailed: 'Сервер не перезапущен',
      restartSaveFirst: 'Сначала сохрани или отмени изменения: сервер перезапустится с тем, что в файле.',
      restartTimeout: 'Сервер не поднялся за 40 секунд — загляни в его консоль.',
      portBusy: port => `Порт ${port} занят другой программой. Выбери другой порт и сохрани.`,
      notSaved: 'Не сохранено',
      fixErrors: 'Поправь поля, отмеченные красным.',
      fileChanged: 'Файл изменён',
      fileChangedText: 'Подхватил правки из конфига.',
      restartChip: 'после перезапуска сервера',
      changedChip: 'изменено',
      alphaChip: 'альфа',
      required: 'Нужно заполнить',
      notNumber: 'Нужно число',
      range: (min, max) => max == null ? `Не меньше ${min}` : `От ${min} до ${max}`,
      on: 'включено',
      off: 'выключено',
      readOnly: 'только в файле',
      modelDefault: 'как у модели',
      test: 'Проверить связь',
      testing: 'Проверяю…',
      testOk: ms => `Работает · ${ms} мс`,
      testReply: reply => `ответ: «${reply}»`,
      testVoice: 'играет образец голоса',
      testDummy: 'Заглушка: проверять нечего',
      testFailed: 'Не работает',
      replay: 'ещё раз',
      show: 'Показать',
      hide: 'Скрыть',
      options: 'Варианты',
      themeDark: 'Тёмная тема',
      themeLight: 'Светлая тема',
      hostChoices: { '127.0.0.1': 'Только этот компьютер', '0.0.0.0': 'Устройства в сети' },
      men: 'Мужчины',
      women: 'Женщины',
      commonVoice: 'общий голос',
      sections: {
        llm: ['Модели', 'Две модели: основная ведёт разговоры, быстрая берёт мелкие задачи. Меняются на лету: следующая реплика уже пойдёт через новую.'],
        stt: ['Речь игрока', 'Кто превращает твой голос в текст. Новая настройка вступает в силу со следующей фразы.'],
        tts: ['Голоса NPC', 'Чем говорят NPC: сервис, голоса по расе и полу, темп речи.'],
        director: ['Режиссёр', 'Как ведут себя NPC: язык, приветствия, болтовня, спутники, квесты, память. Применяется сразу.'],
        server: ['Сервер', 'Как клиент находит сервер. Обычно всё на одном компьютере, и менять тут ничего не нужно.'],
      },
      cards: {
        main: ['Основная модель', 'Ведёт разговоры с NPC: здесь важнее всего качество и умение вызывать действия.'],
        simple: ['Быстрая модель', 'Пишет характеры и болтовню, выбирает, к кому ты обращаешься. Важнее скорость и цена.'],
        stt: ['Распознавание речи', null],
        tts: ['Сервис голосов', null],
        pace: ['Темп речи', 'Слишком длинные реплики ускоряются через ffmpeg.'],
        language: ['Язык и персонаж', null],
        greetings: ['Приветствия', null],
        banter: ['Болтовня', null],
        interjections: ['Реплики группы', null],
        companions: ['Спутники', null],
        quests: ['Квесты', 'Пробная функция: может работать странно. Квесты видно в журнале событий в игре.'],
        memory: ['Память', 'Старые события сводятся в краткие записи, чтобы память не разрасталась.'],
        http: ['Подключение', null],
        advanced: ['Ещё', 'Нужно, только если сервер запущен на другом компьютере, чем игра, или для отладки.'],
      },
      providers: { claude: 'Claude', openai: 'OpenAI', gemini: 'Gemini', deepgram: 'Deepgram', elevenlabs: 'ElevenLabs', dummy: 'Нет' },
      dummy: {
        llm: 'Модель не выбрана. Сервер работает и без неё, но вместо ответов NPC будет тестовая фраза. Выбери облачный сервис или локальную модель: OpenAI / Ollama подходит для любого OpenAI-совместимого сервера на твоём компьютере.',
        stt: 'Распознавание не выбрано: голос игрока не превращается в текст.',
        tts: 'Голоса не выбраны: NPC отвечают только субтитрами.',
      },
      presets: 'Подставить:',
      listLoading: 'Спрашиваю у сервиса список…',
      listFailed: error => `Сервис не отдал список (${error}). Ниже — известные модели.`,
      listEmpty: 'Сервис ничего не вернул.',
      listNeedsKey: 'Впиши ключ API — и здесь будут все модели сервиса.',
      listPrices: 'Цены — за миллион токенов: вход / выход.',
      fields: {
        apiKey: ['Ключ API'],
        model: ['Модель'],
        effort: ['Усилие', 'low отвечает быстрее всего; больше — дольше думает.'],
        baseUrlClaude: ['Адрес сервера', 'Только для прокси; пусто — официальный адрес.'],
        baseUrlOpenAi: ['Адрес сервера', 'Любой OpenAI-совместимый сервер, например Ollama: http://localhost:11434.'],
        baseUrlSpeech: ['Адрес сервера', 'Например, speaches: http://localhost:8000.'],
        thinkingBudget: ['Бюджет размышлений', '0 — без размышлений, быстрее всего.'],
        sttLanguage: ['Язык речи', 'Код языка: ru, en… Тот же, на котором говорят NPC.'],
        sampleRate: ['Частота, Гц'],
        encoding: ['Кодировка'],
        voice: ['Общий голос', 'Для рас и полов без своего голоса.'],
        voicesOpenAi: ['Голоса по расе и полу', 'Пусто — общий голос.'],
        voicesElevenLabs: ['Голоса по расе и полу (id)', 'Пусто — голос по умолчанию.'],
        stability: ['Стабильность', '0–1'],
        similarityBoost: ['Сходство', '0–1'],
        style: ['Выразительность', '0–1'],
        useSpeakerBoost: ['Усиление голоса'],
        fallback: ['Голос по умолчанию (id)'],
        charactersPerSecond: ['Знаков в секунду', 'Реплики длиннее этого темпа ускоряются.'],
        ffmpegExePath: ['Путь к ffmpeg'],
        responseLanguage: ['Язык NPC', 'На нём NPC говорят, пишут поручения и причины.'],
        playerBackstory: ['Кто твой персонаж', 'Что NPC могут знать о тебе с первого взгляда или по слухам.'],
        greetings: ['NPC здороваются первыми', 'Те, кто тебя помнит, когда ты на них смотришь.'],
        greetingCooldownMinutes: ['Не чаще раза в, мин'],
        banter: ['NPC болтают между собой', 'Когда ты молча стоишь рядом.'],
        banterIntervalSeconds: ['Пробовать каждые, сек'],
        banterChance: ['Вероятность', 'От 0 до 1: 0.5 — в половине случаев.'],
        interjections: ['Встревают в разговор', 'Кто стоит рядом и слышал ответ NPC, может вставить своё.'],
        interjectionChance: ['Вероятность', 'От 0 до 1, после каждого ответа NPC.'],
        companions: ['Спутники говорят в дороге', 'Кто пошёл с тобой: пара слов в новом месте и когда долго тихо.'],
        companionTalkIntervalSeconds: ['Пробовать каждые, сек'],
        companionTalkChance: ['Вероятность', 'От 0 до 1, когда долго тихо.'],
        questChains: ['NPC дают квесты', 'Небольшие квесты в 1–2 шага к тем, с кем ты уже говорил, и обратно за наградой.'],
        compactThreshold: ['Сжимать после, событий', 'Столько событий NPC помнит дословно.'],
        compactKeepRecent: ['Оставлять последних', 'Свежие события после сжатия остаются как есть.'],
        host: ['Кто может подключаться', 'Устройства домашней сети (Wi-Fi, кабель) — только если клиент с игрой на другом компьютере. Это не интернет, но подключиться сможет любой в той же сети.'],
        port: ['Порт', 'Поменяй, если 8080 уже занят другой программой, например Steam. Клиенту нужен тот же порт.'],
        clientToken: ['Пароль клиента', 'Клиент присылает его серверу при подключении. Пусто — без пароля.'],
        rpcTimeoutMs: ['Ожидание ответа, мс'],
        maxMessageSize: ['Предел сообщения, байт'],
        consoleLevel: ['Уровень в консоли'],
        fileLevel: ['Уровень в файле'],
        filePath: ['Файл журнала'],
        mainDbPath: ['Основная база'],
        saveGameDbPath: ['База памяти', 'Для каждого прохождения рядом создаётся своя.'],
      },
      names: {
        'llm.main': 'основная модель', 'llm.simple': 'быстрая модель', stt: 'распознавание речи', tts: 'голоса',
        'tts.mp3Speed': 'темп речи', director: 'режиссёр', 'httpServer.clientToken': 'токен клиента',
        'httpServer.host': 'адрес', 'httpServer.port': 'порт', 'httpServer.maxMessageSize': 'предел сообщения',
        'httpServer.rpcTimeoutMs': 'ожидание ответа', database: 'базы', log: 'журналы',
      },
      races: { a: 'Аргониане', b: 'Бретонцы', d: 'Данмеры', h: 'Альтмеры', i: 'Имперцы', k: 'Каджиты', n: 'Норды', o: 'Орки', r: 'Редгарды', w: 'Босмеры' },
    },
    en: {
      subtitle: 'control panel',
      contents: 'Contents',
      clientOn: 'client connected',
      clientOff: 'no client',
      save: 'save',
      fileNote: 'Everything goes into this file, with its comments left alone. You can still edit it by hand: the panel picks that up.',
      loading: 'Loading…',
      unreachable: 'The server does not answer',
      unreachableText: 'Check that the server is running, then reload the page.',
      changes: n => `${n} ${n === 1 ? 'change' : 'changes'}`,
      inFile: f => `goes into ${f}`,
      discard: 'Discard',
      saveBtn: 'Save',
      saving: 'Saving…',
      saved: 'Saved',
      applied: list => `In effect now: ${list}.`,
      restartNeeded: list => `After a server restart: ${list}.`,
      pending: list => `Waiting for a server restart: ${list}.`,
      restart: 'Restart the server',
      restarting: 'Restarting…',
      restartNote: 'The client reconnects by itself.',
      restartNotePort: port => `The client won't learn the new port by itself: set port: ${port} under server in client-config.yaml and restart the client.`,
      restartFailed: 'Server not restarted',
      restartSaveFirst: 'Save or discard your changes first: the server restarts with what is in the file.',
      restartTimeout: 'The server did not come back in 40 seconds: check its console.',
      portBusy: port => `Port ${port} is taken by another program. Pick another port and save.`,
      notSaved: 'Not saved',
      fixErrors: 'Fix the fields marked in red.',
      fileChanged: 'File changed',
      fileChangedText: 'Picked up the edits from the config.',
      restartChip: 'after a server restart',
      changedChip: 'changed',
      alphaChip: 'alpha',
      required: 'Required',
      notNumber: 'Must be a number',
      range: (min, max) => max == null ? `At least ${min}` : `From ${min} to ${max}`,
      on: 'on',
      off: 'off',
      readOnly: 'in the file only',
      modelDefault: 'model default',
      test: 'Test connection',
      testing: 'Testing…',
      testOk: ms => `Works · ${ms} ms`,
      testReply: reply => `reply: “${reply}”`,
      testVoice: 'playing a voice sample',
      testDummy: 'A stand-in: nothing to check',
      testFailed: 'Does not work',
      replay: 'again',
      show: 'Show',
      hide: 'Hide',
      options: 'Options',
      themeDark: 'Dark theme',
      themeLight: 'Light theme',
      hostChoices: { '127.0.0.1': 'This computer only', '0.0.0.0': 'Devices on the network' },
      men: 'Men',
      women: 'Women',
      commonVoice: 'common voice',
      sections: {
        llm: ['Language models', 'Two models: the main one holds conversations, the fast one does small jobs. They change on the fly: the next line already uses the new one.'],
        stt: ['Player speech', 'What turns your voice into text. A new setting takes over from your next phrase.'],
        tts: ['NPC voices', 'How NPCs sound: the service, voices by race and sex, the pace.'],
        director: ['Director', 'How NPCs behave: language, greetings, banter, companions, quests, memory. Applies right away.'],
        server: ['Server', 'How the client finds the server. Usually everything runs on one computer and nothing here needs changing.'],
      },
      cards: {
        main: ['Main model', 'Holds the conversations with NPCs: quality and calling actions matter most.'],
        simple: ['Fast model', 'Writes characters and banter, picks whom you are talking to. Speed and price matter most.'],
        stt: ['Speech recognition', null],
        tts: ['Voice service', null],
        pace: ['Pace', 'Lines that run too long are sped up with ffmpeg.'],
        language: ['Language and character', null],
        greetings: ['Greetings', null],
        banter: ['Banter', null],
        interjections: ['Cutting in', null],
        companions: ['Companions', null],
        quests: ['Quests', 'A trial feature: it may act up. Quests show in the event log in the game.'],
        memory: ['Memory', 'Old events are summed up in short notes, so memory doesn’t grow without end.'],
        http: ['Connection', null],
        advanced: ['More', 'Only for a server on another computer than the game, or for debugging.'],
      },
      providers: { claude: 'Claude', openai: 'OpenAI', gemini: 'Gemini', deepgram: 'Deepgram', elevenlabs: 'ElevenLabs', dummy: 'None' },
      dummy: {
        llm: 'No model chosen. The server runs without one, but NPCs answer with a test phrase. Pick a cloud service or a local model: OpenAI / Ollama fits any OpenAI-compatible server on your computer.',
        stt: 'No speech recognition: your voice is not turned into text.',
        tts: 'No voices: NPCs answer in subtitles only.',
      },
      presets: 'Fill in:',
      listLoading: 'Asking the service for its list…',
      listFailed: error => `The service gave no list (${error}). Below are the models known here.`,
      listEmpty: 'The service listed nothing.',
      listNeedsKey: 'Enter the API key, and every model of the service shows up here.',
      listPrices: 'Prices per million tokens: in / out.',
      fields: {
        apiKey: ['API key'],
        model: ['Model'],
        effort: ['Effort', 'low answers fastest; higher levels think longer.'],
        baseUrlClaude: ['Server URL', 'Only for a proxy; empty: the official endpoint.'],
        baseUrlOpenAi: ['Server URL', 'Any OpenAI-compatible server, e.g. Ollama at http://localhost:11434.'],
        baseUrlSpeech: ['Server URL', 'E.g. speaches at http://localhost:8000.'],
        thinkingBudget: ['Thinking budget', '0: no thinking, fastest.'],
        sttLanguage: ['Spoken language', 'Language code: en, ru… The one your NPCs speak.'],
        sampleRate: ['Sample rate, Hz'],
        encoding: ['Encoding'],
        voice: ['Common voice', 'For races and sexes without a voice of their own.'],
        voicesOpenAi: ['Voices by race and sex', 'Empty: the common voice.'],
        voicesElevenLabs: ['Voices by race and sex (ids)', 'Empty: the fallback voice.'],
        stability: ['Stability', '0–1'],
        similarityBoost: ['Similarity', '0–1'],
        style: ['Style', '0–1'],
        useSpeakerBoost: ['Speaker boost'],
        fallback: ['Fallback voice id'],
        charactersPerSecond: ['Characters per second', 'Lines slower than this are sped up.'],
        ffmpegExePath: ['ffmpeg path'],
        responseLanguage: ['NPC language', 'NPCs speak, write tasks and reasons in it.'],
        playerBackstory: ['Who your character is', 'What NPCs could know about you at a glance or from rumors.'],
        greetings: ['NPCs greet you first', 'Those who remember you, when you look at them.'],
        greetingCooldownMinutes: ['At most once per, min'],
        banter: ['NPCs chat with each other', 'When you stand by quietly.'],
        banterIntervalSeconds: ['Try every, s'],
        banterChance: ['Chance', 'From 0 to 1: 0.5 is half of the time.'],
        interjections: ['NPCs cut in', 'Someone standing close who heard an NPC answer you may add a line.'],
        interjectionChance: ['Chance', 'From 0 to 1, after each NPC answer.'],
        companions: ['Companions talk on the road', 'Whoever follows you: a word in a new place, and when it has been quiet a while.'],
        companionTalkIntervalSeconds: ['Try every, s'],
        companionTalkChance: ['Chance', 'From 0 to 1, when it has been quiet.'],
        questChains: ['NPCs give quests', 'Small quests of 1 or 2 steps to people you have talked to, then back for a reward.'],
        compactThreshold: ['Compact after, events', 'This many events an NPC remembers word for word.'],
        compactKeepRecent: ['Keep the latest', 'Recent events stay as they are after compacting.'],
        host: ['Who may connect', 'Devices on your home network (Wi-Fi, cable): only when the client and the game run on another computer. Not the internet, but anyone on the same network can connect.'],
        port: ['Port', 'Change it if another program, Steam say, already uses 8080. The client needs the same port.'],
        clientToken: ['Client password', 'The client sends it to the server when it connects. Empty: no password.'],
        rpcTimeoutMs: ['Reply timeout, ms'],
        maxMessageSize: ['Message limit, bytes'],
        consoleLevel: ['Console level'],
        fileLevel: ['File level'],
        filePath: ['Log file'],
        mainDbPath: ['Main database'],
        saveGameDbPath: ['Memory database', 'Each playthrough gets one of its own next to it.'],
      },
      names: {
        'llm.main': 'main model', 'llm.simple': 'fast model', stt: 'speech recognition', tts: 'voices',
        'tts.mp3Speed': 'pace', director: 'director', 'httpServer.clientToken': 'client token',
        'httpServer.host': 'address', 'httpServer.port': 'port', 'httpServer.maxMessageSize': 'message limit',
        'httpServer.rpcTimeoutMs': 'reply timeout', database: 'databases', log: 'logs',
      },
      races: { a: 'Argonians', b: 'Bretons', d: 'Dark Elves', h: 'High Elves', i: 'Imperials', k: 'Khajiit', n: 'Nords', o: 'Orcs', r: 'Redguards', w: 'Wood Elves' },
    },
  };

  function plural(n, one, few, many) {
    const m10 = n % 10;
    const m100 = n % 100;
    if (m10 === 1 && m100 !== 11) return one;
    if (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) return few;
    return many;
  }

  const storage = {
    get(key) { try { return localStorage.getItem(key); } catch { return null; } },
    set(key, value) { try { localStorage.setItem(key, value); } catch { /* private mode */ } },
  };

  // The browser keeps them per port, so after a restart on another port they arrive in the address
  const PREFS = ['zdo.lang', 'zdo.theme', 'zdo.section'];
  const carried = new URLSearchParams(location.hash.slice(1));
  if (PREFS.some(key => carried.has(key))) {
    PREFS.forEach(key => carried.has(key) && storage.set(key, carried.get(key)));
    history.replaceState(null, '', location.pathname + location.search);
  }

  let lang = storage.get('zdo.lang') || ((navigator.language || 'en').toLowerCase().startsWith('ru') ? 'ru' : 'en');
  const T = () => STRINGS[lang];

  // Light or dark: the one picked here, else the system's
  let theme = storage.get('zdo.theme');
  const lightQuery = window.matchMedia ? window.matchMedia('(prefers-color-scheme: light)') : null;
  function applyTheme() {
    document.documentElement.dataset.theme = theme || (lightQuery && lightQuery.matches ? 'light' : 'dark');
  }
  applyTheme();
  lightQuery?.addEventListener?.('change', () => { if (!theme) applyTheme(); });

  // ------------------------------------------------------------------ settings, as forms

  const NUMERALS = ['I', 'II', 'III', 'IV', 'V', 'VI'];
  const RACE_KEYS = ['d', 'h', 'w', 'i', 'n', 'b', 'r', 'o', 'a', 'k'];
  const LOG_LEVELS = ['Trace', 'Debug', 'Info', 'Warn', 'Error'];

  // NPC language as the prompt names it, the language's own name, the code speech recognition takes
  const LANGUAGES = [
    ['English', '', 'en'], ['Russian', 'Русский', 'ru'], ['Ukrainian', 'Українська', 'uk'], ['Belarusian', 'Беларуская', 'be'],
    ['Polish', 'Polski', 'pl'], ['Czech', 'Čeština', 'cs'], ['German', 'Deutsch', 'de'], ['French', 'Français', 'fr'],
    ['Spanish', 'Español', 'es'], ['Italian', 'Italiano', 'it'], ['Portuguese', 'Português', 'pt'], ['Turkish', 'Türkçe', 'tr'],
    ['Japanese', '日本語', 'ja'], ['Chinese', '中文', 'zh'], ['Korean', '한국어', 'ko'],
  ];

  const LISTS = {
    // Per million tokens, in / out, as Anthropic listed them in October 2026
    'claude-models': [
      { value: 'claude-fable-5-1', hint: 'Claude Fable 5.1 · $10 / $50' },
      { value: 'claude-opus-5-5', hint: 'Claude Opus 5.5 · $4 / $20' },
      { value: 'claude-sonnet-5-5', hint: 'Claude Sonnet 5.5 · $2 / $10' },
      { value: 'claude-haiku-5-5', hint: 'Claude Haiku 5.5 · $0.10 / $0.50' },
      { value: 'claude-opus-5', hint: 'Claude Opus 5 · $5 / $25' },
      { value: 'claude-sonnet-5', hint: 'Claude Sonnet 5 · $2 / $10' },
      { value: 'claude-opus-4-8', hint: 'Claude Opus 4.8 · $5 / $25' },
      { value: 'claude-sonnet-4-6', hint: 'Claude Sonnet 4.6 · $3 / $15' },
      { value: 'claude-haiku-4-5', hint: 'Claude Haiku 4.5 · $1 / $5' },
    ],
    'openai-models': ['gpt-5', 'gpt-5-mini', 'gpt-5-nano', 'gpt-4.1', 'gpt-4.1-mini', 'gpt-4o', 'gpt-4o-mini'],
    // What people often pull into Ollama; the server's own list shows what is really there
    'local-models': [
      { value: 'qwen2.5:14b', hint: 'ollama pull qwen2.5:14b' },
      { value: 'qwen2.5:7b', hint: 'ollama pull qwen2.5:7b' },
      { value: 'llama3.1:8b', hint: 'ollama pull llama3.1:8b' },
    ],
    'gemini-models': ['gemini-2.5-pro', 'gemini-2.5-flash', 'gemini-2.5-flash-lite', 'gemini-2.0-flash', 'gemini-2.0-flash-lite'],
    'stt-models': ['whisper-1', 'gpt-4o-transcribe', 'gpt-4o-mini-transcribe', 'Systran/faster-whisper-medium'],
    'deepgram-models': ['nova-2', 'nova-3'],
    'tts-models': ['tts-1', 'tts-1-hd', 'gpt-4o-mini-tts'],
    'openai-voices': ['alloy', 'ash', 'ballad', 'coral', 'echo', 'fable', 'nova', 'onyx', 'sage', 'shimmer', 'verse'],
    'elevenlabs-models': ['eleven_multilingual_v2', 'eleven_flash_v2_5', 'eleven_turbo_v2_5', 'eleven_v3'],
    languages: LANGUAGES.map(([name, own]) => ({ value: name, hint: own })),
    'language-codes': LANGUAGES.map(([name, own, code]) => ({ value: code, hint: own || name })),
  };

  const LLM_SERVERS = [['Ollama', 'http://localhost:11434'], ['LM Studio', 'http://localhost:1234'], ['OpenAI', 'https://api.openai.com']];
  // OpenAI itself, as opposed to a server of one's own; no address means OpenAI's default
  const isOpenAi = url => !url || /^https:\/\/api\.openai\.com/.test(url);
  const SPEECH_SERVERS = [['speaches', 'http://localhost:8000'], ['OpenAI', 'https://api.openai.com']];

  // Needed once the provider is chosen; a local server takes any non-empty key
  const apiKey = path => ({ path, label: 'apiKey', mono: true, required: true, secret: true });

  // A model role: the provider picks which block of fields applies
  const llmCard = (id, base, defaultClaude) => ({
    id,
    provider: `${base}.provider`,
    test: base,
    kind: 'llm',
    options: ['claude', 'openai', 'gemini', 'dummy'],
    names: { openai: 'OpenAI / Ollama' },
    blocks: {
      claude: [
        apiKey(`${base}.claude.apiKey`),
        { path: `${base}.claude.model`, label: 'model', mono: true, list: 'claude-models', catalog: base, def: defaultClaude },
        { path: `${base}.claude.effort`, type: 'select', label: 'effort', options: ['', 'low', 'medium', 'high', 'xhigh', 'max'], def: 'low', keepEmpty: true },
        { path: `${base}.claude.baseUrl`, label: 'baseUrlClaude', mono: true, placeholder: 'https://api.anthropic.com' },
      ],
      openai: [
        apiKey(`${base}.openAi.apiKey`),
        { path: `${base}.openAi.baseUrl`, label: 'baseUrlOpenAi', mono: true, def: 'https://api.openai.com', wide: true, presets: LLM_SERVERS },
        {
          path: `${base}.openAi.model`, label: 'model', mono: true, catalog: base, def: 'gpt-4o', wide: true,
          list: () => (isOpenAi(get(state.draft, `${base}.openAi.baseUrl`)) ? 'openai-models' : 'local-models'),
        },
      ],
      gemini: [
        apiKey(`${base}.gemini.apiKey`),
        { path: `${base}.gemini.model`, label: 'model', mono: true, list: 'gemini-models', catalog: base, def: 'gemini-2.5-flash' },
        { path: `${base}.gemini.thinkingBudget`, type: 'number', label: 'thinkingBudget', min: 0, step: 1, def: 0 },
      ],
      dummy: [],
    },
  });

  const SECTIONS = [
    {
      id: 'llm',
      cards: [llmCard('main', 'llm.main', 'claude-opus-5-5'), llmCard('simple', 'llm.simple', 'claude-haiku-5-5')],
    },
    {
      id: 'stt',
      cards: [{
        id: 'stt',
        provider: 'stt.provider',
        test: 'stt',
        kind: 'stt',
        options: ['openai', 'deepgram', 'dummy'],
        names: { openai: 'OpenAI / speaches' },
        blocks: {
          openai: [
            apiKey('stt.openAi.apiKey'),
            { path: 'stt.openAi.baseUrl', label: 'baseUrlSpeech', mono: true, def: 'https://api.openai.com', presets: SPEECH_SERVERS },
            { path: 'stt.openAi.model', label: 'model', mono: true, list: 'stt-models', catalog: 'stt', def: 'whisper-1' },
            { path: 'stt.openAi.language', label: 'sttLanguage', mono: true, list: 'language-codes' },
          ],
          deepgram: [
            apiKey('stt.deepgram.apiKey'),
            { path: 'stt.deepgram.model', label: 'model', mono: true, list: 'deepgram-models', catalog: 'stt', def: 'nova-2' },
            { path: 'stt.deepgram.language', label: 'sttLanguage', mono: true, list: 'language-codes', def: 'en' },
            { path: 'stt.deepgram.sampleRate', type: 'number', label: 'sampleRate', min: 8000, step: 1, def: 16000 },
            { path: 'stt.deepgram.encoding', label: 'encoding', mono: true, def: 'linear16' },
          ],
          dummy: [],
        },
      }],
    },
    {
      id: 'tts',
      cards: [
        {
          id: 'tts',
          provider: 'tts.provider',
          test: 'tts',
          kind: 'tts',
          options: ['openai', 'elevenlabs', 'dummy'],
          names: { openai: 'OpenAI / speaches' },
          blocks: {
            openai: [
              apiKey('tts.openAi.apiKey'),
              { path: 'tts.openAi.baseUrl', label: 'baseUrlSpeech', mono: true, def: 'https://api.openai.com', presets: SPEECH_SERVERS },
              { path: 'tts.openAi.model', label: 'model', mono: true, list: 'tts-models', catalog: 'tts', def: 'tts-1' },
              { path: 'tts.openAi.voice', label: 'voice', mono: true, list: 'openai-voices', def: 'alloy' },
              { path: 'tts.openAi.voiceMapping.byRaceSex', type: 'voices', label: 'voicesOpenAi', list: 'openai-voices', wide: true },
            ],
            elevenlabs: [
              apiKey('tts.elevenLabs.apiKey'),
              { path: 'tts.elevenLabs.model', label: 'model', mono: true, list: 'elevenlabs-models', catalog: 'tts', def: 'eleven_multilingual_v2' },
              { path: 'tts.elevenLabs.voiceIdMapping.fallback', label: 'fallback', mono: true, required: true, voices: 'tts' },
              { path: 'tts.elevenLabs.stability', type: 'number', label: 'stability', min: 0, max: 1, step: 0.05, def: 0.75 },
              { path: 'tts.elevenLabs.similarityBoost', type: 'number', label: 'similarityBoost', min: 0, max: 1, step: 0.05, def: 0.75 },
              { path: 'tts.elevenLabs.style', type: 'number', label: 'style', min: 0, max: 1, step: 0.05, def: 0.2 },
              { path: 'tts.elevenLabs.useSpeakerBoost', type: 'toggle', label: 'useSpeakerBoost', def: false },
              { path: 'tts.elevenLabs.voiceIdMapping.byRaceSex', type: 'voices', label: 'voicesElevenLabs', wide: true, voices: 'tts' },
            ],
            dummy: [],
          },
        },
        {
          id: 'pace',
          fields: [
            { path: 'tts.mp3Speed.charactersPerSecond', type: 'number', label: 'charactersPerSecond', min: 1, step: 1 },
            { path: 'tts.mp3Speed.ffmpegExePath', label: 'ffmpegExePath', mono: true },
          ],
        },
      ],
    },
    {
      id: 'director',
      cards: [
        {
          id: 'language',
          fields: [
            { path: 'director.responseLanguage', label: 'responseLanguage', list: 'languages' },
            { path: 'director.playerBackstory', type: 'textarea', label: 'playerBackstory', wide: true, keepEmpty: true },
          ],
        },
        {
          id: 'greetings',
          fields: [
            { path: 'director.greetings', type: 'toggle', label: 'greetings' },
            { path: 'director.greetingCooldownMinutes', type: 'number', label: 'greetingCooldownMinutes', min: 0, step: 1 },
          ],
        },
        {
          id: 'banter',
          fields: [
            { path: 'director.banter', type: 'toggle', label: 'banter', wide: true },
            { path: 'director.banterIntervalSeconds', type: 'number', label: 'banterIntervalSeconds', min: 10, step: 10 },
            { path: 'director.banterChance', type: 'number', label: 'banterChance', min: 0, max: 1, step: 0.05 },
          ],
        },
        {
          id: 'interjections',
          fields: [
            { path: 'director.interjections', type: 'toggle', label: 'interjections' },
            { path: 'director.interjectionChance', type: 'number', label: 'interjectionChance', min: 0, max: 1, step: 0.05 },
          ],
        },
        {
          id: 'companions',
          fields: [
            { path: 'director.companions', type: 'toggle', label: 'companions', wide: true },
            { path: 'director.companionTalkIntervalSeconds', type: 'number', label: 'companionTalkIntervalSeconds', min: 30, step: 10 },
            { path: 'director.companionTalkChance', type: 'number', label: 'companionTalkChance', min: 0, max: 1, step: 0.05 },
          ],
        },
        {
          id: 'quests',
          fields: [
            { path: 'director.questChains', type: 'toggle', label: 'questChains', wide: true, alpha: true },
          ],
        },
        {
          id: 'memory',
          fields: [
            { path: 'director.compactThreshold', type: 'number', label: 'compactThreshold', min: 2, step: 1 },
            { path: 'director.compactKeepRecent', type: 'number', label: 'compactKeepRecent', min: 1, step: 1 },
          ],
        },
      ],
    },
    {
      id: 'server',
      cards: [
        {
          id: 'http',
          fields: [
            { path: 'httpServer.port', type: 'number', label: 'port', min: 1, max: 65535, step: 1, wide: true },
          ],
        },
        {
          id: 'advanced',
          folded: true,
          fields: [
            { path: 'httpServer.host', type: 'choice', label: 'host', options: ['127.0.0.1', '0.0.0.0'], names: 'hostChoices', wide: true },
            { path: 'httpServer.clientToken', label: 'clientToken', mono: true, secret: true, wide: true },
            { path: 'httpServer.rpcTimeoutMs', type: 'number', label: 'rpcTimeoutMs', min: 100, step: 100 },
            { path: 'httpServer.maxMessageSize', type: 'number', label: 'maxMessageSize', min: 1024, step: 1024 },
            { path: 'log.consoleLevel', type: 'select', label: 'consoleLevel', options: LOG_LEVELS },
            { path: 'log.fileLevel', type: 'select', label: 'fileLevel', options: LOG_LEVELS },
            { path: 'log.filePath', label: 'filePath', mono: true, wide: true },
            { path: 'database.mainDbPath', label: 'mainDbPath', mono: true, wide: true, readOnly: true },
            { path: 'database.saveGameDbPath', label: 'saveGameDbPath', mono: true, wide: true, readOnly: true },
          ],
        },
      ],
    },
  ];

  // ------------------------------------------------------------------ state

  const state = {
    status: null,
    values: null,      // settings in effect, from the server
    restart: [],       // settings read only at start
    pending: [],       // of those, the ones changed since the server started
    file: '',
    draft: null,       // the settings being edited
    errors: {},        // path → message
    section: SECTIONS.some(s => s.id === storage.get('zdo.section')) ? storage.get('zdo.section') : 'llm',
    saving: false,
    offline: false,
    // Cards rise in when the page opens and when the section changes, not on every click
    animate: true,
    tests: {},         // card's test target → { running } or the server's result
    unfolded: {},      // folded card id → shown
  };

  // ------------------------------------------------------------------ helpers

  const $ = sel => document.querySelector(sel);

  function h(tag, props, ...children) {
    const el = document.createElement(tag);
    for (const [key, value] of Object.entries(props || {})) {
      if (value == null || value === false) continue;
      if (key === 'class') el.className = value;
      else if (key === 'text') el.textContent = value;
      else if (key === 'value') el.value = value;
      else if (key === 'checked') el.checked = value;
      else if (key === 'vars') for (const [name, v] of Object.entries(value)) el.style.setProperty(name, v);
      else if (key.startsWith('on')) el.addEventListener(key.slice(2), value);
      else el.setAttribute(key, value === true ? '' : value);
    }
    for (const child of children.flat()) {
      if (child == null || child === false) continue;
      el.append(child instanceof Node ? child : document.createTextNode(String(child)));
    }
    return el;
  }

  function get(obj, path) {
    return path.split('.').reduce((node, key) => (node == null ? undefined : node[key]), obj);
  }

  function set(obj, path, value) {
    const keys = path.split('.');
    let node = obj;
    for (const key of keys.slice(0, -1)) {
      if (node[key] == null || typeof node[key] !== 'object') node[key] = {};
      node = node[key];
    }
    node[keys[keys.length - 1]] = value;
  }

  const clone = value => JSON.parse(JSON.stringify(value));

  const ICONS = {
    eye: 'M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12z M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z',
    eyeOff: 'M3 3l18 18 M10.6 10.6a2 2 0 0 0 2.8 2.8 M9.9 5.2A10 10 0 0 1 12 5c6.4 0 10 7 10 7a17 17 0 0 1-3.1 3.9 M6.6 6.7A17 17 0 0 0 2 12s3.6 7 10 7a9.6 9.6 0 0 0 5.4-1.6',
    chevron: 'M6 9l6 6 6-6',
    sun: 'M12 3v2 M12 19v2 M5.6 5.6l1.4 1.4 M17 17l1.4 1.4 M3 12h2 M19 12h2 M5.6 18.4L7 17 M17 7l1.4-1.4 M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z',
    moon: 'M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5z',
    play: 'M8 5.5v13l11-6.5z',
  };

  function icon(name) {
    const ns = 'http://www.w3.org/2000/svg';
    const svg = document.createElementNS(ns, 'svg');
    svg.setAttribute('viewBox', '0 0 24 24');
    svg.setAttribute('class', `icon icon-${name}`);
    svg.setAttribute('aria-hidden', 'true');
    const path = document.createElementNS(ns, 'path');
    path.setAttribute('d', ICONS[name]);
    svg.append(path);
    return svg;
  }

  // undefined, null and '' all mean "not set"
  const norm = value => (value === undefined || value === null || value === '' ? null : value);

  function fieldLabel(field) {
    return T().fields[field.label] || [field.label];
  }

  function isRestart(path) {
    return state.restart.some(p => path === p || path.startsWith(p + '.'));
  }

  // The fields in effect: a provider card shows only the chosen provider's block
  function fieldsOf(card) {
    if (!card.provider) return card.fields;
    const chosen = get(state.draft, card.provider);
    const providerField = { path: card.provider, type: 'provider', card };
    return [providerField, ...(card.blocks[chosen] || [])];
  }

  // Fields that hold one value each; a voice table holds one per race and sex
  function leafFields(card) {
    return fieldsOf(card).flatMap(field => field.type === 'voices'
      ? RACE_KEYS.flatMap(race => ['m', 'f'].map(sex => ({ ...field, type: 'voice', path: `${field.path}.${race}${sex}` })))
      : [field]);
  }

  function isDirty(field) {
    if (field.readOnly) return false;
    const a = get(state.draft, field.path);
    const b = get(state.values, field.path);
    return field.keepEmpty ? (a ?? null) !== (b ?? null) : norm(a) !== norm(b);
  }

  function dirtyFields(section) {
    return section.cards.flatMap(leafFields).filter(isDirty);
  }

  const allDirty = () => SECTIONS.flatMap(dirtyFields);
  const currentEdits = () => allDirty().map(field => ({ path: field.path, value: editValue(field) }));

  // The value as the file should get it; null removes the setting, so it takes its default
  function editValue(field) {
    const value = get(state.draft, field.path);
    if (field.type === 'number') return value === '' || value == null ? null : Number(value);
    if (field.type === 'toggle' || field.type === 'provider') return value;
    if (typeof value === 'string') {
      const trimmed = value.trim();
      return trimmed === '' && !field.keepEmpty ? null : trimmed;
    }
    return value ?? null;
  }

  // Checks what is being changed, and everything a newly chosen provider needs, such as its key.
  // Settings nobody touched stay as the server accepted them.
  function validate() {
    const errors = {};
    for (const card of SECTIONS.flatMap(section => section.cards)) {
      const providerChanged = Boolean(card.provider) && isDirty({ path: card.provider, type: 'provider' });
      for (const field of leafFields(card)) {
        if (field.readOnly || (!isDirty(field) && !(providerChanged && field.required))) continue;
        const value = get(state.draft, field.path);
        if (field.type === 'number') {
          if (value === '' || value == null) continue;
          const n = Number(value);
          if (!Number.isFinite(n)) errors[field.path] = T().notNumber;
          else if ((field.min != null && n < field.min) || (field.max != null && n > field.max)) errors[field.path] = T().range(field.min, field.max);
        }
        else if (field.required && norm(typeof value === 'string' ? value.trim() : value) == null) {
          errors[field.path] = T().required;
        }
      }
    }
    return errors;
  }

  // ------------------------------------------------------------------ API

  async function api(method, path, body) {
    const response = await fetch(path, {
      method,
      credentials: 'same-origin',
      headers: body ? { 'Content-Type': 'application/json' } : {},
      body: body ? JSON.stringify(body) : undefined,
    });
    if (!response.ok) {
      const error = new Error(`HTTP ${response.status}`);
      error.status = response.status;
      throw error;
    }
    return response.status === 204 ? null : response.json();
  }

  async function loadSettings() {
    const info = await api('GET', '/api/settings');
    state.values = info.values;
    state.restart = info.restartRequired || [];
    state.pending = info.pendingRestart || [];
    state.file = info.path;
    state.draft = clone(info.values);
    state.errors = {};
  }

  async function start() {
    try {
      [state.status] = await Promise.all([api('GET', '/api/status'), loadSettings()]);
      state.offline = false;
    }
    catch {
      state.offline = true;
    }
    render();
  }

  // ------------------------------------------------------------------ rendering

  function render() {
    document.documentElement.lang = lang;
    const app = $('#app');
    app.replaceChildren();
    app.classList.toggle('settled', !state.animate);
    state.animate = false;
    if (state.offline || !state.values) {
      app.append(gate(state.offline ? T().unreachable : T().loading, state.offline ? T().unreachableText : null));
      return;
    }

    // append() would print a null as text
    openCombo = null;
    app.append(...[header(), pendingBanner(), h('div', { class: 'layout' }, contents(), folio()), saveBar()].filter(Boolean));
    updateDirtyUi();
  }

  function header() {
    const status = state.status || {};
    const connected = status.clients > 0;
    return h('header', { class: 'top' },
      h('a', { class: 'brand', href: '/' },
        h('img', { src: '/panel/logo.svg', alt: '' }),
        h('span', null, h('span', { class: 'brand-name', text: 'Zdo RPG AI' }), h('span', { class: 'brand-sub', text: T().subtitle }))),
      h('div', { class: 'top-meta' },
        h('span', { class: 'pill', id: 'pill-client' }, h('span', { class: connected ? 'dot on' : 'dot' }), connected ? T().clientOn : T().clientOff),
        h('span', { class: 'pill', id: 'pill-save' }, T().save, ' ', h('b', { class: 'mono', text: status.saveId || '—' })),
        status.version ? h('span', { class: 'pill' }, h('span', { class: 'mono', text: status.version })) : null,
        h('button', {
          class: 'lang',
          type: 'button',
          title: lang === 'ru' ? 'English' : 'Русский',
          onclick: () => { lang = lang === 'ru' ? 'en' : 'ru'; storage.set('zdo.lang', lang); render(); },
        }, lang === 'ru' ? 'EN' : 'RU'),
        themeButton()));
  }

  function themeButton() {
    const dark = document.documentElement.dataset.theme === 'dark';
    const label = dark ? T().themeLight : T().themeDark;
    return h('button', {
      class: 'lang theme',
      type: 'button',
      title: label,
      'aria-label': label,
      onclick: () => {
        theme = dark ? 'light' : 'dark';
        storage.set('zdo.theme', theme);
        applyTheme();
        render();
      },
    }, icon(dark ? 'sun' : 'moon'));
  }

  function pendingBanner() {
    if (!state.pending.length) return null;
    const names = T().names;
    const button = h('button', { type: 'button', class: 'btn primary', onclick: () => restartServer(button) }, T().restart);
    return h('div', { class: 'banner', role: 'note' },
      h('span', { class: 'banner-mark', text: '↻' }),
      h('span', { class: 'banner-text' }, T().pending(state.pending.map(p => names[p] || p).join(', ')), ' ',
        h('span', { class: 'banner-note', text: state.pending.includes('httpServer.port')
          ? T().restartNotePort(state.values.httpServer.port)
          : T().restartNote })),
      button);
  }

  const wait = ms => new Promise(resolve => setTimeout(resolve, ms));

  // The server restarts with the config in the file, then the page comes back: reloaded when the
  // port stays, opened at the new port when it changed
  async function restartServer(button) {
    if (allDirty().length) {
      toast('error', T().restartFailed, T().restartSaveFirst);
      return;
    }
    button.disabled = true;
    button.textContent = T().restarting;

    let result;
    try {
      result = await api('POST', '/api/restart', {});
    }
    catch (error) {
      result = { ok: false, error: error.message };
    }
    if (!result.ok) {
      toast('error', T().restartFailed, result.port ? T().portBusy(result.port) : result.error);
      button.disabled = false;
      button.textContent = T().restart;
      return;
    }

    const samePort = String(result.port) === (location.port || '80');
    const target = `${location.protocol}//${location.hostname}:${result.port}/`;
    const before = state.status?.started;
    const deadline = Date.now() + 40000;
    await wait(800);
    while (Date.now() < deadline) {
      try {
        if (samePort) {
          // The old run may still answer for a moment: wait for the new one
          const status = await api('GET', '/api/status');
          if (status.started !== before) {
            location.reload();
            return;
          }
        }
        else {
          // Another port is another site to the browser: any answer at all means it's up
          await fetch(target + 'panel/logo.svg', { mode: 'no-cors', cache: 'no-store' });
          const prefs = String(new URLSearchParams(PREFS.filter(key => storage.get(key)).map(key => [key, storage.get(key)])));
          location.href = target + (prefs ? '#' + prefs : '');
          return;
        }
      }
      catch { /* not up yet */ }
      await wait(500);
    }
    toast('error', T().restartFailed, T().restartTimeout);
    button.disabled = false;
    button.textContent = T().restart;
  }

  function contents() {
    return h('nav', { class: 'toc', 'aria-label': T().contents },
      h('p', { class: 'toc-title', text: T().contents }),
      h('ol', null, SECTIONS.map((section, i) => h('li', null,
        h('button', {
          type: 'button',
          'aria-current': section.id === state.section ? 'true' : 'false',
          'data-section': section.id,
          onclick: () => {
            state.section = section.id;
            state.animate = true;
            storage.set('zdo.section', section.id);
            render();
            window.scrollTo({ top: 0 });
          },
        },
        h('span', { class: 'numeral', text: NUMERALS[i] }),
        h('span', { class: 'label', text: T().sections[section.id][0] }),
        h('span', { class: 'count', 'data-count': section.id }))))),
      h('div', { class: 'file-note' }, T().fileNote, h('code', { text: state.file })));
  }

  function folio() {
    const index = SECTIONS.findIndex(s => s.id === state.section);
    const section = SECTIONS[index];
    const [title, intro] = T().sections[section.id];
    return h('main', { class: 'folio' },
      h('div', { class: 'folio-head rise' },
        h('div', { class: 'numeral', text: NUMERALS[index] }),
        h('h1', { text: title }),
        h('p', { text: intro })),
      section.cards.map((card, i) => cardView(card, i + 1)));
  }

  function cardView(card, order) {
    const [title, hint] = T().cards[card.id] || [card.id, null];
    const fields = fieldsOf(card);
    const body = [];

    if (card.provider) {
      const chosen = get(state.draft, card.provider);
      body.push(h('div', { class: 'seg', role: 'group', 'aria-label': title },
        card.options.map(option => h('button', {
          type: 'button',
          'aria-pressed': option === chosen ? 'true' : 'false',
          onclick: () => chooseProvider(card, option),
        }, card.names?.[option] || T().providers[option]))));
      if (chosen === 'dummy') body.push(h('p', { class: 'note', text: T().dummy[card.kind] }));
    }

    const chosen = card.provider ? get(state.draft, card.provider) : null;
    const leaves = leafFields(card);
    const shown = !card.folded || state.unfolded[card.id] || leaves.some(f => isDirty(f) || state.errors[f.path]);
    const grid = shown ? fields.filter(f => f.type !== 'provider').map(fieldView) : [];
    return h('section', { class: 'card rise' + (shown ? '' : ' folded'), vars: { '--i': order }, 'data-card': card.id },
      h('div', { class: 'card-head' },
        h('h2', { text: title }),
        card.provider ? h('span', { class: 'chip dirty', hidden: true, 'data-dirty': card.provider, text: T().changedChip }) : null,
        card.folded ? h('button', {
          type: 'button',
          class: 'fold',
          'aria-expanded': shown ? 'true' : 'false',
          onclick: () => { state.unfolded[card.id] = !shown; render(); },
        }, shown ? T().hide : T().show) : null),
      hint ? h('p', { class: 'card-hint', text: hint }) : null,
      body,
      grid.length ? h('div', { class: 'fields' }, grid) : null,
      card.test && chosen !== 'dummy' ? testRow(card) : null);
  }

  function chooseProvider(card, option) {
    set(state.draft, card.provider, option);
    delete state.tests[card.test];
    // A provider without settings in the file starts from the defaults, written out on save
    for (const field of card.blocks[option] || []) {
      if (field.def !== undefined && get(state.draft, field.path) == null) set(state.draft, field.path, field.def);
    }
    render();
  }

  function fieldView(field) {
    const [label, hint] = fieldLabel(field);
    const id = 'f-' + field.path.replace(/\./g, '-');
    const error = state.errors[field.path];
    const chips = [
      h('span', { class: 'chip dirty', hidden: true, 'data-dirty': field.path, text: T().changedChip }),
      isRestart(field.path) && !field.readOnly ? h('span', { class: 'chip restart', text: '↻ ' + T().restartChip }) : null,
      field.readOnly ? h('span', { class: 'chip restart', text: T().readOnly }) : null,
      field.alpha ? h('span', { class: 'chip alpha', text: T().alphaChip }) : null,
    ];

    let control;
    switch (field.type) {
      case 'choice':
        control = choiceView(field);
        break;
      case 'toggle':
        control = toggleView(field, id);
        break;
      case 'voices':
        control = voicesView(field);
        break;
      default:
        control = inputView(field, id);
    }

    return h('div', { class: 'field' + (field.wide || field.type === 'textarea' ? ' wide' : ''), 'data-path': field.path },
      field.type === 'voices' || field.type === 'toggle' || field.type === 'choice'
        ? h('div', { class: 'label' }, label, chips)
        : h('label', { for: id }, label, chips),
      control,
      field.presets ? presetsView(field) : null,
      error ? h('p', { class: 'error', text: error }) : hint ? h('p', { class: 'hint', text: hint }) : null);
  }

  function onEdit(field, value) {
    set(state.draft, field.path, value);
    if (state.errors[field.path]) {
      delete state.errors[field.path];
      const element = document.querySelector(`.field[data-path="${field.path}"] .error`);
      if (element) element.remove();
      const input = document.getElementById('f-' + field.path.replace(/\./g, '-'));
      if (input) input.classList.remove('invalid');
    }
    updateDirtyUi();
  }

  function inputView(field, id) {
    const value = get(state.draft, field.path);
    const invalid = state.errors[field.path] ? ' invalid' : '';
    const common = { id, class: (field.mono ? 'mono' : '') + invalid, readonly: field.readOnly || null };

    if (field.type === 'select') {
      return h('select', { ...common, onchange: e => onEdit(field, e.target.value) },
        field.options.map(option => h('option', { value: option, selected: option === (value ?? '') ? 'selected' : null },
          option === '' ? T().modelDefault : option)));
    }
    if (field.type === 'textarea') {
      return h('textarea', { ...common, rows: 3, value: value ?? '', oninput: e => onEdit(field, e.target.value) });
    }
    if (field.secret) {
      return secretView({ ...common, value: value ?? '' }, v => onEdit(field, v));
    }
    if (field.list || field.catalog || field.voices) {
      const placeholder = field.placeholder || (field.def != null ? String(field.def) : null);
      return comboView(comboSource(field), { ...common, value: value ?? '', placeholder }, v => onEdit(field, v));
    }
    return h('input', {
      ...common,
      type: field.type === 'number' ? 'number' : 'text',
      inputmode: field.type === 'number' ? 'decimal' : null,
      min: field.min, max: field.max, step: field.step,
      spellcheck: field.mono ? 'false' : null,
      autocomplete: 'off',
      placeholder: field.placeholder || (field.def != null && field.type !== 'number' ? String(field.def) : null),
      value: value ?? '',
      oninput: e => onEdit(field, e.target.value),
    });
  }

  function choiceView(field) {
    const value = get(state.draft, field.path);
    const names = T()[field.names] || {};
    const options = field.options.includes(value) || value == null ? field.options : [...field.options, value];
    return h('div', { class: 'seg', role: 'group' }, options.map(option => h('button', {
      type: 'button',
      'aria-pressed': option === value ? 'true' : 'false',
      title: option,
      onclick: () => { onEdit(field, option); render(); },
    }, names[option] || option)));
  }

  function toggleView(field, id) {
    const checked = Boolean(get(state.draft, field.path));
    const stateText = h('span', { class: 'state', text: checked ? T().on : T().off });
    return h('label', { class: 'toggle', for: id },
      h('input', {
        id,
        type: 'checkbox',
        checked,
        onchange: e => { stateText.textContent = e.target.checked ? T().on : T().off; onEdit(field, e.target.checked); },
      }),
      h('span', { class: 'track' }),
      stateText);
  }

  function voicesView(field) {
    const races = T().races;
    return h('table', { class: 'voices' },
      h('thead', null, h('tr', null, h('th'), h('th', { text: T().men }), h('th', { text: T().women }))),
      h('tbody', null, RACE_KEYS.map(race => h('tr', null,
        h('td', { text: races[race] }),
        ['m', 'f'].map(sex => {
          const path = `${field.path}.${race}${sex}`;
          const props = {
            class: 'mono',
            'data-voice': path,
            spellcheck: 'false',
            autocomplete: 'off',
            'aria-label': `${races[race]}, ${sex === 'm' ? T().men : T().women}`,
            placeholder: T().commonVoice,
            value: get(state.draft, path) ?? '',
          };
          const edit = v => onEdit({ ...field, type: 'voice', path }, v);
          return h('td', null, field.list || field.voices
            ? comboView(comboSource(field), props, edit)
            : h('input', { ...props, oninput: e => edit(e.target.value) }));
        })))));
  }

  // The real value, hidden until the eye is clicked
  function secretView(props, onChange) {
    const input = h('input', { ...props, type: 'password', autocomplete: 'off', spellcheck: 'false', oninput: e => onChange(e.target.value) });
    const eye = h('button', { type: 'button', class: 'eye', title: T().show, 'aria-label': T().show }, icon('eye'));
    eye.addEventListener('click', () => {
      const showing = input.type === 'text';
      input.type = showing ? 'password' : 'text';
      eye.replaceChildren(icon(showing ? 'eye' : 'eyeOff'));
      eye.title = showing ? T().show : T().hide;
      eye.setAttribute('aria-label', eye.title);
    });
    return h('div', { class: 'input-wrap' }, input, eye);
  }

  // Lists the services gave, by what they were asked with: a new key or address asks again
  const PROVIDER_BLOCKS = { claude: 'claude', openai: 'openAi', gemini: 'gemini', deepgram: 'deepgram', elevenlabs: 'elevenLabs' };
  const catalogs = new Map();

  function catalogFor(target, kind) {
    const provider = get(state.draft, `${target}.provider`);
    const at = name => get(state.draft, `${target}.${PROVIDER_BLOCKS[provider]}.${name}`) ?? '';
    // Without a key the service can't be asked; the list known here has to do
    if (!at('apiKey').trim()) return { needsKey: true, listeners: new Set() };
    const key = [target, kind, provider, at('apiKey'), at('baseUrl')].join('|');
    let entry = catalogs.get(key);
    if (!entry) {
      entry = { loading: true, items: null, error: null, listeners: new Set() };
      catalogs.set(key, entry);
      api('POST', '/api/catalog', { target, kind, edits: currentEdits() })
        .then(result => {
          entry.items = result.items.map(item => ({
            value: item.id,
            hint: [item.name, item.size, item.price].filter(Boolean).join(' · ') || null,
            priced: Boolean(item.price),
          }));
          entry.error = result.error;
        })
        .catch(error => { entry.error = error.message; })
        .finally(() => {
          entry.loading = false;
          // A copy: each listener redraws its list and signs up again, and a Set's forEach would
          // visit the new sign-ups too, forever
          [...entry.listeners].forEach(listener => listener());
        });
    }
    return entry;
  }

  // A text field with a list of every option. A datalist shows only the options that match what's
  // already typed, so a filled field would offer nothing but its own value. source: list, the
  // options known here; catalog / voices, the part of the config whose service lists its own.
  let openCombo = null;

  function closeCombo() {
    if (!openCombo) return;
    openCombo.pop.remove();
    openCombo.input.setAttribute('aria-expanded', 'false');
    openCombo.card?.classList.remove('has-pop');
    openCombo.release();
    openCombo = null;
  }

  function comboView(source, props, onChange) {
    const knownItems = () => {
      const name = typeof source.list === 'function' ? source.list() : source.list;
      return (LISTS[name] || []).map(item => (typeof item === 'string' ? { value: item } : item));
    };
    const input = h('input', { ...props, type: 'text', role: 'combobox', 'aria-expanded': 'false', 'aria-autocomplete': 'list' });
    const toggle = h('button', { type: 'button', class: 'combo-btn', tabindex: '-1', title: T().options, 'aria-label': T().options }, icon('chevron'));
    const wrap = h('div', { class: 'combo' }, input, toggle);
    const isOpen = () => openCombo?.wrap === wrap;
    let query = '';

    // Typing narrows the list down; opened with the mouse or the arrow key, it shows everything
    function open(text) {
      closeCombo();
      query = (text || '').trim().toLowerCase();
      const remote = source.catalog ? catalogFor(source.catalog, source.kind) : null;
      const known = knownItems();
      const items = remote?.items?.length ? remote.items : known;
      const shown = query ? items.filter(i => i.value.toLowerCase().includes(query) || (i.hint || '').toLowerCase().includes(query)) : items;

      const rows = [];
      if (remote?.needsKey) rows.push(h('li', { class: 'status', role: 'presentation', text: T().listNeedsKey }));
      else if (remote?.loading) rows.push(h('li', { class: 'status', role: 'presentation', text: T().listLoading }));
      else if (remote?.error) rows.push(h('li', { class: 'status fail', role: 'presentation', text: T().listFailed(remote.error) }));
      else if (remote && !remote.items?.length && !known.length) rows.push(h('li', { class: 'status', role: 'presentation', text: T().listEmpty }));
      rows.push(...shown.map(item => h('li', {
        role: 'option',
        'aria-selected': item.value === input.value ? 'true' : 'false',
        onmousedown: e => { e.preventDefault(); choose(item.value); },
      }, h('span', { class: 'value', text: item.value }), item.hint ? h('span', { class: 'hint', text: item.hint }) : null)));
      if (shown.some(item => item.priced || /\$/.test(item.hint || ''))) rows.push(h('li', { class: 'note', role: 'presentation', text: T().listPrices }));
      if (!rows.length) return;

      const pop = h('ul', { class: 'combo-pop', role: 'listbox' }, rows);
      wrap.append(pop);
      input.setAttribute('aria-expanded', 'true');
      const card = wrap.closest('.card');
      card?.classList.add('has-pop');
      // The list comes back while open: show it in place
      const refresh = () => { if (isOpen()) open(query); };
      remote?.listeners.add(refresh);
      openCombo = { wrap, pop, card, input, active: -1, release: () => remote?.listeners.delete(refresh) };
      // Near the bottom, where the save bar sits, the list opens upwards if there's room above
      const box = pop.getBoundingClientRect();
      if (box.bottom > window.innerHeight - 96 && wrap.getBoundingClientRect().top > box.height + 16) pop.classList.add('up');
      pop.querySelector('[aria-selected="true"]')?.scrollIntoView({ block: 'nearest' });
    }

    function choose(value) {
      input.value = value;
      onChange(value);
      closeCombo();
    }

    function move(step) {
      if (!isOpen()) {
        open('');
        return;
      }
      const options = [...openCombo.pop.querySelectorAll('[role="option"]')];
      if (!options.length) return;
      openCombo.active = (openCombo.active + step + options.length) % options.length;
      options.forEach((o, i) => o.classList.toggle('active', i === openCombo.active));
      options[openCombo.active].scrollIntoView({ block: 'nearest' });
    }

    toggle.addEventListener('mousedown', e => {
      e.preventDefault();
      if (isOpen()) {
        closeCombo();
      }
      else {
        input.focus();
        open('');
      }
    });
    input.addEventListener('click', () => { if (!isOpen()) open(''); });
    input.addEventListener('input', () => { onChange(input.value); open(input.value); });
    input.addEventListener('keydown', e => {
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
        e.preventDefault();
        move(e.key === 'ArrowDown' ? 1 : -1);
      }
      else if (e.key === 'Enter' && isOpen() && openCombo.active >= 0) {
        e.preventDefault();
        choose(openCombo.pop.querySelectorAll('[role="option"]')[openCombo.active].querySelector('.value').textContent);
      }
      else if (e.key === 'Escape' && isOpen()) {
        e.preventDefault();
        closeCombo();
      }
    });
    input.addEventListener('blur', () => setTimeout(() => { if (isOpen()) closeCombo(); }, 150));
    return wrap;
  }

  // Where the field's options come from: the list known here, the service's own, or both
  function comboSource(field) {
    if (field.catalog) return { list: field.list, catalog: field.catalog, kind: 'models' };
    if (field.voices) return { list: field.list, catalog: field.voices, kind: 'voices' };
    return { list: field.list };
  }

  // Local servers listen at known addresses: one click fills the address in, and a key if there's
  // none (local servers ignore it, but it can't be empty)
  function presetsView(field) {
    const keyPath = field.path.replace(/\.baseUrl$/, '.apiKey');
    return h('div', { class: 'presets', 'data-presets': field.path }, h('span', { text: T().presets }),
      field.presets.map(([name, url]) => h('button', {
        type: 'button',
        class: 'preset',
        'data-url': url,
        'aria-pressed': get(state.draft, field.path) === url ? 'true' : 'false',
        onclick: () => {
          set(state.draft, field.path, url);
          const key = get(state.draft, keyPath);
          if (url.startsWith('http://localhost') && !norm(key)) set(state.draft, keyPath, 'local');
          if (!url.startsWith('http://localhost') && key === 'local') set(state.draft, keyPath, '');
          render();
        },
      }, name)));
  }

  // The connection test of a provider card, with the form's values, unsaved ones included
  function testRow(card) {
    const result = state.tests[card.test];
    const button = h('button', {
      type: 'button',
      class: 'btn',
      disabled: result?.running || null,
      onclick: () => runTest(card),
    }, result?.running ? T().testing : T().test);

    let line = null;
    if (result && !result.running) {
      if (result.ok) {
        const parts = [result.dummy ? T().testDummy : T().testOk(result.ms)];
        if (result.reply) parts.push(T().testReply(result.reply));
        if (result.audio) parts.push(T().testVoice);
        line = h('span', { class: 'test-result ok' }, h('span', { class: 'mark', text: '✓' }), h('span', { text: parts.join(' · ') }),
          result.audio ? h('button', { type: 'button', class: 'replay', onclick: () => playSample(result.audio) }, icon('play'), T().replay) : null);
      }
      else {
        line = h('span', { class: 'test-result fail' }, h('span', { class: 'mark', text: '✕' }), h('span', { text: `${T().testFailed}: ${result.error}` }));
      }
    }
    return h('div', { class: 'test-row', 'data-test': card.test }, button, line);
  }

  async function runTest(card) {
    const errors = validate();
    if (Object.keys(errors).length) {
      state.errors = errors;
      render();
      toast('error', T().testFailed, T().fixErrors);
      return;
    }

    state.tests[card.test] = { running: true };
    refreshTest(card);
    let result;
    try {
      result = await api('POST', '/api/test', { target: card.test, edits: currentEdits() });
    }
    catch (error) {
      result = { ok: false, error: error.message };
    }
    state.tests[card.test] = result;
    refreshTest(card);
    if (result.ok && result.audio) playSample(result.audio);
  }

  // Only the test row: the form keeps its focus and what's typed in it
  function refreshTest(card) {
    document.querySelector(`[data-test="${card.test}"]`)?.replaceWith(testRow(card));
  }

  let sample = null;
  function playSample(base64) {
    sample?.pause();
    sample = new Audio('data:audio/mpeg;base64,' + base64);
    sample.play().catch(() => { /* the browser wants a click first: the replay button is one */ });
  }

  function saveBar() {
    return h('div', { class: 'savebar', id: 'savebar', role: 'region' },
      h('div', null,
        h('span', { class: 'what', id: 'savebar-what' }),
        h('span', { class: 'where', text: T().inFile(state.file.split(/[\\/]/).pop()) })),
      h('span', { class: 'spacer' }),
      h('button', { type: 'button', class: 'btn', onclick: discard }, T().discard),
      h('button', { type: 'button', class: 'btn primary', id: 'save-btn', onclick: save }, T().saveBtn));
  }

  function gate(title, text) {
    return h('div', { class: 'gate' }, h('div', { class: 'card rise' },
      h('img', { src: '/panel/logo.svg', alt: '' }),
      h('h1', { text: title }),
      text ? h('p', { text }) : null));
  }

  // Marks changed fields and sections, and shows the save bar while anything is changed
  function updateDirtyUi() {
    const dirty = new Set(allDirty().map(f => f.path));
    document.querySelectorAll('.field[data-path]').forEach(el => {
      const path = el.dataset.path;
      const changed = dirty.has(path) || [...dirty].some(p => p.startsWith(path + '.'));
      el.classList.toggle('is-dirty', changed);
    });
    document.querySelectorAll('[data-dirty]').forEach(el => { el.hidden = !dirty.has(el.dataset.dirty) && ![...dirty].some(p => p.startsWith(el.dataset.dirty + '.')); });
    document.querySelectorAll('[data-voice]').forEach(el => el.classList.toggle('changed', dirty.has(el.dataset.voice)));
    // A typed address unmarks the preset it no longer matches
    document.querySelectorAll('[data-presets]').forEach(row => row.querySelectorAll('.preset').forEach(button =>
      button.setAttribute('aria-pressed', get(state.draft, row.dataset.presets) === button.dataset.url ? 'true' : 'false')));
    for (const section of SECTIONS) {
      const count = dirtyFields(section).length;
      const el = document.querySelector(`[data-count="${section.id}"]`);
      if (el) el.textContent = count ? '• ' + T().changes(count) : '';
    }
    const bar = $('#savebar');
    if (bar) {
      bar.classList.toggle('show', dirty.size > 0);
      $('#savebar-what').textContent = T().changes(dirty.size);
    }
  }

  function discard() {
    state.draft = clone(state.values);
    state.errors = {};
    render();
  }

  async function save() {
    if (state.saving) return;
    const errors = validate();
    if (Object.keys(errors).length) {
      state.errors = errors;
      const first = SECTIONS.find(section => section.cards.flatMap(leafFields).some(f => errors[f.path]));
      if (first) state.section = first.id;
      render();
      toast('error', T().notSaved, T().fixErrors);
      return;
    }

    const edits = currentEdits();
    state.saving = true;
    const button = $('#save-btn');
    if (button) { button.disabled = true; button.textContent = T().saving; }
    try {
      const result = await api('PUT', '/api/settings', { edits });
      if (!result.ok) {
        toast('error', T().notSaved, result.error);
        return;
      }
      await loadSettings();
      render();
      const names = T().names;
      const describe = list => list.map(p => names[p] || p).join(', ');
      const lines = [];
      if (result.applied.length) lines.push(T().applied(describe(result.applied)));
      if (result.restartRequired.length) lines.push(T().restartNeeded(describe(result.restartRequired)));
      toast('ok', T().saved, lines.join(' '));
    }
    catch (error) {
      toast('error', T().notSaved, error.message);
    }
    finally {
      state.saving = false;
      const again = $('#save-btn');
      if (again) { again.disabled = false; again.textContent = T().saveBtn; }
    }
  }

  function toast(kind, title, text) {
    const el = h('div', { class: `toast ${kind}` }, h('b', { text: title }), text ? h('span', { text }) : null);
    $('#toasts').append(el);
    setTimeout(() => {
      el.classList.add('leaving');
      setTimeout(() => el.remove(), 320);
    }, kind === 'error' ? 9000 : 5500);
  }

  // ------------------------------------------------------------------ keeping up with the server

  async function pollStatus() {
    if (!state.values) return;
    try {
      state.status = await api('GET', '/api/status');
      state.offline = false;
      const client = $('#pill-client');
      if (client) {
        const connected = state.status.clients > 0;
        client.replaceChildren(h('span', { class: connected ? 'dot on' : 'dot' }), connected ? T().clientOn : T().clientOff);
      }
      const saveId = $('#pill-save b');
      if (saveId) saveId.textContent = state.status.saveId || '—';
    }
    catch { /* the server is restarting or gone; the next poll tries again */ }
  }

  // Edits made in the file by hand show up here, unless something is being edited in the panel
  async function pollSettings() {
    if (!state.values || state.saving || allDirty().length) return;
    if (document.activeElement && document.activeElement.closest('.folio')) return;
    try {
      const info = await api('GET', '/api/settings');
      if (JSON.stringify(state.values) === JSON.stringify(info.values)) return;
      await loadSettings();
      render();
      toast('ok', T().fileChanged, T().fileChangedText);
    }
    catch { /* the status poll reports trouble */ }
  }

  window.addEventListener('beforeunload', e => {
    if (allDirty().length) e.preventDefault();
  });

  start();
  setInterval(pollStatus, 4000);
  setInterval(pollSettings, 6000);
})();
