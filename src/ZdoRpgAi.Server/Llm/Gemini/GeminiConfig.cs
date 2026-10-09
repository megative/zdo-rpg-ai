namespace ZdoRpgAi.Server.Llm.Gemini;

public class GeminiConfig {
    public required string ApiKey { get; set; }
    public string Model { get; set; } = "gemini-2.5-flash";
    public int ThinkingBudget { get; set; }
}
