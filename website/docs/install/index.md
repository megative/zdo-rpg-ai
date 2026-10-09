# Installation

Zdo RPG AI has three parts. The **mod** lives inside OpenMW. The **client** and the **server** are two small console programs from the [zdo-rpg-ai](https://github.com/megative/zdo-rpg-ai) repository: the client listens to your microphone and talks to the game, the server talks to the AI services. Both run on your PC (the server can also run on another machine).

## What you need

- **OpenMW 0.50 or newer** with Morrowind, already set up and loading your game.
- **.NET SDK 9 or newer** to build and run the client and the server.
- **ffmpeg**: the server uses it to fit long lines to the subtitles. Without it, long lines come without a voice.
- **Git**, or download the two repositories as ZIP files.
- **A microphone.**
- **Keys for AI services**, or local models. A good start is an Anthropic key for Claude and an OpenAI key for speech and voices; see [Configure](../configuration.md) for other options.

## The steps

1. Install the tools and get the code of the client, the server and the mod.
2. Add the mod to OpenMW.
3. Create the configs: API keys, plus where the mod and the game's log are.
4. Start the client and the server, then the game.
5. Walk up to someone and talk.

The details differ a little per system:

<div class="grid cards" markdown>

-   :material-microsoft-windows:{ .lg } [__Windows__](windows.md)
-   :material-linux:{ .lg } [__Linux__](linux.md) — native, Flatpak, Wine or Proton
-   :material-apple:{ .lg } [__macOS__](macos.md)

</div>

## How the parts find each other

- The **client** and the **server** connect over a local network port (`8080` in the examples). Use the same port in both configs.
- The **client** and the **mod** exchange messages through files: the client writes into the mod's folder (`mod.openmw.dataDir`) and reads the game's log (`mod.openmw.logFilePath`). That's why the client needs both paths.
- OpenMW only sees files that exist when it starts. The client creates them on its first launch, so **on a fresh install start the client before the game**. After that, the order doesn't matter.

!!! tip "Updating"
    Pull both repositories (`git pull`). The `run.sh` script and `dotnet run` rebuild the programs on start. After the mod's scripts change, restart the game or type `reloadlua` in its console.
