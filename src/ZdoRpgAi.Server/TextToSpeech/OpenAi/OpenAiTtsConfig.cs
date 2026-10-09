namespace ZdoRpgAi.Server.TextToSpeech.OpenAi;

public class OpenAiTtsConfig {
    public required string ApiKey { get; init; }
    public string Model { get; init; } = "tts-1";
    public string Voice { get; init; } = "alloy";
    public string BaseUrl { get; init; } = "https://api.openai.com";

    // Optional per race/sex voice mapping. When a key is missing, falls back to Voice.
    public VoiceMappingConfig? VoiceMapping { get; init; }
}

public class VoiceMappingConfig {
    public Dictionary<string, string> ByRaceSex { get; init; } = new();
}
