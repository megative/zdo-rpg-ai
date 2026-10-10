# macOS

## 1. Install OpenMW and Morrowind

Get OpenMW from [GitHub](https://github.com/OpenMW/openmw/releases/latest): the macOS `.dmg`, the arm64 build on Apple Silicon. Drag `OpenMW.app` into Applications. Homebrew won't do here: its `openmw` cask is disabled because it fails the Gatekeeper check. For the same reason macOS blocks the first launch. Allow it in **System Settings → Privacy & Security → Open Anyway**, or lift the quarantine:

```sh
xattr -dr com.apple.quarantine /Applications/OpenMW.app
```

OpenMW is only the engine; the game data must be your own. GOG is the easiest: download the offline installer for Windows from your library (the `.exe` and its `.bin`) and unpack it without Windows:

```sh
brew install innoextract
innoextract -d ~/Games/Morrowind ~/Downloads/setup_the_elder_scrolls_iii_morrowind_goty*.exe
```

Steam for Mac won't download the Windows game, so a Steam copy is easiest to bring over from any Windows PC: you need its `Data Files` folder.

On its first start OpenMW runs a wizard: say the game is already installed and point it to the folder with `Morrowind.esm`.

## 2. Install the tools

Install the rest with [Homebrew](https://brew.sh):

```sh
brew install --cask dotnet-sdk
brew install ffmpeg git
```

## 3. Get the code

```sh
mkdir -p ~/Games && cd ~/Games
git clone https://github.com/megative/zdo-rpg-ai
git clone https://github.com/megative/zdo-rpg-ai-openmw-mod
```

## 4. Add the mod to OpenMW

Add the mod's folder in the launcher (**Data Files → Directories**, then tick `zdorpgai.omwscripts` on **Content Files**), or append two lines to `~/Library/Preferences/openmw/openmw.cfg`:

```ini
data="/Users/<you>/Games/zdo-rpg-ai-openmw-mod/zdorpgai"
content=zdorpgai.omwscripts
```

## 5. Create the configs

```sh
cd ~/Games/zdo-rpg-ai
mkdir -p .tmp
cp example/server-config.example.yaml .tmp/server-config.yaml
cp example/client-config.example.yaml .tmp/client-config.yaml
```

In `.tmp/client-config.yaml`, point the client at the mod and at the game's log, and turn on the microphone (`~` stands for your home folder):

```yaml
mod:
  provider: openmw
  openmw:
    dataDir: ~/Games/zdo-rpg-ai-openmw-mod/zdorpgai
    logFilePath: ~/Library/Preferences/openmw/openmw.log

voiceCapture:
  enabled: true
```

Then fill in the server config: which AI to use and your keys. The [Configure](../configuration.md) page has ready-made setups.

## 6. Start everything

```sh
./run.sh
```

It builds both programs, starts the server and the client, and prefixes their output with `[server]` and `[client]`. ++ctrl+c++ stops both.

The first time the client opens the microphone, macOS asks whether your terminal may use it: allow it. If you missed the prompt, turn it on in **System Settings → Privacy & Security → Microphone**.

On the first start, the client prints `Waiting for mod handshake`. Now start OpenMW and load a save. When the client prints `Connected to mod` and the game shows *ZdoRPG connected*, you're in. Head to [Play](../playing.md).
