namespace ZdoRpgAi.Server.TextToSpeech.OpenAi;

public class OpenAiTtsConfig {
    public required string ApiKey { get; set; }
    public string Model { get; set; } = "tts-1";
    public string Voice { get; set; } = "alloy";
    public string BaseUrl { get; set; } = "https://api.openai.com";

    // Optional per race/sex voice mapping. When a key is missing, falls back to Voice.
    public VoiceMappingConfig? VoiceMapping { get; set; }
}

public class VoiceMappingConfig {
    public Dictionary<string, string> ByRaceSex { get; set; } = new();
}
