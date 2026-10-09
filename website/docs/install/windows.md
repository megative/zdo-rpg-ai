# Windows

## 1. Install the tools

You need OpenMW 0.50 or newer with Morrowind, already set up. Install the rest in PowerShell:

```powershell
winget install Microsoft.DotNet.SDK.9
winget install Gyan.FFmpeg
winget install Git.Git
```

Close and reopen PowerShell so it picks up the new programs.

## 2. Get the code

Pick a folder for it, for example `C:\Games`:

```powershell
cd C:\Games
git clone https://github.com/megative/zdo-rpg-ai
git clone https://github.com/megative/zdo-rpg-ai-openmw-mod
```

## 3. Add the mod to OpenMW

=== "In the launcher"

    1. Open the OpenMW Launcher, go to **Data Files → Directories** and add `C:\Games\zdo-rpg-ai-openmw-mod\zdorpgai`.
    2. On the **Content Files** tab, tick `zdorpgai.omwscripts`.

=== "In openmw.cfg"

    Open `Documents\My Games\OpenMW\openmw.cfg` and add at the end:

    ```ini
    data="C:\Games\zdo-rpg-ai-openmw-mod\zdorpgai"
    content=zdorpgai.omwscripts
    ```

!!! note "OneDrive"
    If your Documents folder is synced by OneDrive, the OpenMW folder is under `C:\Users\<you>\OneDrive\Documents\My Games\OpenMW`.

## 4. Create the configs

```powershell
cd C:\Games\zdo-rpg-ai
mkdir .tmp
copy example\server-config.example.yaml .tmp\server-config.yaml
copy example\client-config.example.yaml .tmp\client-config.yaml
```

The `.tmp` folder is ignored by Git, so your keys stay out of the repository.

In `.tmp\client-config.yaml`, point the client at the mod and at the game's log, and turn on the microphone:

```yaml
mod:
  provider: openmw
  openmw:
    dataDir: 'C:\Games\zdo-rpg-ai-openmw-mod\zdorpgai'
    logFilePath: 'C:\Users\<you>\Documents\My Games\OpenMW\openmw.log'

voiceCapture:
  enabled: true
```

!!! tip "Backslashes in YAML"
    Keep Windows paths in single quotes, as above, or write them with forward slashes: `C:/Games/...`. Inside double quotes, YAML treats backslashes as escapes.

Then fill in the server config: which AI to use and your keys. The [Configure](../configuration.md) page has ready-made setups. If everything runs on this PC, set `httpServer.host: 127.0.0.1` so the server isn't reachable from your network.

## 5. Start everything

Open two PowerShell windows in `C:\Games\zdo-rpg-ai`. The server:

```powershell
$env:DOTNET_ROLL_FORWARD = "Major"
dotnet run --project src/ZdoRpgAi.Server.Console -- --config .tmp/server-config.yaml
```

The client:

```powershell
$env:DOTNET_ROLL_FORWARD = "Major"
dotnet run --project src/ZdoRpgAi.Client.Console -- --config .tmp/client-config.yaml
```

`DOTNET_ROLL_FORWARD` lets the programs run on a newer .NET than 9; with .NET 9 it changes nothing.

On the first start, the client prints `Waiting for mod handshake`. Now start OpenMW and load a save. When the client prints `Connected to mod` and the game shows *ZdoRPG connected*, you're in. Head to [Play](../playing.md).
