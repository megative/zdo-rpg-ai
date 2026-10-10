using ZdoRpgAi.Core;
using ZdoRpgAi.Server.Llm.Claude;
using ZdoRpgAi.Server.Llm.Gemini;
using ZdoRpgAi.Server.Llm.OpenAi;
using ZdoRpgAi.Server.SpeechToText.Deepgram;
using ZdoRpgAi.Server.SpeechToText.OpenAi;
using ZdoRpgAi.Server.TextToSpeech.ElevenLabs;
using ZdoRpgAi.Server.TextToSpeech.OpenAi;
using ZdoRpgAi.Server.Util.Mp3;

namespace ZdoRpgAi.Server.Bootstrap;

public class ServerConfig {
    public LogConfig Log { get; set; } = new();
    public required DatabaseSection Database { get; set; }
    public HttpServerSection HttpServer { get; set; } = new();
    public required TtsSection Tts { get; set; }
    public required SttSection Stt { get; set; }
    public required LlmSection Llm { get; set; }
    public DirectorSection Director { get; set; } = new();
}

public class DirectorSection {
    public int CompactThreshold { get; set; } = 30;
    public int CompactKeepRecent { get; set; } = 10;
    public string ResponseLanguage { get; set; } = "Russian";
    // Who the player character is, as NPCs could know it at a glance or from rumours
    public string PlayerBackstory { get; set; } = "";
    // NPCs that remember the player greet them first, at most once per cooldown per NPC
    public bool Greetings { get; set; } = true;
    public double GreetingCooldownMinutes { get; set; } = 10;
    // Every interval, with this chance, two NPCs near a quiet player chat with each other
    public bool Banter { get; set; } = true;
    public double BanterIntervalSeconds { get; set; } = 180;
    public double BanterChance { get; set; } = 0.5;
    // After an NPC answers the player, with this chance someone standing close who heard it may cut
    // in with a line of its own
    public bool Interjections { get; set; } = true;
    public double InterjectionChance { get; set; } = 0.3;
    // NPCs that follow the player say a word on arriving somewhere new and, every interval with
    // this chance, when it's been quiet
    public bool Companions { get; set; } = true;
    public double CompanionTalkIntervalSeconds { get; set; } = 240;
    public double CompanionTalkChance { get; set; } = 0.5;
    // Alpha: NPCs may give small quests whose steps send the player to other NPCs they have met
    public bool QuestChains { get; set; }

    // The running server keeps this instance and reads it on each use, so changed settings are
    // copied into it. A new setting goes here too.
    public void CopyFrom(DirectorSection other) {
        CompactThreshold = other.CompactThreshold;
        CompactKeepRecent = other.CompactKeepRecent;
        ResponseLanguage = other.ResponseLanguage;
        PlayerBackstory = other.PlayerBackstory;
        Greetings = other.Greetings;
        GreetingCooldownMinutes = other.GreetingCooldownMinutes;
        Banter = other.Banter;
        BanterIntervalSeconds = other.BanterIntervalSeconds;
        BanterChance = other.BanterChance;
        Interjections = other.Interjections;
        InterjectionChance = other.InterjectionChance;
        Companions = other.Companions;
        CompanionTalkIntervalSeconds = other.CompanionTalkIntervalSeconds;
        CompanionTalkChance = other.CompanionTalkChance;
        QuestChains = other.QuestChains;
    }
}

public class DatabaseSection {
    public required string MainDbPath { get; set; }
    public required string SaveGameDbPath { get; set; }
}

public class HttpServerSection {
    // This computer only by default; 0.0.0.0 lets clients from the local network in
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8080;
    public int MaxMessageSize { get; set; } = 10_485_760;
    public int RpcTimeoutMs { get; set; } = 5000;
    public string ClientToken { get; set; } = "";
}

public class TtsSection {
    public required string Provider { get; set; }
    public ElevenLabsConfig? ElevenLabs { get; set; }
    public OpenAiTtsConfig? OpenAi { get; set; }
    public Mp3SpeedConfig Mp3Speed { get; set; } = new();
}

public class SttSection {
    public required string Provider { get; set; }
    public DeepgramConfig? Deepgram { get; set; }
    public OpenAiSttConfig? OpenAi { get; set; }
}

public class LlmSection {
    public required LlmProviderSection Main { get; set; }
    public required LlmProviderSection Simple { get; set; }
}

public class LlmProviderSection {
    public required string Provider { get; set; }
    public GeminiConfig? Gemini { get; set; }
    public OpenAiConfig? OpenAi { get; set; }
    public ClaudeConfig? Claude { get; set; }
}
