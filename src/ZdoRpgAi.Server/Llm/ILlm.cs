using System.Text.Json.Nodes;

namespace ZdoRpgAi.Server.Llm;

public interface ILlm {
    Task<LlmResponse> ChatAsync(LlmRequest request);
}

public class LlmRequest {
    // Instructions that stay the same across requests. Providers put it first, where it can be
    // cached; SystemPrompt carries what changes from request to request.
    public string? SystemPromptPrefix { get; init; }
    public required string SystemPrompt { get; init; }
    public List<LlmMessage> Messages { get; init; } = [];
    public List<LlmTool> Tools { get; init; } = [];
    public List<LlmResource> Resources { get; init; } = [];
    // False makes the model answer in words; the tools stay declared, as the history may hold their calls
    public bool AllowToolCalls { get; init; } = true;
}

public class LlmResponse {
    public string? Text { get; init; }
    // Why there is no answer: an API error, a refusal. The settings panel's connection test shows it;
    // NPCs just stay silent.
    public string? Error { get; init; }
    public List<LlmToolCall>? ToolCalls { get; init; }
    // The model's turn as the provider returned it, with any thinking signatures: to continue the
    // conversation after tool calls, it goes back in an LlmMessage unchanged
    public JsonNode? ProviderContent { get; init; }
}
