---
template: home.html
title: Морровинд заговорил
hide:
  - navigation
  - toc
hero:
  kicker: Zdo RPG AI · мод для OpenMW
  title: Морровинд
  title_accent: заговорил.
  lead: Зажми Y и говори с кем угодно в Вварденфелле. Отвечают голосом, помнят тебя, а если договоритесь, делают, а не только обещают.
  primary: Начать
  primary_link: install/
  secondary: Код на GitHub
  note: Windows · Linux · macOS · Claude, OpenAI, Gemini или локальные модели
  image_alt: Фаргот отвечает игроку в Сейда Нине, внизу экрана субтитры
  chip_top: Фаргот, своим голосом
  chip_bottom: зажми и говори
---

<div class="zdo-section" markdown>

<p class="zdo-kicker">Что умеет</p>

## Что делают NPC

<p class="zdo-intro">Заготовленных реплик нет: всё пишет модель, которую ты выберешь. Поэтому иногда выходит смешно, а иногда невпопад.</p>

<div class="grid cards zdo-features" markdown>

-   :material-microphone:{ .lg } __Голос или текст__

    ---

    Зажми ++y++ и говори на любом языке. Не хочешь вслух, нажми ++enter++ и напиши. NPC остановится, повернётся и ответит голосом под свою расу, с субтитрами.

-   :material-brain:{ .lg } __Помнят тебя__

    ---

    У каждого свой характер и своя память о тебе, отдельная для каждого сохранения. Нахамишь сегодня, завтра с тобой поздороваются сквозь зубы.

-   :material-eye-off-outline:{ .lg } __Знают только своё__

    ---

    Твой журнал видит тот, кто сам участвует в квесте. Что ты из Клинков, знают только Клинки. Что ты сказал стражнику, слышали стоявшие рядом, а не вся деревня.

-   :material-sack:{ .lg } __Делают, а не обещают__

    ---

    Отдают вещи и золото, берут плату, идут следом, дерутся, открывают торговлю, обучение и перевозку. Игра всё проверяет: чего нет в кармане, того не отдашь.

-   :material-hiking:{ .lg } __Спутники__

    ---

    Позови кого-нибудь с собой. В новом городе он скажет пару слов о месте, а если долго идёте молча, заговорит первым.

-   :material-account-group:{ .lg } __Город живёт сам__

    ---

    Постой молча, и соседи заболтают между собой. Кто стоит рядом, может влезть в твой разговор. О драке или щедром подарке город узнаёт через пару игровых часов.

-   :material-book-open-variant:{ .lg } __Поручения и квесты__

    ---

    NPC дают свои поручения с настоящей наградой. Квесты игры не ломаются: о них NPC говорит своими словами и открывает обычный диалог. Есть и пробные квесты по цепочке.

-   :material-robot-outline:{ .lg } __Любая модель__

    ---

    Claude, OpenAI, Gemini или всё у себя через Ollama и speaches. Настройки в браузере: сервер сам покажет список моделей и проверит ключи.

</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Управление</p>

## Три клавиши и браузер

<p class="zdo-intro">Клавиши меняются в конфиге клиента. Настройки сервера правятся в браузере и пишутся в тот же YAML, комментарии остаются на месте.</p>

<div class="zdo-keys">
  <div class="zdo-key"><kbd>Y</kbd><p>Зажми и говори. Отпустил, и NPC думает над ответом.</p></div>
  <div class="zdo-key"><kbd>Enter</kbd><p>Открывает строку. Кликни по ней, напиши и снова нажми Enter.</p></div>
  <div class="zdo-key"><kbd>L</kbd><p>Журнал: что тебе дали и поручили. Ещё раз: кто кому что сказал. В третий раз закрывает.</p></div>
  <div class="zdo-key"><kbd>localhost:8080</kbd><p>Панель настроек: модели, голоса, поведение NPC. Работает, пока запущен сервер.</p></div>
</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Что попробовать</p>

## Сценарии на первый вечер

<p class="zdo-intro">Начни в Сейда Нине, там все рядом. Каждый раз выходит по-своему, так и задумано.</p>

<div class="grid cards" markdown>

-   :material-map-marker-path:{ .lg } __Найми провожатого__

    ---

    Подойди к Хрисскару и предложи двадцать монет, чтобы он проводил тебя до Балморы. Согласится, пойдёт следом и в Балморе отпустит пару слов о дороге. Передумаешь, скажи, чтобы шёл домой.

-   :material-incognito:{ .lg } __Проверь, умеют ли молчать__

    ---

    Спроси Фаргота, зачем тебе Кай Косадес. Он понятия не имеет. А Селлус Гравиус знает: это он тебя туда и отправил.

-   :material-account-voice:{ .lg } __Поругайся при свидетелях__

    ---

    Нагруби стражнику. Он станет холоднее, а Фаргот, если стоит рядом, может вставить своё. Слова дальше не уйдут, а вот драку через пару игровых часов обсудит весь Сейда Нин.

-   :material-keyboard-outline:{ .lg } __Напиши вместо голоса__

    ---

    Нажми ++enter++, кликни по строке и спроси, сколько стоит железный кинжал. Пригодится ночью, когда дома спят, и для имён, которые распознавание коверкает.

-   :material-script-text-outline:{ .lg } __Перечитай, что было__

    ---

    Нажми ++l++: поручения и всё, что тебе дали. Ещё раз: последние реплики, кто кому что сказал. Лог лежит в сохранении, загрузишься, и он снова совпадёт с инвентарём.

-   :material-chart-timeline-variant:{ .lg } __Возьми квест по цепочке__

    ---

    Включи «NPC дают квесты» в панели. Поговори с кем-нибудь в Балморе, вернись и спроси Селлуса про работу. Он может послать тебя к тому, кого ты уже видел, а потом обратно за наградой. Пока это альфа.

</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">До игры</p>

## Проверь модель на сценариях

<p class="zdo-intro">Не знаешь, какую модель взять? Эмулятор сыграет за тебя в маленький Сейда Нин: поговорит, дойдёт до Балморы, подождёт пару часов. Потом проверит, что NPC ответили на твоём языке, ничего не выболтали и не полезли в драку зря.</p>

<div class="zdo-run" markdown>

```sh
dotnet run --project src/ZdoRpgAi.ModEmulator.Console -- \
  --scenarios eval/scenarios --config .tmp/server-config.yaml --report report.md
```

На выходе отчёт: что прошло, что нет и сколько модель думала. Прогон тратит немного токенов той модели, что в конфиге. Свои сценарии пишутся в YAML, как это устроено, рассказано [на отдельной странице](scenarios.md).

</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Как устроено</p>

## Три части, один разговор

<p class="zdo-intro">В игре живёт небольшой мод. Слушают и думают две консольные программы на твоём компьютере.</p>

<div class="zdo-flow" markdown>

<div class="zdo-node" markdown>
:material-sword-cross:

### Игра

<p>OpenMW и Lua-мод. Знает, кто рядом и что у кого в карманах, показывает субтитры, проигрывает голоса и делает то, о чём договорились с NPC.</p>
</div>

<div class="zdo-arrow">⇄</div>

<div class="zdo-node" markdown>
:material-microphone-outline:

### Клиент

<p>Пишет твой голос, пока зажата клавиша, и носит сообщения между игрой и сервером.</p>
</div>

<div class="zdo-arrow">⇄</div>

<div class="zdo-node" markdown>
:material-head-cog-outline:

### Сервер

<p>Распознаёт речь, спрашивает модель, что NPC скажет и сделает, озвучивает ответ и хранит память. Может жить и на другом компьютере.</p>
</div>

</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Начать</p>

## Выбери систему

<p class="zdo-intro">Нужны Morrowind, OpenMW 0.50 или новее, микрофон или хотя бы клавиатура, и ключи к ИИ-сервисам либо локальные модели.</p>

<div class="grid cards" markdown>

-   :material-microsoft-windows:{ .lg } __Windows__

    ---

    .NET и ffmpeg через winget, два окна PowerShell.

    [:octicons-arrow-right-24: Установка на Windows](install/windows.md)

-   :material-linux:{ .lg } __Linux__

    ---

    Родной OpenMW, Flatpak или Windows-сборка под Wine или Proton.

    [:octicons-arrow-right-24: Установка на Linux](install/linux.md)

-   :material-apple:{ .lg } __macOS__

    ---

    OpenMW с GitHub, .NET и ffmpeg из Homebrew, запуск одним скриптом.

    [:octicons-arrow-right-24: Установка на macOS](install/macos.md)

</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Авторы</p>

## Сделано многими руками

<div class="zdo-credits" markdown>

- [drzdo](https://github.com/drzdo) создал Zdo RPG AI и его мод для OpenMW, развитие проекта [immersive_morrowind_llm_ai](https://github.com/drzdo/immersive_morrowind_llm_ai).
- [waterbobo24](https://github.com/waterbobo24) добавил подробный контекст NPC и игрока, игровое время, передачу предметов и память для каждого сохранения в своих форках [Morrowind-llm-server-client](https://github.com/waterbobo24/Morrowind-llm-server-client) и [Morrowind-llm-mod](https://github.com/waterbobo24/Morrowind-llm-mod).
- [megative](https://github.com/megative) ведёт этот форк: голоса, Claude, поручения и квесты, болтовню, слухи, спутников, лог в игре, ввод текстом, панель настроек и сценарии проверки.

Zdo RPG AI — свободная программа под лицензией [GPL-3.0](https://github.com/megative/zdo-rpg-ai/blob/main/LICENSE). Morrowind — товарный знак Bethesda Softworks; проект не связан ни с Bethesda, ни с командой OpenMW.

</div>

</div>
