# Zdo RPG AI

System to make NPCs in the RPG alive.

![Screenshot](./doc/screenshot.jpg)

This is evolution of https://github.com/drzdo/immersive_morrowind_llm_ai

Right now this mod is primarily focused on Morrowind. But I would like to keep it extandable for other games as well.

# Instructions

Server:

```sh
dotnet run --project src/ZdoRpgAi.Server.Console -- --config .tmp/server-config.yaml
```

Client:

```sh
dotnet run --project src/ZdoRpgAi.Client.Console -- --config .tmp/client-config.yaml
```

Game mods:

- Morrowind OpenMW https://github.com/drzdo/zdo-rpg-ai-openmw-mod

# Credits

- [drzdo](https://github.com/drzdo): original author of Zdo RPG AI and its OpenMW mod.
- [waterbobo24](https://github.com/waterbobo24): richer NPC and player context, in-game time and item transfers are ported from their forks [Morrowind-llm-server-client](https://github.com/waterbobo24/Morrowind-llm-server-client) and [Morrowind-llm-mod](https://github.com/waterbobo24/Morrowind-llm-mod).
