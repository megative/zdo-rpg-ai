using System.Text.Json.Nodes;

namespace ZdoRpgAi.Server.Llm;

public enum LlmRole { User, Model }

public class LlmMessage {
    public required LlmRole Role { get; init; }
    public string? Text { get; init; }
    public List<LlmToolCall>? ToolCalls { get; init; }
    public List<LlmToolResult>? ToolResults { get; init; }
    // LlmResponse.ProviderContent of a model turn; the provider that made it sends it back as is
    // instead of rebuilding the turn from Text and ToolCalls
    public JsonNode? ProviderContent { get; init; }
}

public class LlmToolCall {
    public required string Id { get; init; }
    public required string Name { get; init; }
    public JsonObject Arguments { get; init; } = new();
}

public class LlmToolResult {
    public required string CallId { get; init; }
    public required string Name { get; init; }
    public required string Result { get; init; }
}
