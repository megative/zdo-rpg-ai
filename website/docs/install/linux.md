# Linux

Zdo RPG AI works with OpenMW from a native package, from the Flatpak, and with its Windows build under Wine or Proton. The client and the server always run natively.

## 1. Install the tools

You need OpenMW 0.50 or newer with Morrowind, already set up. Install the .NET SDK 9 or newer, ffmpeg and Git:

=== "Fedora"

    ```sh
    sudo dnf install dotnet-sdk-9.0 ffmpeg-free git
    ```

=== "Ubuntu / Debian"

    ```sh
    sudo apt install dotnet-sdk-9.0 ffmpeg git
    ```

    On older releases without `dotnet-sdk-9.0`, use [Microsoft's packages](https://learn.microsoft.com/dotnet/core/install/linux).

=== "Arch"

    ```sh
    sudo pacman -S dotnet-sdk ffmpeg git
    ```

=== "Bazzite, Silverblue and other image-based systems"

    [Homebrew](https://brew.sh) keeps it all in your home folder, nothing to layer:

    ```sh
    brew install dotnet ffmpeg git
    ```

## 2. Get the code

```sh
mkdir -p ~/Games && cd ~/Games
git clone https://github.com/megative/zdo-rpg-ai
git clone https://github.com/megative/zdo-rpg-ai-openmw-mod
```

## 3. Add the mod to OpenMW

Add the mod's folder in the launcher (**Data Files → Directories**, then tick `zdorpgai.omwscripts` on **Content Files**), or append two lines to `openmw.cfg`:

```ini
data="/home/<you>/Games/zdo-rpg-ai-openmw-mod/zdorpgai"
content=zdorpgai.omwscripts
```

| OpenMW | `openmw.cfg` and `openmw.log` are in |
|---|---|
| Native package | `~/.config/openmw/` |
| Flatpak | `~/.var/app/org.openmw.OpenMW/config/openmw/` |
| Wine or Proton | `<prefix>/drive_c/users/<user>/Documents/My Games/OpenMW/` |

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
    logFilePath: ~/.config/openmw/openmw.log

voiceCapture:
  enabled: true
```

Then fill in the server config: which AI to use and your keys. The [Configure](../configuration.md) page has ready-made setups.

## 5. Start everything

```sh
./run.sh
```

It builds both programs, starts the server and the client, and prefixes their output with `[server]` and `[client]`. ++ctrl+c++ stops both.

On the first start, the client prints `Waiting for mod handshake`. Now start OpenMW and load a save. When the client prints `Connected to mod` and the game shows *ZdoRPG connected*, you're in. Head to [Play](../playing.md).

## Wine or Proton

The Windows build of OpenMW runs fine in a Wine prefix (Lutris, Bottles, plain Wine or Proton), with the client and the server running natively next to it. The mod goes inside the prefix, and the client reads and writes there directly.

Say the prefix is `~/Games/omw` and the mod is cloned into its `C:\Games\openmw-mods`:

```sh
cd ~/Games/omw/drive_c/Games/openmw-mods
git clone https://github.com/megative/zdo-rpg-ai-openmw-mod
```

The prefix's `openmw.cfg` (in `drive_c/users/<user>/Documents/My Games/OpenMW/`; the user is `steamuser` under Proton) gets Windows paths:

```ini
data="C:\Games\openmw-mods\zdo-rpg-ai-openmw-mod\zdorpgai"
content=zdorpgai.omwscripts
```

The client config gets the same places as Linux paths:

```yaml
mod:
  provider: openmw
  openmw:
    dataDir: ~/Games/omw/drive_c/Games/openmw-mods/zdo-rpg-ai-openmw-mod/zdorpgai
    logFilePath: ~/Games/omw/drive_c/users/steamuser/Documents/My Games/OpenMW/openmw.log
```

Push-to-talk works here too: the game itself watches the key, so nothing needs access to your keyboard outside of it.

## Microphone

The client records through PortAudio, which works with PipeWire, PulseAudio and ALSA. At start it lists the audio devices it found and says which one it uses; to pick another, put its number into `voiceCapture.deviceIndex`. A few `ALSA lib ...` warnings at start are normal.
