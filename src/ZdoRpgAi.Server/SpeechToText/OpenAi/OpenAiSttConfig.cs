namespace ZdoRpgAi.Server.SpeechToText.OpenAi;

public class OpenAiSttConfig {
    public required string ApiKey { get; set; }
    public string Model { get; set; } = "whisper-1";
    public string? Language { get; set; }
    public string BaseUrl { get; set; } = "https://api.openai.com";
}
