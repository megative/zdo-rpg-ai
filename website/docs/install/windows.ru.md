# Windows

## 1. Поставь инструменты

Нужен уже настроенный OpenMW 0.50 или новее с Morrowind. Остальное поставь в PowerShell:

```powershell
winget install Microsoft.DotNet.SDK.9
winget install Gyan.FFmpeg
winget install Git.Git
```

Закрой и снова открой PowerShell, чтобы он увидел новые программы.

## 2. Скачай код

Выбери для него папку, например `C:\Games`:

```powershell
cd C:\Games
git clone https://github.com/megative/zdo-rpg-ai
git clone https://github.com/megative/zdo-rpg-ai-openmw-mod
```

## 3. Подключи мод к OpenMW

=== "Через лаунчер"

    1. Открой OpenMW Launcher, вкладку **Data Files → Directories**, и добавь `C:\Games\zdo-rpg-ai-openmw-mod\zdorpgai`.
    2. На вкладке **Content Files** отметь `zdorpgai.omwscripts`.

=== "Через openmw.cfg"

    Открой `Documents\My Games\OpenMW\openmw.cfg` и допиши в конец:

    ```ini
    data="C:\Games\zdo-rpg-ai-openmw-mod\zdorpgai"
    content=zdorpgai.omwscripts
    ```

!!! note "OneDrive"
    Если папку «Документы» синхронизирует OneDrive, папка OpenMW лежит в `C:\Users\<ты>\OneDrive\Documents\My Games\OpenMW`.

## 4. Создай конфиги

```powershell
cd C:\Games\zdo-rpg-ai
mkdir .tmp
copy example\server-config.example.yaml .tmp\server-config.yaml
copy example\client-config.example.yaml .tmp\client-config.yaml
```

Git не видит папку `.tmp`, так что ключи не попадут в репозиторий.

В `.tmp\client-config.yaml` укажи клиенту папку мода и лог игры и включи микрофон:

```yaml
mod:
  provider: openmw
  openmw:
    dataDir: 'C:\Games\zdo-rpg-ai-openmw-mod\zdorpgai'
    logFilePath: 'C:\Users\<ты>\Documents\My Games\OpenMW\openmw.log'

voiceCapture:
  enabled: true
```

!!! tip "Обратные слэши в YAML"
    Пиши пути Windows в одинарных кавычках, как выше, или через прямые слэши: `C:/Games/...`. В двойных кавычках YAML считает обратный слэш началом спецсимвола.

Потом заполни конфиг сервера: какой ИИ использовать и твои ключи. Готовые варианты — на странице [Настройка](../configuration.md). Если всё работает на одном ПК, поставь `httpServer.host: 127.0.0.1`, чтобы сервер не был виден из сети.

## 5. Запусти всё

Открой два окна PowerShell в `C:\Games\zdo-rpg-ai`. Сервер:

```powershell
$env:DOTNET_ROLL_FORWARD = "Major"
dotnet run --project src/ZdoRpgAi.Server.Console -- --config .tmp/server-config.yaml
```

Клиент:

```powershell
$env:DOTNET_ROLL_FORWARD = "Major"
dotnet run --project src/ZdoRpgAi.Client.Console -- --config .tmp/client-config.yaml
```

`DOTNET_ROLL_FORWARD` позволяет запускать программы на .NET новее 9-го; с .NET 9 он ничего не меняет.

При первом запуске клиент пишет `Waiting for mod handshake`. Теперь запусти OpenMW и загрузи сохранение. Когда клиент напишет `Connected to mod`, а игра покажет *ZdoRPG connected*, всё готово. Дальше — [Игра](../playing.md).
