# Zdo RPG AI

System to make NPCs in the RPG alive.

![Screenshot](./doc/screenshot.jpg)

This is evolution of https://github.com/drzdo/immersive_morrowind_llm_ai

Right now this mod is primarily focused on Morrowind. But I would like to keep it extandable for other games as well.

Installation, configuration and how to play, in English and Russian: https://megative.github.io/zdo-rpg-ai/

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
- [waterbobo24](https://github.com/waterbobo24): richer NPC and player context, in-game time, item transfers and per-save memory are ported from their forks [Morrowind-llm-server-client](https://github.com/waterbobo24/Morrowind-llm-server-client) and [Morrowind-llm-mod](https://github.com/waterbobo24/Morrowind-llm-mod).

# Local models

Every provider named `openai` talks to `{baseUrl}/v1/...` in the OpenAI format, so any compatible local server works: no API costs, nothing leaves your machine. The `apiKey` must not be empty, but local servers ignore it.

LLM, e.g. [Ollama](https://ollama.com) (`ollama serve` listens on port 11434). NPC actions need a model with tool calling, such as Qwen 2.5 or Llama 3.1:

```yaml
llm:
  main:
    provider: openai
    openAi: { apiKey: local, model: "qwen2.5:14b", baseUrl: "http://localhost:11434" }
  simple:
    provider: openai
    openAi: { apiKey: local, model: "qwen2.5:7b", baseUrl: "http://localhost:11434" }
```

Speech, e.g. [speaches](https://github.com/speaches-ai/speaches), which serves both Whisper transcription and text-to-speech. Pick a voice that speaks your NPC language (for Russian, one of the Piper `ru_RU` voices):

```yaml
stt:
  provider: openai
  openAi: { apiKey: local, model: "Systran/faster-whisper-medium", language: ru, baseUrl: "http://localhost:8000" }
tts:
  provider: openai
  openAi: { apiKey: local, model: "<tts model>", voice: "<voice>", baseUrl: "http://localhost:8000" }
```

# Website

The site lives in `website/` and is built with [Material for MkDocs](https://squidfunk.github.io/mkdocs-material/). Preview it locally:

```sh
pip install -r website/requirements.txt
mkdocs serve -f website/mkdocs.yml
```

Pushes to `main` that touch `website/` publish it to GitHub Pages.
