namespace ZdoRpgAi.Server.SpeechToText.OpenAi;

public class OpenAiSttConfig {
    public required string ApiKey { get; init; }
    public string Model { get; init; } = "whisper-1";
    public string? Language { get; init; }
    public string BaseUrl { get; init; } = "https://api.openai.com";
}
