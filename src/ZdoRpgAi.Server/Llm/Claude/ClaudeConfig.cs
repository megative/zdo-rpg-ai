namespace ZdoRpgAi.Server.Llm.Claude;

public class ClaudeConfig {
    public required string ApiKey { get; set; }
    public string Model { get; set; } = "claude-opus-5-5";
    // low, medium, high, xhigh or max; empty to leave the model default. NPC lines need little
    // reasoning and come faster at low.
    public string Effort { get; set; } = "low";
    // Only to route requests through a proxy; the official endpoint by default
    public string? BaseUrl { get; set; }
}
