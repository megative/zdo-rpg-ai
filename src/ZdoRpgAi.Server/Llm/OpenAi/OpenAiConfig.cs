namespace ZdoRpgAi.Server.Llm.OpenAi;

public class OpenAiConfig {
    public required string ApiKey { get; set; }
    public string Model { get; set; } = "gpt-4o";
    public string BaseUrl { get; set; } = "https://api.openai.com";
}
