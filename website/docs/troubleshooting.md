# Troubleshooting

First, find the logs. The server and the client print to their consoles and also write `logs/server.log` and `logs/client.log` next to their configs, so `.tmp/logs/` with the setup from this guide. The game writes `openmw.log`; the mod's lines in it carry `[ZDORPG]`.

## The client keeps waiting for the mod

The client prints `Waiting for mod handshake` and never `Connected to mod`.

- **Load a save.** The mod starts working once a game is loaded, not in the main menu.
- **Check `mod.openmw.dataDir`.** It must be the mod's `zdorpgai` folder, the one with `zdorpgai.omwscripts` inside, and the same folder that's in `openmw.cfg`.
- **Check `mod.openmw.logFilePath`.** It must be the `openmw.log` the game is writing right now: its modification time changes while you play.
- **Fresh install?** OpenMW only sees files that existed when it started. Keep the client running and restart the game.
- **Is the mod on?** `openmw.log` should mention `scripts/zdorpgai/global.lua`. If it doesn't, the mod isn't enabled in `openmw.cfg` or the launcher.

## The client can't reach the server

A `Server connection error` or two while the server starts up is normal: the client retries. If it never connects:

- **The port is taken.** If the server fails with "address already in use", another program holds the port. On some systems Steam's web helper takes `8080`. Pick another port, say `18080`, in both configs: `httpServer.port` for the server, `server.port` for the client.
- **The ports or tokens differ.** `httpServer.port` and `server.port` must match, and so must `clientToken`.

## .NET complains about its version

"You must install or update .NET to run this application" means you have a newer .NET than 9. Set the environment variable `DOTNET_ROLL_FORWARD=Major` before starting; `run.sh` already does.

## NPCs don't answer

Look at the server's output right after you speak:

- `API error` with a status code: usually a wrong key, a model name the provider doesn't know, or no credit left.
- `Claude declined to answer`: the model refused that particular line. Rephrase it.
- No `Final recognition` line at all: your speech didn't reach the server; see the microphone below.

## Subtitles, but no voice

- `Failed to generate speech` in the server's output: check the TTS key and model. If it mentions ffmpeg, install it or set `tts.mp3Speed.ffmpegExePath`; without ffmpeg, long lines stay silent.
- Check the voice volume in the game's audio settings.

## The microphone

- Press the push-to-talk key: the client should print `Mic activated`, then `Mic deactivated` when you let go. If nothing appears, check that `voiceCapture.enabled` is `true` and that the key name is right.
- Wrong microphone? The client lists the audio devices at start; put the right number into `voiceCapture.deviceIndex`.
- On macOS, allow your terminal to use the microphone in **System Settings → Privacy & Security → Microphone**.
- Speech recognized in the wrong language: set `stt.openAi.language` (or `stt.deepgram.language`) to the language you speak.

## The text line doesn't type

Click it: Lua in OpenMW can't put the cursor there on its own. If the line doesn't open at all, check `textInput.key` in the client config, and that no other game window is open at the moment.

## An NPC doesn't know what it should

Usually that's on purpose. An NPC knows what it saw and heard itself, and a conversation carries about ten metres. Your journal is seen only by those in a quest, secret factions only by their members, and the town hears of deeds a couple of game hours later. If the NPC really should have known, check it with a scenario: [prompt checks](scenarios.md) show exactly what it was told.

## Answers are slow

- Keep `effort: low` for Claude, `thinkingBudget: 0` for Gemini.
- Use a fast model for `llm.simple`: it picks whom you're talking to and writes characters and banter.
- Local models need a decent GPU; a smaller model answers faster.

## Still stuck?

Open an issue on [GitHub](https://github.com/megative/zdo-rpg-ai/issues) with the server, client and game logs around the moment it went wrong. Remove your API keys from anything you paste.
