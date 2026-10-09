# Linux

Zdo RPG AI работает с OpenMW из пакета дистрибутива, из Flatpak и с Windows-сборкой под Wine или Proton. Клиент и сервер всегда работают нативно.

## 1. Поставь инструменты

Нужен уже настроенный OpenMW 0.50 или новее с Morrowind. Поставь .NET SDK 9 или новее, ffmpeg и Git:

=== "Fedora"

    ```sh
    sudo dnf install dotnet-sdk-9.0 ffmpeg-free git
    ```

=== "Ubuntu / Debian"

    ```sh
    sudo apt install dotnet-sdk-9.0 ffmpeg git
    ```

    На старых версиях без `dotnet-sdk-9.0` возьми [пакеты Microsoft](https://learn.microsoft.com/dotnet/core/install/linux).

=== "Arch"

    ```sh
    sudo pacman -S dotnet-sdk ffmpeg git
    ```

=== "Bazzite, Silverblue и другие образные системы"

    [Homebrew](https://brew.sh) ставит всё в домашнюю папку, ничего не нужно наслаивать на систему:

    ```sh
    brew install dotnet ffmpeg git
    ```

## 2. Скачай код

```sh
mkdir -p ~/Games && cd ~/Games
git clone https://github.com/megative/zdo-rpg-ai
git clone https://github.com/megative/zdo-rpg-ai-openmw-mod
```

## 3. Подключи мод к OpenMW

Добавь папку мода в лаунчере (**Data Files → Directories**, затем отметь `zdorpgai.omwscripts` на вкладке **Content Files**) или допиши две строки в `openmw.cfg`:

```ini
data="/home/<ты>/Games/zdo-rpg-ai-openmw-mod/zdorpgai"
content=zdorpgai.omwscripts
```

| OpenMW | Где лежат `openmw.cfg` и `openmw.log` |
|---|---|
| Пакет дистрибутива | `~/.config/openmw/` |
| Flatpak | `~/.var/app/org.openmw.OpenMW/config/openmw/` |
| Wine или Proton | `<префикс>/drive_c/users/<пользователь>/Documents/My Games/OpenMW/` |

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
    logFilePath: ~/.config/openmw/openmw.log

voiceCapture:
  enabled: true
```

Потом заполни конфиг сервера: какой ИИ использовать и твои ключи. Готовые варианты — на странице [Настройка](../configuration.md).

## 5. Запусти всё

```sh
./run.sh
```

Скрипт соберёт обе программы, запустит сервер и клиент и подпишет их вывод `[server]` и `[client]`. ++ctrl+c++ останавливает обе.

При первом запуске клиент пишет `Waiting for mod handshake`. Теперь запусти OpenMW и загрузи сохранение. Когда клиент напишет `Connected to mod`, а игра покажет *ZdoRPG connected*, всё готово. Дальше — [Игра](../playing.md).

## Wine или Proton { #wine-proton }

Windows-сборка OpenMW отлично работает в префиксе Wine (Lutris, Bottles, обычный Wine или Proton), а клиент и сервер — нативно рядом. Мод кладётся внутрь префикса, и клиент читает и пишет туда напрямую.

Например, префикс — `~/Games/omw`, а мод склонирован в его `C:\Games\openmw-mods`:

```sh
cd ~/Games/omw/drive_c/Games/openmw-mods
git clone https://github.com/megative/zdo-rpg-ai-openmw-mod
```

В `openmw.cfg` внутри префикса (в `drive_c/users/<пользователь>/Documents/My Games/OpenMW/`; под Proton пользователь — `steamuser`) пути виндовые:

```ini
data="C:\Games\openmw-mods\zdo-rpg-ai-openmw-mod\zdorpgai"
content=zdorpgai.omwscripts
```

А в конфиге клиента — те же места, но линуксовыми путями:

```yaml
mod:
  provider: openmw
  openmw:
    dataDir: ~/Games/omw/drive_c/Games/openmw-mods/zdo-rpg-ai-openmw-mod/zdorpgai
    logFilePath: ~/Games/omw/drive_c/users/steamuser/Documents/My Games/OpenMW/openmw.log
```

Клавиша разговора работает и здесь: её отслеживает сама игра, так что никому снаружи не нужен доступ к клавиатуре.

## Микрофон

Клиент пишет звук через PortAudio — он работает с PipeWire, PulseAudio и ALSA. При запуске клиент перечисляет найденные устройства и пишет, какое выбрал; чтобы взять другое, укажи его номер в `voiceCapture.deviceIndex`. Пара предупреждений `ALSA lib ...` при старте — это нормально.
