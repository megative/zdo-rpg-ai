using System.Text.Json.Serialization.Metadata;
using ZdoRpgAi.Core;
using ZdoRpgAi.Repository;
using ZdoRpgAi.Server.App;
using ZdoRpgAi.Server.Http;
using ZdoRpgAi.Server.Panel;
using ZdoRpgAi.Server.Settings;
using ZdoRpgAi.Server.Llm;
using ZdoRpgAi.Server.Llm.Dummy;
using ZdoRpgAi.Server.Llm.Claude;
using ZdoRpgAi.Server.Llm.Gemini;
using ZdoRpgAi.Server.Llm.OpenAi;
using ZdoRpgAi.Server.Lua;
using ZdoRpgAi.Server.SpeechToText;
using ZdoRpgAi.Server.SpeechToText.Deepgram;
using ZdoRpgAi.Server.SpeechToText.Dummy;
using ZdoRpgAi.Server.SpeechToText.OpenAi;
using ZdoRpgAi.Server.TextToSpeech;
using ZdoRpgAi.Server.TextToSpeech.Dummy;
using ZdoRpgAi.Server.TextToSpeech.ElevenLabs;
using ZdoRpgAi.Server.TextToSpeech.OpenAi;

namespace ZdoRpgAi.Server.Bootstrap;

public static class ServerBootstrap {
    private static readonly ILog Log = Logger.Get<ServerApplication>();

    public static void ResolvePaths(ServerConfig config, string configPath) {
        var baseDir = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        config.Database.MainDbPath = ExpandPath(config.Database.MainDbPath, baseDir);
        config.Database.SaveGameDbPath = ExpandPath(config.Database.SaveGameDbPath, baseDir);
        if (config.Log.FilePath != null) {
            config.Log.FilePath = ExpandPath(config.Log.FilePath, baseDir);
        }
    }

    // configPath and configTypeInfo let the web panel change the settings in the file; panel false
    // leaves the panel out, the config file is still watched for edits by hand. restart ends this
    // run for a new one with the config read afresh: the panel's restart button.
    public static ServerApplication Create(ServerConfig config, string configPath, JsonTypeInfo<ServerConfig> configTypeInfo, string version,
        bool panel = true, Action? restart = null) {
        var mainRepo = new LocalDatabaseMainRepository(config.Database.MainDbPath);
        var saveGameRepo = new LocalDatabaseSaveGameRepository(config.Database.SaveGameDbPath);
        // Swappable, so changed settings take effect without a restart
        var tts = new SwitchableTextToSpeech(CreateTts(config.Tts));
        var stt = new SwitchableSpeechToText(CreateStt(config.Stt));
        var mainLlm = new SwitchableLlm(CreateLlm("main", config.Llm.Main));
        var simpleLlm = new SwitchableLlm(CreateLlm("simple", config.Llm.Simple));
        var lua = new LuaSandbox();
        var httpServer = new HttpServer(config.HttpServer);

        var settings = new SettingsStore(configPath, configTypeInfo, config, new LiveSettings(config, mainLlm, simpleLlm, tts, stt));
        if (panel) {
            httpServer.AddEndpoints(new PanelEndpoints(settings, httpServer, saveGameRepo, version, restart).Map);
            Log.Info("Settings panel: http://localhost:{Port}/", config.HttpServer.Port);
        }
        else {
            Log.Info("Settings panel off");
        }

        return new ServerApplication(mainRepo, saveGameRepo, tts, stt, mainLlm, simpleLlm, lua, httpServer, config.Director, config.Tts.Mp3Speed, settings);
    }

    // announce false: a provider made for a look, like the panel's connection test, not for use
    internal static ITextToSpeech CreateTts(TtsSection config, bool announce = true) {
        if (announce) {
            Log.Info("Creating text-to-speech: {Provider}", config.Provider);
        }
        return config.Provider switch {
            "dummy" => new DummyTextToSpeech(),
            "elevenlabs" => new ElevenLabsTextToSpeech(config.ElevenLabs
                ?? throw new InvalidOperationException("tts.elevenLabs config is required when provider is 'elevenlabs'")),
            "openai" => new OpenAiTextToSpeech(config.OpenAi
                ?? throw new InvalidOperationException("tts.openai config is required when provider is 'openai'")),
            _ => throw new InvalidOperationException($"Unknown TTS provider: {config.Provider}"),
        };
    }

    internal static ISpeechToText CreateStt(SttSection config, bool announce = true) {
        if (announce) {
            Log.Info("Creating speech-to-text: {Provider}", config.Provider);
        }
        return config.Provider switch {
            "dummy" => new DummySpeechToText(),
            "deepgram" => new DeepgramSpeechToText(config.Deepgram
                ?? throw new InvalidOperationException("stt.deepgram config is required when provider is 'deepgram'")),
            "openai" => new OpenAiSpeechToText(config.OpenAi
                ?? throw new InvalidOperationException("stt.openai config is required when provider is 'openai'")),
            _ => throw new InvalidOperationException($"Unknown STT provider: {config.Provider}"),
        };
    }

    internal static ILlm CreateLlm(string role, LlmProviderSection config, bool announce = true) {
        if (announce) {
            Log.Info("Creating {Role} LLM: {Provider}", role, config.Provider);
        }
        return config.Provider switch {
            "dummy" => new DummyLlm(),
            "gemini" => new GeminiLlm(config.Gemini
                ?? throw new InvalidOperationException("llm.gemini config is required when provider is 'gemini'")),
            "openai" => new OpenAiLlm(config.OpenAi
                ?? throw new InvalidOperationException("llm.openAi config is required when provider is 'openai'")),
            "claude" => new ClaudeLlm(config.Claude
                ?? throw new InvalidOperationException("llm.claude config is required when provider is 'claude'")),
            _ => throw new InvalidOperationException($"Unknown LLM provider: {config.Provider}"),
        };
    }

    private static string ExpandPath(string path, string baseDir) {
        if (path.StartsWith('~')) {
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/'));
        }

        return Path.GetFullPath(path, baseDir);
    }
}
