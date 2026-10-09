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
  lead: Подойди к любому жителю Вварденфелла, зажми клавишу и скажи, что хочешь. Он ответит своим голосом, запомнит тебя, поторгуется, пойдёт следом или полезет в драку, разнесёт слухи о твоих делах — и не сломает квесты игры.
  primary: Начать
  primary_link: install/
  secondary: Код на GitHub
  note: Windows · Linux · macOS — Claude, OpenAI, Gemini или локальные модели
  image_alt: Фаргот отвечает игроку в Сейда Нине, внизу экрана субтитры
  chip_top: Фаргот, своим голосом
  chip_bottom: зажми и говори
---

<div class="zdo-section" markdown>

<p class="zdo-kicker">Что умеет</p>

## Каждый NPC — живой собеседник

<p class="zdo-intro">Это не чат-бот, прикрученный к игре: NPC видят мир вокруг и действуют в нём, поэтому их слова что-то значат.</p>

<div class="grid cards zdo-features" markdown>

-   :material-microphone:{ .lg } __Говори голосом__

    ---

    Зажми клавишу и говори на любом языке. NPC остановится, повернётся к тебе, задумается и ответит вслух — с субтитрами и голосом под свою расу и пол.

-   :material-brain:{ .lg } __Тебя запоминают__

    ---

    У каждого NPC свой характер, и он помнит, что ты говорил и делал, — отдельно для каждого сохранения. Вернёшься — сам с тобой поздоровается.

-   :material-sack:{ .lg } __Не только болтают__

    ---

    Подарки, оплата и взятки переносят настоящие вещи и золото — с проверкой обоих инвентарей. NPC идут за тобой, дерутся, теплеют или обижаются и открывают торговлю, обучение, перевозку, заклинания и ремонт.

-   :material-account-group:{ .lg } __Город судачит__

    ---

    Замолчишь — и соседи заговорят между собой. Твои дела расходятся слухами: обзови стражника, и к вечеру об этом знает вся деревня.

-   :material-book-open-variant:{ .lg } __Квесты целы__

    ---

    NPC читают твой журнал и знают шаги квестов, которые игра выдаёт через них. Когда речь о квесте, они открывают обычный диалог игры и подсказывают тему. А ещё дают свои небольшие поручения — с настоящей наградой.

-   :material-feather:{ .lg } __Верно лору__

    ---

    Упомяни место или тему — NPC вспомнит, что сказал бы об этом в оригинальных диалогах, и перескажет своими словами.

-   :material-robot-outline:{ .lg } __Любой ИИ__

    ---

    Claude, OpenAI или Gemini для разговоров, OpenAI или Deepgram для распознавания речи, OpenAI или ElevenLabs для голосов. Или всё локально через Ollama и speaches — ничего не уходит с твоего ПК.

-   :material-monitor:{ .lg } __Windows, Linux, macOS__

    ---

    Работает везде, где есть OpenMW, в том числе Windows-сборка под Wine или Proton. Клавишу разговора отслеживает сама игра, так что и под Wayland всё в порядке.

</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Без сценария</p>

## Вечер в Сейда Нине

<p class="zdo-intro">Чужеземец только сошёл с корабля и уже не раз обозвал стражников псинами. Фаргот, который уверен, что он тут мэр, отправил его извиняться.</p>

<div class="zdo-chat">
  <div class="zdo-line zdo-line--you">
    <span class="zdo-who">Ты — Селлусу Гравиусу</span>
    <p>Мне мэр сказал, чтобы я перед тобой извинился за то, что я зову стражников псинами. Мне очень жаль. Я так больше делать не буду.</p>
  </div>
  <div class="zdo-line">
    <span class="zdo-who">Селлус Гравиус</span>
    <p>Слышал, слышал. Извинения приняты. Вот так бы сразу, без всяких «бро». Только запомни: ещё раз услышу «псину», и никакие извинения тебе не помогут. А теперь иди к причалу, в Балморе тебя ждёт Косадес.</p>
    <div class="zdo-acts"><span class="zdo-act">отношение +5: извинился искренне</span></div>
  </div>
  <div class="zdo-line zdo-line--you">
    <span class="zdo-who">Ты — Фарготу</span>
    <p>Фаргот, я извинился. Я хочу свой факел.</p>
  </div>
  <div class="zdo-line">
    <span class="zdo-who">Фаргот</span>
    <p>Да-да, слыхал уже! Селлус сказал, что ты там бормотал, как пьяный нетч, но извинение он всё-таки принял. Мэр своё слово держит, вот твой факел, бери!</p>
    <div class="zdo-acts"><span class="zdo-act">даёт тебе факел</span><span class="zdo-act">поручение выполнено: извиниться перед Селлусом Гравиусом</span></div>
  </div>
  <p class="zdo-scene">Через пару минут, пока ты стоишь рядом</p>
  <div class="zdo-line zdo-line--aside">
    <span class="zdo-who">Фаргот — стражнице</span>
    <p>Слыхала? Я лично извинился перед Ганциэлем Дуаром! По-настоящему, при свидетелях. Так поступает истинный мэр Сейда Нин, не находишь?</p>
  </div>
  <div class="zdo-line zdo-line--aside">
    <span class="zdo-who">Имперская стражница</span>
    <p>Мэром тебя никто не выбирал, Фаргот. Но извинился — и правильно. Меньше драк у маяка.</p>
  </div>
</div>

<p class="zdo-caption">Реплики из настоящего прохождения. Здесь ничего не прописано заранее: деревня запомнила оскорбления, разнесла новость, а Фаргот приписал заслугу себе.</p>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Как устроено</p>

## Три части — один разговор

<p class="zdo-intro">В игре живёт небольшой мод. Две консольные программы на твоём ПК слушают и думают.</p>

<div class="zdo-flow" markdown>

<div class="zdo-node" markdown>
:material-sword-cross:

### Игра

<p>OpenMW с Lua-модом: знает, кто рядом, что у кого в карманах и что написано в журнале. Показывает субтитры, проигрывает голоса, выполняет действия NPC.</p>
</div>

<div class="zdo-arrow">⇄</div>

<div class="zdo-node" markdown>
:material-microphone-outline:

### Клиент

<p>Записывает голос, пока зажата клавиша, и передаёт сообщения между игрой и сервером.</p>
</div>

<div class="zdo-arrow">⇄</div>

<div class="zdo-node" markdown>
:material-head-cog-outline:

### Сервер

<p>Превращает речь в текст, спрашивает у ИИ, что NPC скажет и сделает, озвучивает ответ и хранит память каждого NPC. Может работать на другом компьютере.</p>
</div>

</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Начать</p>

## Выбери систему

<p class="zdo-intro">Нужны Morrowind с OpenMW 0.50 или новее, микрофон и ключи к ИИ-сервисам или локальные модели.</p>

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

    .NET и ffmpeg из Homebrew, запуск одним скриптом.

    [:octicons-arrow-right-24: Установка на macOS](install/macos.md)

</div>

</div>

<div class="zdo-section" markdown>

<p class="zdo-kicker">Авторы</p>

## Сделано многими руками

<div class="zdo-credits" markdown>

- [drzdo](https://github.com/drzdo) создал Zdo RPG AI и его мод для OpenMW — развитие проекта [immersive_morrowind_llm_ai](https://github.com/drzdo/immersive_morrowind_llm_ai).
- [waterbobo24](https://github.com/waterbobo24) добавил подробный контекст NPC и игрока, игровое время, передачу предметов и память для каждого сохранения в своих форках [Morrowind-llm-server-client](https://github.com/waterbobo24/Morrowind-llm-server-client) и [Morrowind-llm-mod](https://github.com/waterbobo24/Morrowind-llm-mod).
- [megative](https://github.com/megative) ведёт этот форк: голоса, Claude, квесты, поручения, разговоры NPC между собой, слухи и многое другое.

Zdo RPG AI — свободная программа под лицензией [GPL-3.0](https://github.com/megative/zdo-rpg-ai/blob/main/LICENSE). Morrowind — товарный знак Bethesda Softworks; проект не связан ни с Bethesda, ни с командой OpenMW.

</div>

</div>
