# macOS

## 1. Поставь инструменты

Нужен уже настроенный OpenMW 0.50 или новее с Morrowind. Остальное поставь через [Homebrew](https://brew.sh):

```sh
brew install --cask dotnet-sdk
brew install ffmpeg git
```

## 2. Скачай код

```sh
mkdir -p ~/Games && cd ~/Games
git clone https://github.com/megative/zdo-rpg-ai
git clone https://github.com/megative/zdo-rpg-ai-openmw-mod
```

## 3. Подключи мод к OpenMW

Добавь папку мода в лаунчере (**Data Files → Directories**, затем отметь `zdorpgai.omwscripts` на вкладке **Content Files**) или допиши две строки в `~/Library/Preferences/openmw/openmw.cfg`:

```ini
data="/Users/<ты>/Games/zdo-rpg-ai-openmw-mod/zdorpgai"
content=zdorpgai.omwscripts
```

## 4. Создай конфиги

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

## 5. Запусти всё

```sh
./run.sh
```

Скрипт соберёт обе программы, запустит сервер и клиент и подпишет их вывод `[server]` и `[client]`. ++ctrl+c++ останавливает обе.

Когда клиент впервые откроет микрофон, macOS спросит, можно ли терминалу им пользоваться, — разреши. Если окно пропустил, включи доступ в **Системные настройки → Конфиденциальность и безопасность → Микрофон**.

При первом запуске клиент пишет `Waiting for mod handshake`. Теперь запусти OpenMW и загрузи сохранение. Когда клиент напишет `Connected to mod`, а игра покажет *ZdoRPG connected*, всё готово. Дальше — [Игра](../playing.md).
