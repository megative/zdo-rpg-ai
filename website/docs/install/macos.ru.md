# macOS

## 1. Поставь OpenMW и Morrowind

OpenMW возьми с [GitHub](https://github.com/OpenMW/openmw/releases/latest): нужен `.dmg` для macOS, на Apple Silicon — сборка arm64. Перетащи `OpenMW.app` в «Программы». Через Homebrew не выйдет: cask `openmw` отключён, потому что не проходит проверку Gatekeeper. По той же причине первый запуск macOS заблокирует. Открой **Системные настройки → Конфиденциальность и безопасность → «Всё равно открыть»** или сними карантин:

```sh
xattr -dr com.apple.quarantine /Applications/OpenMW.app
```

OpenMW — только движок, данные игры нужны свои. Проще всего с GOG: скачай в библиотеке офлайн-установщик для Windows (`.exe` и `.bin` к нему) и распакуй его без Windows:

```sh
brew install innoextract
innoextract -d ~/Games/Morrowind ~/Downloads/setup_the_elder_scrolls_iii_morrowind_goty*.exe
```

Мак-версия Steam Windows-игру не скачает, так что Steam-копию проще перенести с любого Windows-компьютера: нужна папка `Data Files`.

При первом запуске OpenMW откроется мастер: выбери, что игра уже установлена, и укажи папку, где лежит `Morrowind.esm`.

## 2. Поставь инструменты

Остальное — через [Homebrew](https://brew.sh):

```sh
brew install --cask dotnet-sdk
brew install ffmpeg git
```

## 3. Скачай код

```sh
mkdir -p ~/Games && cd ~/Games
git clone https://github.com/megative/zdo-rpg-ai
git clone https://github.com/megative/zdo-rpg-ai-openmw-mod
```

## 4. Подключи мод к OpenMW

Добавь папку мода в лаунчере (**Data Files → Directories**, затем отметь `zdorpgai.omwscripts` на вкладке **Content Files**) или допиши две строки в `~/Library/Preferences/openmw/openmw.cfg`:

```ini
data="/Users/<ты>/Games/zdo-rpg-ai-openmw-mod/zdorpgai"
content=zdorpgai.omwscripts
```

## 5. Создай конфиги

```sh
cd ~/Games/zdo-rpg-ai
mkdir -p .tmp
cp example/server-config.example.yaml .tmp/server-config.yaml
cp example/client-config.example.yaml .tmp/client-config.yaml
```

В `.tmp/client-config.yaml` укажи клиенту папку мода и лог игры и включи микрофон (`~` — это твоя домашняя папка):

```yaml
mod:
  provider: openmw
  openmw:
    dataDir: ~/Games/zdo-rpg-ai-openmw-mod/zdorpgai
    logFilePath: ~/Library/Preferences/openmw/openmw.log

voiceCapture:
  enabled: true
```

Потом заполни конфиг сервера: какой ИИ использовать и твои ключи. Готовые варианты — на странице [Настройка](../configuration.md).

## 6. Запусти всё

```sh
./run.sh
```

Скрипт соберёт обе программы, запустит сервер и клиент и подпишет их вывод `[server]` и `[client]`. ++ctrl+c++ останавливает обе.

Когда клиент впервые откроет микрофон, macOS спросит, можно ли терминалу им пользоваться, — разреши. Если окно пропустил, включи доступ в **Системные настройки → Конфиденциальность и безопасность → Микрофон**.

При первом запуске клиент пишет `Waiting for mod handshake`. Теперь запусти OpenMW и загрузи сохранение. Когда клиент напишет `Connected to mod`, а игра покажет *ZdoRPG connected*, всё готово. Дальше — [Игра](../playing.md).
