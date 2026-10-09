# macOS

## 1. Install the tools

You need OpenMW 0.50 or newer with Morrowind, already set up. Install the rest with [Homebrew](https://brew.sh):

```sh
brew install --cask dotnet-sdk
brew install ffmpeg git
```

## 2. Get the code

```sh
mkdir -p ~/Games && cd ~/Games
git clone https://github.com/megative/zdo-rpg-ai
git clone https://github.com/megative/zdo-rpg-ai-openmw-mod
```

## 3. Add the mod to OpenMW

Add the mod's folder in the launcher (**Data Files → Directories**, then tick `zdorpgai.omwscripts` on **Content Files**), or append two lines to `~/Library/Preferences/openmw/openmw.cfg`:

```ini
data="/Users/<you>/Games/zdo-rpg-ai-openmw-mod/zdorpgai"
content=zdorpgai.omwscripts
```

## 4. Create the configs

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

## 5. Start everything

```sh
./run.sh
```

It builds both programs, starts the server and the client, and prefixes their output with `[server]` and `[client]`. ++ctrl+c++ stops both.

The first time the client opens the microphone, macOS asks whether your terminal may use it: allow it. If you missed the prompt, turn it on in **System Settings → Privacy & Security → Microphone**.

On the first start, the client prints `Waiting for mod handshake`. Now start OpenMW and load a save. When the client prints `Connected to mod` and the game shows *ZdoRPG connected*, you're in. Head to [Play](../playing.md).
